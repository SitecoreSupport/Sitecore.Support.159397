using Sitecore.Analytics.Tracking.External;
using Sitecore.Diagnostics;
using Sitecore.EmailCampaign.Analytics.Model;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.Modules.EmailCampaign.Core.Pipelines.HandleMessageEventBase;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Factories;
using Sitecore.Modules.EmailCampaign.Messages;
using System;

namespace Sitecore.Support.Modules.EmailCampaign.Core.Pipelines.HandleMessageEventBase
{
    public class SetCustomValues
    {
        // Fields
        private readonly EcmFactory _factory;
        private readonly ILogger _logger;

        // Methods
        public SetCustomValues(ILogger logger) : this(EcmFactory.GetDefaultFactory(), logger)
        {
        }

        internal SetCustomValues(EcmFactory factory, ILogger logger)
        {
            Assert.ArgumentNotNull(factory, "factory");
            Assert.ArgumentNotNull(logger, "logger");
            this._factory = factory;
            this._logger = logger;
        }

        public void Process(HandleMessageEventPipelineArgs args)
        {
            ExmCustomValues customValues;
            string str;
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentCondition(args.MessageItem != null, "args", "MessageItem not set");
            Assert.ArgumentCondition(args.TouchPointRecord != null, "args", "TouchPointRecord not set");
            MessageItem messageItem = args.MessageItem;
            TouchPointRecord touchPointRecord = args.TouchPointRecord;
            try
            {
                customValues = args.EventData.GetAs<SerializableCustomValues>("custom_values", null);
            }
            catch (Exception exception)
            {
                this._logger.LogError("Failed to retrieve custom EXM values", exception);
                throw;
            }
            {
            if (customValues == null)
                throw new MessageEventPipelineException("Custom values not found for " + args);
            }
            if (ExmCustomValuesHolder.ContainsCustomValuesHolderKey(touchPointRecord.CustomValues, out str))
            {
                throw new Sitecore.Modules.EmailCampaign.Exceptions.MessageEventPipelineException("Touch point already contains customvalues for " + args);
            }
            ExmCustomValuesHolder holder = new ExmCustomValuesHolder
            {
                ExmCustomValues = { {
                1,
                customValues
            } }
            };
            touchPointRecord.CustomValues["ScExmHolder"] = holder;
        }
    }
}
