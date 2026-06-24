using ForestIQ.Domain.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IPowerShellService
    {
        Task<PowerShellExecutionResult> ConnectAsync(PowerShellRequest request);

        Task<GraphResponse?> GetAdDiagnosticsAsync(bool refreshView = false, string? domain = null, string? site = null);
        
        Task<GraphResponse?> GetAdTopologyAsync(bool refreshView = false, string? domain = null, string? site = null);
        Task<GraphResponse?> GetAdReplicationAsync(bool refreshView = false, string? domain = null, string? site = null);
        Task<GraphResponse?> GetAdDiagnosticsProgressiveAsync(bool refreshView = false, string? domain = null, string? site = null);

        Task<PowerShellExecutionResult> ExecuteCommandAsync(PowerShellExecuteRequest request);

        Task<PowerShellExecutionResult> ExecuteScriptAsync(PowerShellScriptRequest request);

        IEnumerable<PowerShellCommandDefinition> GetAvailableCommands();

        Task<DomainDiscoveryResponse> DiscoverHostsAsync(PowerShellRequest request);

        Task<List<string>> GetAdDomainsAsync(bool refreshView = false);
        Task<List<string>> GetAdSitesAsync(bool refreshView = false);

        void ClearCache();
    }
}
