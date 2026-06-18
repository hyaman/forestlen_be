using ForestIQ.Domain.DTO;

namespace ForestIQ.Domain.Interface
{
    public interface IConfigureRepository
    {
        Task<long> UpsertAsync(AdConfiguration configuration);
        Task<AdConfiguration?> GetByForestNameAsync();
        Task<bool> DeleteAsync();
    }
}
