using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Models.Dashboard;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IPerformanceHistoryRepository
    {
        Task SaveEntryAsync(DcPerformanceHistoryEntry entry);
        Task<List<DcPerformanceHistoryModel>> GetHistoryAsync(string serverName);
    }
}
