using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.EmailCampaign.Analytics.Model;
using Sitecore.ExM.Framework.Distributed.Tasks.TaskPools.ShortRunning;
using Sitecore.Configuration;
using Sitecore.ExM.Framework.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;
using Sitecore.Diagnostics;
using Sitecore.Modules.EmailCampaign.Core.Crypto;

namespace Sitecore.Support
{
    static class CustomValuesManager
    {
        static private readonly ILogger _logger = Factory.CreateObject("exmLogger", true) as Logger;
        static private readonly ShortRunningTaskPool _taskPool = Factory.CreateObject("exm/sentMessagesTaskPool", true) as DatabaseTaskPool;
        static private readonly IStringCipher cipher = Factory.CreateObject("exmAuthenticatedCipher", true) as AuthenticatedAesStringCipher;
        internal static void RegisterCustomValuesToTasks(ExmCustomValues customValues, Guid contactId)
        {
            string messageId = customValues.MessageId.ToString();
            ShortRunningTask task = new ShortRunningTask(null);
            task.Data.SetAs<string>("message_id", cipher.Encrypt(customValues.MessageId.ToString()));
            task.Data.SetAs<string>("instance_id", cipher.Encrypt(customValues.MessageId.ToString()));
            task.Data.SetAs<string>("contact_id", cipher.Encrypt(contactId.ToString()));
            task.Data.SetAs<SerializableCustomValues>("custom_values", customValues);
            ShortRunningTask[] tasks = new ShortRunningTask[] { task };
            _taskPool.AddTasks(tasks);
        }        
    }
}
