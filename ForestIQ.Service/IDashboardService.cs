using ForestIQ.Domain.Models.Dashboard;
using ForestIQ.Domain.DTO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public interface IDashboardService
    {
        Task<DcInventoryResponseModel?> GetDcInventoryAsync(DashboardFilterRequest filter);
        Task<List<DcLogonSessionModel>?> GetDcLogonSessionsAsync(DashboardFilterRequest filter);
        Task<List<DcAuthSummaryModel>?> GetDcAuthSummaryAsync(DashboardFilterRequest filter);
        Task<List<DcNtdsHealthModel>?> GetDcNtdsHealthAsync(DashboardFilterRequest filter);
        Task<DcPerformanceResponseModel?> GetDcPerformanceAsync(DashboardFilterRequest filter);
        Task<List<DcHierarchyRawModel>?> GetDcHierarchyAsync(string domainFilter = "All", string siteFilter = "All", bool refreshView = false);
        Task<DefaultDcModel?> GetDefaultDcAsync(string targetDomain = "", bool refreshView = false);
    }
}
