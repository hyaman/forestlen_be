using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public interface IDashboardService
    {
        Task<JsonElement?> GetDcInventoryAsync(string targetDc);
        Task<JsonElement?> GetDcLogonSessionsAsync(string targetDc);
        Task<JsonElement?> GetDcAuthSummaryAsync(string targetDc, int lookBackHours = 24);
        Task<JsonElement?> GetDcNtdsHealthAsync(string targetDc);
    }
}
