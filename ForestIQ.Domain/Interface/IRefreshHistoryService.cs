using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IRefreshHistoryService
    {
        Task AddRefreshHistoryAsync(SectionName sectionName, string? triggeredBy);
        Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName);
        Task<RefreshHistory?> GetLatestAsync(SectionName sectionName);
    }
}
