using ForestIQ.Domain;
using ForestIQ.Service.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using System;

namespace ForestIQ.Extensions
{
    public static class HangfireExtensions
    {
        public static void ScheduleRecurringJobs(this IApplicationBuilder app)
        {
            RecurringJob.AddOrUpdate<PerformancePollingJob>(
                "poll-performance", 
                job => job.ExecuteAsync(), 
                Runtime.BackgroundJobs.PerformancePollingCron, 
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Local }
            );

            RecurringJob.AddOrUpdate<CleanupRefreshHistoryJob>(
                "cleanup-refresh-history",
                job => job.ExecuteAsync(),
                Runtime.BackgroundJobs.RefreshHistoryCleanupCron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Local }
            );
        }
    }
}
