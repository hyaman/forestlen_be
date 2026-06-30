using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ForestIQ.Domain;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using ForestIQ.Domain.Models.Dashboard;

namespace ForestIQ.Service
{
    public class DashboardService : IDashboardService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ILogger<DashboardService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPerformanceHistoryRepository _historyRepository;
        private readonly IRefreshHistoryService _refreshHistoryService;
        public DashboardService(
            IPowerShellService powerShellService, 
            ILogger<DashboardService> logger, 
            IMemoryCache cache,
            IPerformanceHistoryRepository historyRepository,
            IRefreshHistoryService refreshHistoryService)
        {
            _powerShellService = powerShellService;
            _logger = logger;
            _cache = cache;
            _historyRepository = historyRepository;
            _refreshHistoryService = refreshHistoryService;
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

        public async Task<DcInventoryResponseModel?> GetDcInventoryAsync(DashboardFilterRequest filter)
        {
            string cacheKey = $"Dashboard_Inventory_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}_{filter.Health}";
            if (!filter.RefreshView && _cache.TryGetValue(cacheKey, out DcInventoryResponseModel? cachedResult))
            {
                return cachedResult;
            }

            string allCacheKey = "Dashboard_Inventory_All_All_All_All_All";

            if (!filter.RefreshView && _cache.TryGetValue(allCacheKey, out DcInventoryResponseModel? allCachedResult))
            {
                var filtered = allCachedResult.InventoryResults?.AsEnumerable() ?? Enumerable.Empty<DcInventoryModel>();
                
                if (filter.TargetDc != "All" && !string.IsNullOrEmpty(filter.TargetDc))
                    filtered = filtered.Where(x => x.Inventory?.ServerName?.Equals(filter.TargetDc, StringComparison.OrdinalIgnoreCase) == true || x.Inventory?.FQDN?.Equals(filter.TargetDc, StringComparison.OrdinalIgnoreCase) == true);
                
                if (filter.Domain != "All" && !string.IsNullOrEmpty(filter.Domain))
                    filtered = filtered.Where(x => x.Inventory?.FQDN?.EndsWith(filter.Domain, StringComparison.OrdinalIgnoreCase) == true);
                    
                if (filter.Site != "All" && !string.IsNullOrEmpty(filter.Site))
                    filtered = filtered.Where(x => x.Inventory?.Site?.Equals(filter.Site, StringComparison.OrdinalIgnoreCase) == true);
                    
                if (filter.Health != "All" && !string.IsNullOrEmpty(filter.Health))
                    filtered = filtered.Where(x => (x.Inventory?.OverallStatus ?? "Unknown").Equals(filter.Health, StringComparison.OrdinalIgnoreCase));

                var finalResult = new DcInventoryResponseModel
                {
                    GeneratedAt = allCachedResult.GeneratedAt,
                    InventoryResults = filtered.ToList()
                };
                
                _cache.Set(cacheKey, finalResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
                return finalResult;
            }

            if (!filter.RefreshView)
            {
                await _refreshHistoryService.AddRefreshHistoryAsync(SectionName.DeepDcDiscovery, null);
            }


            var vars = $"$TargetDC = '{filter.TargetDc}'\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcInventory.ps1", vars);
            if (!result.HasValue) return null;
            
            var mappedResult = JsonSerializer.Deserialize<DcInventoryResponseModel>(result.Value.GetRawText());

            if (mappedResult?.InventoryResults != null && filter.Health != "All" && !string.IsNullOrEmpty(filter.Health))
            {
                mappedResult.InventoryResults = mappedResult.InventoryResults.Where(x => (x.Inventory?.OverallStatus ?? "Unknown").Equals(filter.Health, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (mappedResult != null)
            {
                _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            }
            return mappedResult;
        }

        public async Task<List<DcLogonSessionModel>?> GetDcLogonSessionsAsync(DashboardFilterRequest filter)
        {
            string cacheKey = $"Dashboard_LogonSessions_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}_{filter.Health}";
            
            if (!filter.RefreshView && _cache.TryGetValue(cacheKey, out List<DcLogonSessionModel>? cachedResult))
            {
                return cachedResult;
            }

            string allCacheKey = "Dashboard_LogonSessions_All_All_All_All_All";
            
            if (!filter.RefreshView && _cache.TryGetValue(allCacheKey, out List<DcLogonSessionModel>? allCachedResult))
            {
                var filtered = allCachedResult.AsEnumerable();
                
                var inventory = await GetDcInventoryAsync(new DashboardFilterRequest 
                { 
                    TargetDc = filter.TargetDc, Forest = filter.Forest, Domain = filter.Domain, Site = filter.Site, Health = filter.Health 
                });
                
                if (inventory?.InventoryResults != null)
                {
                    var validHosts = inventory.InventoryResults.Select(i => i.Inventory?.ServerName)
                                              .Where(s => !string.IsNullOrEmpty(s))
                                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    filtered = filtered.Where(x => x.ServerName != null && validHosts.Contains(x.ServerName));
                }

                var finalResult = filtered.ToList();
                _cache.Set(cacheKey, finalResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
                return finalResult;
            }

            var vars = $"$TargetDC = '{filter.TargetDc}'\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcLogonSessions.ps1", vars);
            
            if (!result.HasValue) return null;

            var mappedResult = result.Value.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<DcLogonSessionModel>>(result.Value.GetRawText()) 
                : new List<DcLogonSessionModel> { JsonSerializer.Deserialize<DcLogonSessionModel>(result.Value.GetRawText())! };

            if (mappedResult != null && filter.Health != "All" && !string.IsNullOrEmpty(filter.Health))
            {
                var inventory = await GetDcInventoryAsync(new DashboardFilterRequest { TargetDc = filter.TargetDc, Forest = filter.Forest, Domain = filter.Domain, Site = filter.Site });
                if (inventory?.InventoryResults != null)
                {
                    var validHosts = inventory.InventoryResults.Where(i => (i.Inventory?.OverallStatus ?? "Unknown").Equals(filter.Health, StringComparison.OrdinalIgnoreCase))
                                              .Select(i => i.Inventory?.ServerName)
                                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    mappedResult = mappedResult.Where(x => validHosts.Contains(x.ServerName)).ToList();
                }
            }

            _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            return mappedResult;
        }

        public async Task<List<DcAuthSummaryModel>?> GetDcAuthSummaryAsync(DashboardFilterRequest filter)
        {
            string cacheKey = $"Dashboard_AuthSummary_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}_{filter.Health}_{filter.LookBackHours}";
            
            if (!filter.RefreshView && _cache.TryGetValue(cacheKey, out List<DcAuthSummaryModel>? cachedResult))
            {
                return cachedResult;
            }

            string allCacheKey = $"Dashboard_AuthSummary_All_All_All_All_All_{filter.LookBackHours}";

            if (!filter.RefreshView && _cache.TryGetValue(allCacheKey, out List<DcAuthSummaryModel>? allCachedResult))
            {
                var filtered = allCachedResult.AsEnumerable();
                
                var inventory = await GetDcInventoryAsync(new DashboardFilterRequest 
                { 
                    TargetDc = filter.TargetDc, Forest = filter.Forest, Domain = filter.Domain, Site = filter.Site, Health = filter.Health 
                });
                
                if (inventory?.InventoryResults != null)
                {
                    var validHosts = inventory.InventoryResults.Select(i => i.Inventory?.ServerName)
                                              .Where(s => !string.IsNullOrEmpty(s))
                                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    filtered = filtered.Where(x => x.ServerName != null && validHosts.Contains(x.ServerName));
                }

                var finalResult = filtered.ToList();
                _cache.Set(cacheKey, finalResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
                return finalResult;
            }

            var vars = $"$TargetDC = '{filter.TargetDc}'\n$AuthLookBackHours = {filter.LookBackHours}\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcAuthSummary.ps1", vars);
            if (!result.HasValue) return null;

            var mappedResult = result.Value.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<List<DcAuthSummaryModel>>(result.Value.GetRawText()) : new List<DcAuthSummaryModel> { JsonSerializer.Deserialize<DcAuthSummaryModel>(result.Value.GetRawText())! };

            if (mappedResult != null && filter.Health != "All" && !string.IsNullOrEmpty(filter.Health))
            {
                var inventory = await GetDcInventoryAsync(new DashboardFilterRequest { TargetDc = filter.TargetDc, Forest = filter.Forest, Domain = filter.Domain, Site = filter.Site });
                
                if (inventory?.InventoryResults != null)
                {
                    var validHosts = inventory.InventoryResults.Where(i => (i.Inventory?.OverallStatus ?? "Unknown").Equals(filter.Health, StringComparison.OrdinalIgnoreCase))
                                              .Select(i => i.Inventory?.ServerName)
                                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    mappedResult = mappedResult.Where(x => validHosts.Contains(x.ServerName)).ToList();
                }
            }

            _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            return mappedResult;
        }

        public async Task<List<DcNtdsHealthModel>?> GetDcNtdsHealthAsync(DashboardFilterRequest filter)
        {
            string cacheKey = $"Dashboard_NtdsHealth_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}_{filter.Health}";
            if (!filter.RefreshView && _cache.TryGetValue(cacheKey, out List<DcNtdsHealthModel>? cachedResult))
            {
                return cachedResult;
            }

            string allCacheKey = "Dashboard_NtdsHealth_All_All_All_All_All";
            if (!filter.RefreshView && _cache.TryGetValue(allCacheKey, out List<DcNtdsHealthModel>? allCachedResult))
            {
                var filtered = allCachedResult.AsEnumerable();
                
                var inventory = await GetDcInventoryAsync(new DashboardFilterRequest 
                { 
                    TargetDc = filter.TargetDc, Forest = filter.Forest, Domain = filter.Domain, Site = filter.Site, Health = filter.Health 
                });
                
                if (inventory?.InventoryResults != null)
                {
                    var validHosts = inventory.InventoryResults.Select(i => i.Inventory?.ServerName)
                                              .Where(s => !string.IsNullOrEmpty(s))
                                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    filtered = filtered.Where(x => x.ServerName != null && validHosts.Contains(x.ServerName));
                }

                var finalResult = filtered.ToList();
                _cache.Set(cacheKey, finalResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
                return finalResult;
            }

            var vars = $"$TargetDC = '{filter.TargetDc}'\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcNtdsHealth.ps1", vars);
            if (!result.HasValue) return null;

            var mappedResult = result.Value.ValueKind == JsonValueKind.Array 
                ? JsonSerializer.Deserialize<List<DcNtdsHealthModel>>(result.Value.GetRawText())
                : new List<DcNtdsHealthModel> { JsonSerializer.Deserialize<DcNtdsHealthModel>(result.Value.GetRawText())! };

            if (mappedResult != null && filter.Health != "All" && !string.IsNullOrEmpty(filter.Health))
            {
                mappedResult = mappedResult.Where(x => (x.Health ?? "Unknown").Equals(filter.Health, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            return mappedResult;
        }

        public async Task<List<DcPerformanceResponseModel>?> GetDcPerformanceAsync(DashboardFilterRequest filter)
        {
            string cacheKey = $"Dashboard_Performance_{filter.TargetDc}";
            
            // Note: we might not want to heavily cache live performance stats, but we will for a few seconds if requested.
            // But we'll follow the pattern and just query the script directly for now.
            var vars = $"$TargetDC = '{filter.TargetDc}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcPerformance.ps1", vars);
            
            if (!result.HasValue) return null;

            var rawText = result.Value.GetRawText();
            var responses = new List<DcPerformanceResponseModel>();

            using var doc = JsonDocument.Parse(rawText);
            var root = doc.RootElement;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var liveStats = JsonSerializer.Deserialize<DcPerformanceLiveModel>(item.GetRawText(), options);
                    var serverName = item.TryGetProperty("ServerName", out var sn) ? sn.GetString() : filter.TargetDc;
                    var history = await _historyRepository.GetHistoryAsync(serverName ?? "");

                    responses.Add(new DcPerformanceResponseModel
                    {
                        ServerName = serverName,
                        LiveStats = liveStats,
                        History = history
                    });
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var liveStats = JsonSerializer.Deserialize<DcPerformanceLiveModel>(rawText, options);
                var serverName = root.TryGetProperty("ServerName", out var sn) ? sn.GetString() : filter.TargetDc;
                var history = await _historyRepository.GetHistoryAsync(serverName ?? "");

                responses.Add(new DcPerformanceResponseModel
                {
                    ServerName = serverName,
                    LiveStats = liveStats,
                    History = history
                });
            }

            return responses;
        }

        public async Task<List<DcHierarchyRawModel>?> GetDcHierarchyAsync(string domainFilter = "All", string siteFilter = "All", bool refreshView = false)
        {
            string cacheKey = $"Dashboard_Hierarchy_{domainFilter}_{siteFilter}";
            if (!refreshView && _cache.TryGetValue(cacheKey, out List<DcHierarchyRawModel>? cachedResult))
            {
                return cachedResult;
            }

            var vars = $"$DomainFilter = '{domainFilter}'\n$SiteFilter = '{siteFilter}'";
            var result = await ExecuteDashboardScriptAsync("Get-DcHierarchy.ps1", vars);
            if (!result.HasValue) return null;

            var mappedResult = result.Value.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<List<DcHierarchyRawModel>>(result.Value.GetRawText()) : new List<DcHierarchyRawModel> { JsonSerializer.Deserialize<DcHierarchyRawModel>(result.Value.GetRawText())! };

            _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            return mappedResult;
        }

        public async Task<DefaultDcModel?> GetDefaultDcAsync(string targetDomain = "", bool refreshView = false)
        {
            string cacheKey = $"Dashboard_DefaultDc_{targetDomain}";
            if (!refreshView && _cache.TryGetValue(cacheKey, out DefaultDcModel? cachedResult))
            {
                return cachedResult;
            }

            var vars = $"$TargetDomain = '{targetDomain}'";
            var result = await ExecuteDashboardScriptAsync("Get-DefaultDc.ps1", vars);
            if (!result.HasValue) return null;

            var mappedResult = JsonSerializer.Deserialize<DefaultDcModel>(result.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _cache.Set(cacheKey, mappedResult, TimeSpan.FromMinutes(Runtime.Cache.DashboardCacheMinutes));
            return mappedResult;
        }
    }
}
