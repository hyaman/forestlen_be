using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace ForestIQ.Infrastructure.Data
{
    public class ConfigureRepository : IConfigureRepository
    {
        private readonly ForestIqDbContext _dbContext;

        public ConfigureRepository(ForestIqDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<long> UpsertAsync(AdConfiguration configuration)
        {
            var existing = await _dbContext.AdConfigurations
                .FirstOrDefaultAsync(c => c.ForestName == configuration.ForestName);

            if (existing != null)
            {
                existing.UserName = configuration.UserName;
                existing.EncryptedPassword = configuration.EncryptedPassword;
                existing.DnsServersJson = configuration.DnsServersJson;
                existing.UpdatedAtUtc = configuration.UpdatedAtUtc;
                _dbContext.AdConfigurations.Update(existing);
            }
            else
            {
                await _dbContext.AdConfigurations.AddAsync(configuration);
            }

            await _dbContext.SaveChangesAsync();

            return existing?.Id ?? configuration.Id;
        }

        public async Task<AdConfiguration?> GetByForestNameAsync()
        {
            return await _dbContext.AdConfigurations
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteAsync()
        {
            var existing = await _dbContext.AdConfigurations
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                _dbContext.AdConfigurations.Remove(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }

            return false;
        }
    }
}
