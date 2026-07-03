using ForestIQ.Domain.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IJobConfigurationRepository
    {
        Task<IEnumerable<JobConfiguration>> GetAllAsync();
        Task<JobConfiguration?> GetByJobNameAsync(string jobName);
        Task UpdateAsync(JobConfiguration jobConfiguration);
        Task AddAsync(JobConfiguration jobConfiguration);
    }
}
