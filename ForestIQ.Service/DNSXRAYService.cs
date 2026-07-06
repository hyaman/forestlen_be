using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.DNSXRAY;
using ForestIQ.Domain.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ForestIQ.Service
{
    public class DNSXRAYService : IDNSXRAYService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ILogger<DNSXRAYService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

        public DNSXRAYService(IPowerShellService powerShellService, ILogger<DNSXRAYService> logger, IMemoryCache memoryCache)
        {
            _powerShellService = powerShellService;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        private async Task<PowerShellExecutionResult> ExecuteScriptAsync(string scriptName, string variablesPrepended)
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "DNSXRAY", scriptName);

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"DNSXRAY script not found at path: {scriptPath}");
                    return new PowerShellExecutionResult { Success = false, Error = $"DNSXRAY script not found at path: {scriptPath}" };
                }

                var scriptContent = await File.ReadAllTextAsync(scriptPath);

                var helpersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "DNSXRAY", "DNSXRAY-Helpers.ps1");
                string helpersContent = string.Empty;
                if (File.Exists(helpersPath))
                {
                    helpersContent = await File.ReadAllTextAsync(helpersPath);
                }

                var finalScript = variablesPrepended + Environment.NewLine + helpersContent + Environment.NewLine + scriptContent;

                var request = new PowerShellScriptRequest { Script = finalScript };
                var result = await _powerShellService.ExecuteScriptAsync(request);

                if (result.Success && result.Data.HasValue)
                {
                    return result;
                }

                _logger.LogWarning($"DNSXRAY script {scriptName} executed but did not return successful data. Error: {result.Error}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing DNSXRAY script {scriptName}");
                return new PowerShellExecutionResult { Success = false, Error = $"Error executing DNSXRAY script {scriptName}: {ex.Message}" };
            }
        }

        private async Task<T?> ExecuteAndDeserializeAsync<T>(string scriptName, string variablesPrepended) where T : class
        {
            var result = await ExecuteScriptAsync(scriptName, variablesPrepended);
            if (!result.Success || !result.Data.HasValue) return null;

            var rawText = result.Data.Value.GetRawText();
            if (string.IsNullOrWhiteSpace(rawText) || rawText == "[]") return default;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
            {
                if (result.Data.Value.ValueKind != JsonValueKind.Array)
                {
                    // Convert single object to array format for deserialization
                    rawText = $"[{rawText}]";
                }
            }

            return JsonSerializer.Deserialize<T>(rawText, options);
        }

        public async Task<List<DnsServerDto>?> GetDnsServersAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_Servers_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<DnsServerDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$TargetDC = '{filter.TargetDc}'\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteAndDeserializeAsync<List<DnsServerDto>>("DnsServers.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<ZoneDto>?> GetZonesAsync(DnsXrayFilterRequest filter)
        {
            string dnsServer = filter.DnsServer ?? "All";
            string cacheKey = $"DNSXRAY_Zones_{filter.TargetDc}_{filter.Forest}_{filter.Domain}_{filter.Site}_{dnsServer}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<ZoneDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{dnsServer}'\n$TargetDC = '{filter.TargetDc}'\n$ForestFilter = '{filter.Forest}'\n$DomainFilter = '{filter.Domain}'\n$SiteFilter = '{filter.Site}'";
            var result = await ExecuteAndDeserializeAsync<List<ZoneDto>>("Zones.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<RecordDto>?> GetRecordsAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_Records_{filter.DnsServer}_{filter.ZoneName}_{filter.StaleRecordDays}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<RecordDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'\n$ZoneName = '{filter.ZoneName}'\n$StaleRecordDays = {filter.StaleRecordDays}";
            var result = await ExecuteAndDeserializeAsync<List<RecordDto>>("Records.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<RecordStatisticDto>?> GetRecordStatisticsAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_RecordStatistics_{filter.DnsServer}_{filter.ZoneName}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<RecordStatisticDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'\n$ZoneName = '{filter.ZoneName}'";
            var result = await ExecuteAndDeserializeAsync<List<RecordStatisticDto>>("RecordStatistics.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<DuplicateRecordDto>?> GetDuplicateRecordsAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_DuplicateRecords_{filter.DnsServer}_{filter.ZoneName}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<DuplicateRecordDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'\n$ZoneName = '{filter.ZoneName}'";
            var result = await ExecuteAndDeserializeAsync<List<DuplicateRecordDto>>("DuplicateRecords.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<IntegrityFindingDto>?> GetRecordIntegrityAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_RecordIntegrity_{filter.DnsServer}_{filter.ZoneName}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<IntegrityFindingDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'\n$ZoneName = '{filter.ZoneName}'";
            var result = await ExecuteAndDeserializeAsync<List<IntegrityFindingDto>>("RecordIntegrity.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<BestPracticeCheckDto>?> GetBestPracticesAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_BestPractices_{filter.DnsServer}_{filter.ZoneName}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<BestPracticeCheckDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'\n$ZoneName = '{filter.ZoneName}'";
            var result = await ExecuteAndDeserializeAsync<List<BestPracticeCheckDto>>("BestPractices.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<SoaComparisonDto>?> GetSoaInformationAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_SoaInformation_{filter.ZoneName}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<SoaComparisonDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$ZoneName = '{filter.ZoneName}'";
            var result = await ExecuteAndDeserializeAsync<List<SoaComparisonDto>>("SoaInformation.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<List<ResolutionTestDto>?> GetResolutionTestsAsync(DnsXrayFilterRequest filter)
        {
            string cacheKey = $"DNSXRAY_ResolutionTests_{filter.DnsServer}";

            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out List<ResolutionTestDto>? cachedData))
            {
                return cachedData;
            }

            var vars = $"$DnsServer = '{filter.DnsServer}'";
            var result = await ExecuteAndDeserializeAsync<List<ResolutionTestDto>>("ResolutionTests.ps1", vars);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }
    }
}
