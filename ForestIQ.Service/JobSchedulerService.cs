using ForestIQ.Domain.Interface;
using ForestIQ.Service.Jobs;
using Hangfire;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ForestIQ.Service
{
    public class JobSchedulerService : IJobSchedulerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobSchedulerService> _logger;
        private readonly IRecurringJobManager _recurringJobManager;

        public JobSchedulerService(IServiceProvider serviceProvider, ILogger<JobSchedulerService> logger, IRecurringJobManager recurringJobManager)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _recurringJobManager = recurringJobManager;
        }

        public void AddOrUpdateJob(string jobName, string cronExpression)
        {
            try
            {
                var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.Local };

                switch (jobName)
                {
                    case "PerformancePolling":
                        _recurringJobManager.AddOrUpdate<PerformancePollingJob>(
                            jobName,
                            job => job.ExecuteAsync(),
                            cronExpression,
                            options
                        );
                        break;
                    case "RefreshHistoryCleanup":
                        _recurringJobManager.AddOrUpdate<CleanupRefreshHistoryJob>(
                            jobName,
                            job => job.ExecuteAsync(),
                            cronExpression,
                            options
                        );
                        break;
                    default:
                        _logger.LogWarning($"Unknown job name: {jobName}");
                        break;
                }
                
                _logger.LogInformation($"Scheduled job {jobName} with cron {cronExpression}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to schedule job {jobName}");
            }
        }

        public void RemoveJob(string jobName)
        {
            _recurringJobManager.RemoveIfExists(jobName);
            _logger.LogInformation($"Removed job {jobName}");
        }

        public void SyncAllJobs()
        {
            using var scope = _serviceProvider.CreateScope();
            var jobConfigRepo = scope.ServiceProvider.GetRequiredService<IJobConfigurationRepository>();
            var allConfigs = jobConfigRepo.GetAllAsync().GetAwaiter().GetResult();

            foreach (var config in allConfigs)
            {
                if (config.IsEnabled)
                {
                    AddOrUpdateJob(config.JobName, config.CronExpression);
                }
                else
                {
                    RemoveJob(config.JobName);
                }
            }
        }
    }
}
