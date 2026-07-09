using ForestIQ.Domain.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IJobConfigurationService
    {
        Task<IEnumerable<JobConfiguration>> GetAllJobsAsync();
        Task<JobConfiguration?> GetByJobNameAsync(string jobName);
        Task UpdateJobAsync(string jobName, string cronExpression, bool isEnabled, int? retentionDays);
        Task InitializeJobsAsync();
        IEnumerable<AvailableJobDto> GetAvailableJobNames();
    }
}
