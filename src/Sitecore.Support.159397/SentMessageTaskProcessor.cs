using Sitecore.Data;
using Sitecore.EmailCampaign.Analytics.Model;
using Sitecore.EmailCampaign.Cm.Pipelines.HandleSentMessage;
using Sitecore.ExM.Framework.Data;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.ExM.Framework.Distributed.Tasks.TaskPools.ShortRunning;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Core.Crypto;
using Sitecore.Modules.EmailCampaign.Core.Pipelines.HandleMessageEventBase;

namespace Sitecore.Support.EmailCampaign.Cm.Tasks
{
    class SentMessageTaskProcessor : Sitecore.EmailCampaign.Cm.Tasks.SentMessageTaskProcessor
    {
        public SentMessageTaskProcessor(IStringCipher cipher, ILogger logger) : base(cipher, logger)
        {
        }
        public SentMessageTaskProcessor(IStringCipher cipher, ILogger logger, PipelineHelper pipelineHelper) : base(cipher, logger, pipelineHelper)
        {
        }
        protected override EventData ExtractEventData(ShortRunningTask task)
        {
            SerializationCollection serializationCollection = new SerializationCollection();
            SerializableCustomValues customValues = task.Data.GetAs<SerializableCustomValues>("custom_values", null);
            serializationCollection.SetAs<SerializableCustomValues>("custom_values", customValues);
            return new EventData(task.Data.GetAs<string>("contact_id", null), task.Data.GetAs<string>("message_id", null), task.Data.GetAs<string>("instance_id", null), serializationCollection);
        }


        protected override void RunPipeline(ID contactId, ID messageId, ID instanceId, SerializationCollection eventData)
        {
            eventData.Set("MessageId", messageId);
            HandleSentMessagePipelineArgs args = new HandleSentMessagePipelineArgs(contactId, messageId, instanceId, eventData);
            base.PipelineHelper.RunPipeline("handleSentMessage", args);
        }

    }
}
