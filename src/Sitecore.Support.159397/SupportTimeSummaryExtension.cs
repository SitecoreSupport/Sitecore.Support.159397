using Sitecore.Modules.EmailCampaign.Core.Dispatch;
using Sitecore.Support.Modules.EmailCampaign.Core.Dispatch;
using System;
using System.Reflection;

namespace Sitecore.Support
{
    static class SupportExtenssion
    {
        internal static void AddTimeDuration(this TimeSummary timeSummary, TimeFlag phase, DateTime startTime, DateTime endTime)
        {
            Type timeFlagType = typeof(IDispatchManager).Assembly.GetType("Sitecore.Modules.EmailCampaign.Core.Dispatch.TimeFlag");
            var method = typeof(TimeSummary).GetMethod("AddTimeDuration", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { timeFlagType, typeof(DateTime), typeof(DateTime) }, null);
            method.Invoke(timeSummary, new object[] { phase, startTime, endTime });
        }

        internal static void AddTimeDuration(this TimeSummary timeSummary, TimeFlag phase, TimeSpan duration)
        {
            Type timeFlagType = typeof(IDispatchManager).Assembly.GetType("Sitecore.Modules.EmailCampaign.Core.Dispatch.TimeFlag");
            var method = typeof(TimeSummary).GetMethod("AddTimeDuration", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { timeFlagType, typeof(TimeSpan) }, null);
            method.Invoke(timeSummary, new object[] { phase, duration });
        }

        internal static void GetNextCpuValue(this TimeSummary timeSummary)
        {
            var method = typeof(TimeSummary).GetMethod("GetNextCpuValue", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(timeSummary, new object[] { });
        }
    }
}
