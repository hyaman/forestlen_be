using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ForestIQ.Domain.Interface
{
    public interface IRefreshHistoryService
    {
        Task AddRefreshHistoryAsync(AddRefreshHistoryRequest request);
        Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName);
        Task<RefreshHistory?> GetLatestAsync(int HistoryId, Guid? DiscoveryId, string? DcName);
    }
}
