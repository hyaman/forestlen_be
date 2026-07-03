using ForestIQ.Domain;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public class JobConfigurationService : IJobConfigurationService
    {
        private readonly IJobConfigurationRepository _repository;
        private readonly IJobSchedulerService _schedulerService;

        public JobConfigurationService(IJobConfigurationRepository repository, IJobSchedulerService schedulerService)
        {
            _repository = repository;
            _schedulerService = schedulerService;
        }

        public async Task<IEnumerable<JobConfiguration>> GetAllJobsAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<JobConfiguration?> GetByJobNameAsync(string jobName)
        {
            return await _repository.GetByJobNameAsync(jobName);
        }

        public IEnumerable<AvailableJobDto> GetAvailableJobNames()
        {
            return new List<AvailableJobDto>
            {
                new AvailableJobDto { JobName = "PerformancePolling", JobDescription = "Polls domain controller performance" },
                new AvailableJobDto { JobName = "RefreshHistoryCleanup", JobDescription = "Cleans up old refresh history" }
            };
        }

        public async Task UpdateJobAsync(string jobName, string cronExpression, bool isEnabled, int? retentionDays)
        {
            if (!IsValidCron(cronExpression))
            {
                throw new ArgumentException("Invalid Cron expression format.", nameof(cronExpression));
            }

            if (retentionDays != null && retentionDays <= 0)
            {
                throw new ArgumentException("Invalid History Retention Days.", nameof(retentionDays));
            }

            var job = await _repository.GetByJobNameAsync(jobName);
            if (job == null)
            {
                var availableJobs = GetAvailableJobNames();
                if (!availableJobs.Any(j => j.JobName == jobName))
                {
                    throw new KeyNotFoundException($"Job name {jobName} is not a supported recurring job.");
                }

                job = new JobConfiguration
                {
                    JobName = jobName,
                    CronExpression = cronExpression,
                    IsEnabled = isEnabled,
                    RetentionDays = retentionDays,
                    CreatedDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Description = $"Configuration for {jobName}"
                };
                await _repository.AddAsync(job);
            }
            else
            {
                job.CronExpression = cronExpression;
                job.IsEnabled = isEnabled;
                job.RetentionDays = retentionDays;
                job.LastModified = DateTime.UtcNow;

                await _repository.UpdateAsync(job);
            }

            if (isEnabled)
            {
                _schedulerService.AddOrUpdateJob(jobName, cronExpression);
            }
            else
            {
                _schedulerService.RemoveJob(jobName);
            }
        }

        public async Task InitializeJobsAsync()
        {
            var defaultJobs = new List<JobConfiguration>
            {
                new JobConfiguration
                {
                    JobName = "PerformancePolling",
                    CronExpression = Runtime.BackgroundJobs.PerformancePollingCron,
                    IsEnabled = true,
                    Description = "Polls domain controller performance",
                    CreatedDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                },
                new JobConfiguration
                {
                    JobName = "RefreshHistoryCleanup",
                    CronExpression = Runtime.BackgroundJobs.RefreshHistoryCleanupCron,
                    IsEnabled = true,
                    RetentionDays = Runtime.BackgroundJobs.RefreshHistoryRetentionDays,
                    Description = "Cleans up old refresh history",
                    CreatedDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                }
            };

            foreach (var defaultJob in defaultJobs)
            {
                var existingJob = await _repository.GetByJobNameAsync(defaultJob.JobName);
                if (existingJob == null)
                {
                    await _repository.AddAsync(defaultJob);
                }
            }
        }

        private bool IsValidCron(string cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                return false;
            }

            // A basic check for standard 5-part cron or 6-part cron expressions
            var parts = cronExpression.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 5 || parts.Length == 6;
        }
    }
}
