using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;

namespace ForestIQ.Service
{
    public class DashboardService : IDashboardService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(IPowerShellService powerShellService, ILogger<DashboardService> logger)
        {
            _powerShellService = powerShellService;
            _logger = logger;
        }

        private async Task<JsonElement?> ExecuteDashboardScriptAsync(string scriptName, string variablesPrepended)
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "Dashboard", scriptName);
                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Dashboard script not found at path: {scriptPath}");
                    return null;
                }

                var scriptContent = await File.ReadAllTextAsync(scriptPath);
                var finalScript = variablesPrepended + Environment.NewLine + scriptContent;

                var request = new PowerShellScriptRequest { Script = finalScript };
                var result = await _powerShellService.ExecuteScriptAsync(request);

                if (result.Success && result.Data.HasValue)
                {
                    return result.Data;
                }

                _logger.LogWarning($"Dashboard script {scriptName} executed but did not return successful data. Error: {result.Error}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing dashboard script {scriptName}");
                return null;
            }
        }

        public async Task<JsonElement?> GetDcInventoryAsync(string targetDc)
        {
            var vars = $"$TargetDC = '{targetDc}'";
            return await ExecuteDashboardScriptAsync("Get-DcInventory.ps1", vars);
        }

        public async Task<JsonElement?> GetDcLogonSessionsAsync(string targetDc)
        {
            var vars = $"$TargetDC = '{targetDc}'";
            return await ExecuteDashboardScriptAsync("Get-DcLogonSessions.ps1", vars);
        }

        public async Task<JsonElement?> GetDcAuthSummaryAsync(string targetDc, int lookBackHours = 24)
        {
            var vars = $"$TargetDC = '{targetDc}'\n$AuthLookBackHours = {lookBackHours}";
            return await ExecuteDashboardScriptAsync("Get-DcAuthSummary.ps1", vars);
        }

        public async Task<JsonElement?> GetDcNtdsHealthAsync(string targetDc)
        {
            var vars = $"$TargetDC = '{targetDc}'";
            return await ExecuteDashboardScriptAsync("Get-DcNtdsHealth.ps1", vars);
        }
    }
}
