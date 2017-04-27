using Sitecore.Common;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.EDS.Core.Exceptions;
using Sitecore.EDS.Core.Reporting;
using Sitecore.EmailCampaign.Analytics.Model;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Core.Dispatch;
using Sitecore.Modules.EmailCampaign.Core.Gateways;
using Sitecore.Modules.EmailCampaign.Core.Pipelines;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Factories;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.Modules.EmailCampaign.Recipients;
using Sitecore.Pipelines;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sitecore.Support.Modules.EmailCampaign.Core.Dispatch
{
    public class DispatchTask : Sitecore.Modules.EmailCampaign.Core.Dispatch.DispatchTask
    {
        // Fields
        private Item _campaign;
        private bool _isSuppressionManagerConfigured;
        private int _numDispatchesBeforeNextCampaignUpdate = 1;
        private ReadAheadBuffer<DispatchQueueItem> _recipientQueue;
        private const int CampaignEndDateUpdateInterval = 500;

        // Methods
        private Language DecidePreferredLanguage(Recipient recipient)
        {
            Language targetLanguage = base.Message.TargetLanguage;
            if (base.Message.UsePreferredLanguage)
            {
                CommunicationSettings defaultProperty = recipient.GetProperties<CommunicationSettings>().DefaultProperty;
                if ((defaultProperty != null) && (defaultProperty.PreferredLanguage != null))
                {
                    Language language2;
                    ItemUtilExt itemUtilExt = CoreFactory.Instance.GetItemUtilExt();
                    if (Language.TryParse(defaultProperty.PreferredLanguage, out language2) && itemUtilExt.IsTranslatedTo(base.Message.InnerItem, language2))
                    {
                        targetLanguage = language2;
                    }
                }
            }
            return targetLanguage;
        }

        private MessageItem GenerateEmailContent(Recipient recipient, string emailAddress, Language language, Guid recipientId, Dictionary<string, object> customPersonTokens, out DateTime startGetPageTime, out DateTime endGetPageTime, out DateTime startCollectFilesTime, out DateTime endCollectFilesTime)
        {
            Assert.ArgumentNotNull(recipient, "recipient");
            Assert.ArgumentNotNullOrEmpty(emailAddress, "emailAddress");
            Assert.ArgumentNotNull(language, "language");
            MessageItem item = (MessageItem)base.Message.Clone();
            item.GuidCryptoServiceProvider = base.GuidCryptoServiceProvider;
            startGetPageTime = DateTime.UtcNow;
            item.To = emailAddress;
            item.TargetLanguage = language;
            IMailMessage message = item as IMailMessage;
            if (message != null)
            {
                message.RecipientId = recipientId;
            }
            item.PersonalizationRecipient = recipient;
            base.Factory.Bl.DispatchManager.RestoreCustomTokensFromQueue(item, customPersonTokens);
            if (item.Body == null)
            {
                item.ClearRelatedHtmlCache();
                item.Body = item.GetMessageBody();
            }
            endGetPageTime = DateTime.UtcNow;
            startCollectFilesTime = endGetPageTime;
            IHtmlMail mail = item as IHtmlMail;
            if (mail != null)
            {
                mail.CollectRelativeFiles(false);
            }
            endCollectFilesTime = DateTime.UtcNow;
            return item;
        }

        public override void Initialize(EcmFactory factory, MessageItem messageItem, ILogger logger)
        {
            base.Initialize(factory, messageItem, logger);
            this._campaign = base.Factory.Gateways.SitecoreDataGateway.GetMessageCampaign(base.Message);
            if (this._campaign == null)
            {
                object[] parameters = new object[] { base.Message.InnerItem.DisplayName };
                throw new EmailCampaignException("No campaign item was found for the message '{0}'.", parameters);
            }
            this._isSuppressionManagerConfigured = this.IsSuppressionManagerConfigured();
            EngagementAnalyticsPlanSendingContext customContext = Switcher<EngagementAnalyticsPlanSendingContext, EngagementAnalyticsPlanSendingContext>.CurrentValue;
            if (customContext == null)
            {
                this._recipientQueue = new ReadAheadBuffer<DispatchQueueItem>(() => base.Factory.Gateways.EcmDataGateway.GetNextRecipientForDispatch(base.Message.InnerItem.ID.ToGuid(), base.QueueState), 2, 5);
            }
            else
            {
                this._recipientQueue = new ReadAheadBuffer<DispatchQueueItem>(delegate
                {
                    using (new EngagementAnalyticsPlanSendingContextSwitcher(customContext))
                    {
                        return this.Factory.Gateways.EcmDataGateway.GetNextRecipientForDispatch(this.Message.InnerItem.ID.ToGuid(), this.QueueState);
                    }
                }, 2, 5);
            }
        }

        internal virtual bool IsSuppressed(string email) =>
            (SuppressionManager.GetSingleAsync(email).Result != null);

        internal virtual bool IsSuppressionManagerConfigured() =>
            SuppressionManager.IsConfigured;


        protected override ProgressFeedback OnSendToNextRecipient()
        {
            if (base.InterruptRequest != null)
            {
                switch (base.InterruptRequest())
                {
                    case DispatchInterruptSignal.Abort:
                        return ProgressFeedback.Abort;

                    case DispatchInterruptSignal.Pause:
                        return ProgressFeedback.Pause;
                }
            }
            RecipientId recipientId = null;
            DateTime utcNow = DateTime.UtcNow;
            EcmGlobals.GenerationSemaphore.WaitOne();
            base.Summary.AddTimeDuration(TimeFlag.TaskWorkerWait, utcNow, DateTime.UtcNow);
            SendMessageArgs args = null;
            try
            {
                DateTime startTime = DateTime.UtcNow;
                Context.Site = Util.GetContentSite();
                using (new SecurityDisabler())
                {
                    DispatchQueueItem item;
                    Exception exception;
                    DateTime? nullable;
                    DateTime time3 = DateTime.UtcNow;
                    if (this._recipientQueue.TryGetNextItem(out item, out exception))
                    {
                        if (exception != null)
                        {
                            base.Logger.LogError($"Failed to retrieve recipient for the message '{base.Message.InnerItem.Name}' from the dispatch queue.", exception);
                            return ProgressFeedback.Pause;
                        }
                    }
                    else
                    {
                        return ProgressFeedback.Finish;
                    }
                    base.Summary.AddTimeDuration(TimeFlag.AutomationStateFetch, time3, DateTime.UtcNow);
                    DateTime time4 = DateTime.UtcNow;
                    recipientId = RecipientRepository.GetDefaultInstance().ResolveRecipientId(item.RecipientId);
                    Recipient recipient = null;
                    if (recipientId != null)
                    {
                        try
                        {
                            recipient = RecipientRepository.GetDefaultInstance().GetRecipient(recipientId);
                        }
                        catch (Exception exception2)
                        {
                            base.Logger.LogError("Failed to retrieve recipient.", exception2);
                            throw new NonCriticalException($"Recipient {recipientId} skipped. Failed to retrieve the recipient from its repository.", new object[0]);
                        }
                    }
                    if (recipient == null)
                    {
                        object[] parameters = new object[] { recipientId };
                        throw new NonCriticalException("The recipient '{0}' does not exist.", parameters);
                    }
                    Guid contactId = item.ContactId;
                    Email defaultProperty = recipient.GetProperties<Email>().DefaultProperty;
                    bool flag = (defaultProperty != null) && defaultProperty.IsValid;
                    string email = ((defaultProperty == null) || (defaultProperty.EmailAddress == null)) ? string.Empty : defaultProperty.EmailAddress;
                    bool flag2 = (!base.CheckContactSubscription || !GlobalSettings.Instance.GetCheckContactSubscriptionAfterDispatchPause()) ? true : (((base.Message.MessageType != MessageType.OneTime) && (base.Message.MessageType != MessageType.Subscription)) ? true : base.Message.IsSubscribed(recipientId).Value);
                    bool flag3 = this._isSuppressionManagerConfigured && this.IsSuppressed(email);
                    DateTime endTime = DateTime.UtcNow;
                    Language language = this.DecidePreferredLanguage(recipient);
                    DateTime startSendTime = startTime;
                    TimeSpan duration = new TimeSpan(0L);
                    TimeSpan timeDiff = new TimeSpan(0L);
                    TimeSpan generateMimeTime = new TimeSpan(0L);
                    TimeSpan insertFilesTime = new TimeSpan(0L);
                    TimeSpan replaceTokensTime = new TimeSpan(0L);
                    ExmCustomValues customValues = new ExmCustomValues
                    {
                        DispatchType = item.DispatchType,
                        Email = email,
                        MessageLanguage = language.ToString(),
                        ManagerRootId = base.Message.ManagerRoot.InnerItem.ID.ToGuid(),
                        MessageId = base.Message.InnerItem.ID.ToGuid()
                    };
                    if (!flag2 | flag3)
                    {
                        nullable = null;
                        base.Factory.Gateways.EcmDataGateway.SetMessageStatisticData(this._campaign.ID.ToGuid(), nullable, null, FieldUpdate.Add<int>(-1));
                        if (!flag2)
                        {
                            base.Logger.LogInfo($"Recipient { recipientId} skipped due to the recipient has been added to OptOut list during the sending process.");
                        }
                        else
                        {
                            base.Logger.LogInfo($"Recipient { recipientId} skipped because the recipient is in the suppression list.");
                        }
                    }
                    else if (!flag)
                    {
                        base.Factory.Bl.DispatchManager.EnrollOrUpdateContact(contactId, item, base.Message.PlanId.ToGuid(), "Invalid Address", customValues);
                        base.Logger.LogWarn($"Message '{base.Message.InnerItem.Name}': Recipient is skipped. " + $"'{email}' is not a valid email address.");
                    }
                    else
                    {
                        DateTime time9;
                        DateTime time10;
                        DateTime time11;
                        DateTime time12;
                        EnrollOrUpdateContactResult result;
                        MessageItem messageItem = this.GenerateEmailContent(recipient, email, language, contactId, item.CustomPersonTokens, out time9, out time10, out time11, out time12);
                        if (base.Factory.Bl.TestLabHelper.IsTestConfigured(base.Message))
                        {
                            customValues.TestValueIndex = base.Factory.Bl.TestLabHelper.GetMessageTestValueIndex(messageItem);
                        }
                        try
                        {
                            result = base.Factory.Bl.DispatchManager.EnrollOrUpdateContact(contactId, item, base.Message.PlanId.ToGuid(), "Send Completed", customValues);
                            CustomValuesManager.RegisterCustomValuesToTasks(customValues, contactId);
                        }
                        catch (Exception exception3)
                        {
                            base.Logger.LogError("Failed to enroll a contact in the engagement plan.", exception3);
                            throw new NonCriticalException($"Recipient {recipientId} skipped. Failed to enroll its corresponding contact in the engagement plan.", new object[0]);
                        }
                        if (result == EnrollOrUpdateContactResult.Failed)
                        {
                            base.Logger.LogInfo($"Recipient {recipientId} could not be enrolled in the engagement plan for message {base.Message.InnerItem.Name}.");
                        }
                        if ((base.Message.MessageType == MessageType.Triggered) && (result == EnrollOrUpdateContactResult.ContactEnrolled))
                        {
                            nullable = null;
                            base.Factory.Gateways.EcmDataGateway.SetMessageStatisticData(this._campaign.ID.ToGuid(), nullable, null, FieldUpdate.Add<int>(1));
                        }
                        args = new SendMessageArgs(messageItem, this);
                        CorePipeline.Run("SendEmail", args);
                        if (args.Aborted)
                        {
                            base.Logger.LogInfo($"The '{"SendEmail"}' pipeline is aborted.");
                            object[] objArray2 = new object[] { recipientId };
                            throw new NonCriticalException("Message not sent to the following recipient: {0}.", objArray2);
                        }
                        duration = Util.GetTimeDiff(time9, time10);
                        timeDiff = Util.GetTimeDiff(time11, time12);
                        startSendTime = args.StartSendTime;
                        generateMimeTime = args.GenerateMimeTime;
                        insertFilesTime = args.InsertFilesTime;
                        replaceTokensTime = args.ReplaceTokensTime;
                    }
                    DateTime time7 = DateTime.UtcNow;
                    base.Factory.Gateways.EcmDataGateway.DeleteRecipientsFromDispatchQueue(item.Id);
                    if (Interlocked.Decrement(ref this._numDispatchesBeforeNextCampaignUpdate) == 0)
                    {
                        Interlocked.Exchange(ref this._numDispatchesBeforeNextCampaignUpdate, 500);
                        base.Factory.Gateways.EcmDataGateway.SetCampaignEndDate(this._campaign.ID.ToGuid(), DateTime.UtcNow);
                    }
                    Util.TraceTimeDiff("MarkMessageSent", time7);
                    DateTime time8 = DateTime.UtcNow;
                    if (flag)
                    {
                        base.Summary.AddTimeDuration(TimeFlag.Generate, startTime, startSendTime);
                        base.Summary.AddTimeDuration(TimeFlag.Send, startSendTime, time8);
                    }
                    base.Summary.AddTimeDuration(TimeFlag.Process, startTime, time8);
                    base.Summary.AddTimeDuration(TimeFlag.LoadUser, time4, endTime);
                    base.Summary.AddTimeDuration(TimeFlag.GetPage, duration);
                    base.Summary.AddTimeDuration(TimeFlag.CollectFiles, timeDiff);
                    base.Summary.AddTimeDuration(TimeFlag.GenerateMime, generateMimeTime);
                    base.Summary.AddTimeDuration(TimeFlag.InsertFiles, insertFilesTime);
                    base.Summary.AddTimeDuration(TimeFlag.ReplaceTokens, replaceTokensTime);
                    base.Summary.GetNextCpuValue();
                    if (GlobalSettings.Debug)
                    {
                        TimeSpan span6 = Util.GetTimeDiff(startTime, startSendTime);
                        TimeSpan span7 = Util.GetTimeDiff(startTime, time8);
                        string format = flag ? "Detailed time statistics for '{0}' ({1})\r\nProcess the message: {2}\r\n Generate the message: {3}\r\n   Load user: {4}\r\n   Get page (render page, correct html): {5}\r\n   Collect files (embedded images) in memory: {6}\r\n   Generate MIME: {7}\r\n     Insert files (embedded images) to MIME: {8}\r\n     Personalize (replace $tokens$, insert campaign event ID): {9}\r\n Send the message: {10}\r\n" : "Detailed time statistics for '{0}' ({1})\r\nrecipient skipped due to invalid e-mail.\r\nProcess the message: {2}\r\n   Load user: {4}\r\n";
                        base.Logger.LogInfo(string.Format(format, new object[] { base.Message.InnerItem.DisplayName, recipientId, span7, span6, Util.GetTimeDiff(time4, endTime), duration, timeDiff, generateMimeTime, insertFilesTime, replaceTokensTime, startSendTime }));
                    }
                }
            }
            catch (NonCriticalException exception4)
            {
                base.Logger.LogError(exception4);
                if (recipientId != null)
                {
                    base.Logger.LogError($"Failed to send '{base.Message.InnerItem.Name}' to '{recipientId}'.");
                }
            }
            catch (SmtpException exception5)
            {
                base.Logger.LogError("Message sending error: " + exception5);
                return ProgressFeedback.Pause;
            }
            catch (AggregateException exception6)
            {
                base.Logger.LogError("Message sending error: " + exception6);
                if (recipientId != null)
                {
                    base.Logger.LogError($"Failed to send '{base.Message.InnerItem.Name}' to '{recipientId}'.");
                }
                bool pause = true;
                exception6.Flatten().Handle(delegate (Exception x)
                {
                    if (x is InvalidMessageException)
                    {
                        pause = false;
                    }
                    return true;
                });
                if (pause)
                {
                    return ProgressFeedback.Pause;
                }
            }
            catch (Exception exception7)
            {
                if (recipientId != null)
                {
                    base.Logger.LogError($"Failed to send '{base.Message.InnerItem.Name}' to '{recipientId}'.");
                }
                base.Logger.LogError("Message sending error: " + exception7);
                return ProgressFeedback.Pause;
            }
            finally
            {
                if ((args == null) || (args.StartSendTime <= DateTime.MinValue))
                {
                    EcmGlobals.GenerationSemaphore.ReleaseOne();
                }
            }
            return ProgressFeedback.Continue;
        }


    }

  

   
}
