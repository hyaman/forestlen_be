using ForestIQ.Domain;
using ForestIQ.Domain.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ForestIQ.Service.Jobs
{
    public class CleanupRefreshHistoryJob
    {
        private readonly IRefreshHistoryRepository _repository;
        private readonly ILogger<CleanupRefreshHistoryJob> _logger;

        public CleanupRefreshHistoryJob(IRefreshHistoryRepository repository, ILogger<CleanupRefreshHistoryJob> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                var retentionDays = Runtime.BackgroundJobs.RefreshHistoryRetentionDays;
                var thresholdDate = DateTime.UtcNow.AddDays(-retentionDays);
                _logger.LogInformation($"Starting cleanup of Refresh History older than {thresholdDate} ({retentionDays} days)");
                await _repository.DeleteOlderThanAsync(thresholdDate);
                _logger.LogInformation("Successfully cleaned up old Refresh History records.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up old Refresh History records.");
            }
        }
    }
}
