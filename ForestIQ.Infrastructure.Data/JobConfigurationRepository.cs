using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Infrastructure.Data
{
    public class JobConfigurationRepository : IJobConfigurationRepository
    {
        private readonly ForestIqDbContext _dbContext;

        public JobConfigurationRepository(ForestIqDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<JobConfiguration>> GetAllAsync()
        {
            return await _dbContext.JobConfigurations.ToListAsync();
        }

        public async Task<JobConfiguration?> GetByJobNameAsync(string jobName)
        {
            return await _dbContext.JobConfigurations
                .FirstOrDefaultAsync(x => x.JobName == jobName);
        }

        public async Task UpdateAsync(JobConfiguration jobConfiguration)
        {
            _dbContext.JobConfigurations.Update(jobConfiguration);
            await _dbContext.SaveChangesAsync();
        }

        public async Task AddAsync(JobConfiguration jobConfiguration)
        {
            await _dbContext.JobConfigurations.AddAsync(jobConfiguration);
            await _dbContext.SaveChangesAsync();
        }
    }
}
