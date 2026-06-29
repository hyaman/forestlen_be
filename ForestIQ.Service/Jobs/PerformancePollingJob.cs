using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using ForestIQ.Domain.Models.Dashboard;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Service.Jobs
{
    public class PerformancePollingJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PerformancePollingJob> _logger;

        public PerformancePollingJob(IServiceProvider serviceProvider, ILogger<PerformancePollingJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 900)]
        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Starting Performance Polling Job...");

            // Use a scope since this is a background job
            using var scope = _serviceProvider.CreateScope();
            var configureRepository = scope.ServiceProvider.GetRequiredService<IConfigureRepository>();
            var powerShellService = scope.ServiceProvider.GetRequiredService<IPowerShellService>();
            var historyRepository = scope.ServiceProvider.GetRequiredService<IPerformanceHistoryRepository>();

            var config = await configureRepository.GetByForestNameAsync();

            if (config != null && !string.IsNullOrEmpty(config.RemoteHost))
            {
                try
                {
                    var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "Dashboard", "Get-DcPerformance.ps1");
                    if (!System.IO.File.Exists(scriptPath))
                    {
                        _logger.LogError("Get-DcPerformance.ps1 not found");
                        return;
                    }

                    var scriptContent = await System.IO.File.ReadAllTextAsync(scriptPath);
                    var vars = $"$TargetDC = 'All'";
                    var finalScript = vars + Environment.NewLine + scriptContent;

                    var result = await powerShellService.ExecuteBackgroundScriptAsync(config, finalScript);
                    
                    if (!result.Success || !result.Data.HasValue)
                    {
                        return;
                    }

                    var jsonResult = result.Data.Value.GetRawText();
                    if (string.IsNullOrWhiteSpace(jsonResult))
                    {
                        return;
                    }

                    // Deserialize single result or array
                    var doc = JsonDocument.Parse(jsonResult);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        foreach(var element in doc.RootElement.EnumerateArray())
                        {
                            await ProcessAndSaveElement(element, historyRepository);
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        await ProcessAndSaveElement(doc.RootElement, historyRepository);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Performance Polling Job.");
                    // We intentionally swallow the exception so Hangfire marks the job as Succeeded
                    // and doesn't aggressively retry it. It will run again at the next 15-minute interval.
                }
            }
            
            _logger.LogInformation("Performance Polling Job Completed.");
        }

        private async Task ProcessAndSaveElement(JsonElement element, IPerformanceHistoryRepository historyRepository)
        {
            try
            {
                var serverName = element.GetProperty("ServerName").GetString();
                if (string.IsNullOrEmpty(serverName)) return;

                var cpuLoad = element.GetProperty("CpuLoad").GetDouble();
                var memoryUsage = element.GetProperty("MemoryUsage").GetDouble();
                
                // NetworkIo might not exist if the script fails to get it
                double networkIo = 0;
                if (element.TryGetProperty("NetworkIo", out var netProp))
                {
                    networkIo = netProp.GetDouble();
                }

                var entry = new DcPerformanceHistoryEntry
                {
                    ServerName = serverName,
                    Timestamp = DateTime.Now,
                    CpuLoad = cpuLoad,
                    MemoryUsage = memoryUsage,
                    NetworkIo = networkIo
                };

                await historyRepository.SaveEntryAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse performance JSON result");
            }
        }
    }
}
