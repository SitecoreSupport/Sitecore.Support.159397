using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sitecore.Support.Modules.EmailCampaign.Core.Dispatch
{    internal enum TimeFlag
    {
        AutomationStateWait,
        AutomationStateFetch,
        TaskWorkerWait,
        LoadUser,
        WorkerThreadWait,
        Generate,
        CollectFiles,
        GenerateMime,
        InsertFiles,
        ReplaceTokens,
        GetPage,
        SendMailWait,
        Send,
        Process
    }
}
