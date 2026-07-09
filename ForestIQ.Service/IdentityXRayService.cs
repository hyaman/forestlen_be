using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.IdentityXRay;
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
    public class IdentityXRayService : IIdentityXRayService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ILogger<IdentityXRayService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

        public IdentityXRayService(IPowerShellService powerShellService, ILogger<IdentityXRayService> logger, IMemoryCache memoryCache)
        {
            _powerShellService = powerShellService;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        private async Task<PowerShellExecutionResult> ExecuteScriptAsync(string scriptName, string variablesPrepended)
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "IdentityXRay", scriptName);

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"IdentityXRay script not found at path: {scriptPath}");
                    return new PowerShellExecutionResult { Success = false, Error = $"IdentityXRay script not found at path: {scriptPath}" };
                }

                var scriptContent = await File.ReadAllTextAsync(scriptPath);

                var helpersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "IdentityXRay", "IdentityXRay-Helpers.ps1");
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

                _logger.LogWarning($"IdentityXRay script {scriptName} executed but did not return successful data. Error: {result.Error}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing IdentityXRay script {scriptName}");
                return new PowerShellExecutionResult { Success = false, Error = $"Error executing IdentityXRay script {scriptName}: {ex.Message}" };
            }
        }

        private async Task<T?> ExecuteAndDeserializeAsync<T>(string scriptName, string variablesPrepended) where T : class
        {
            var result = await ExecuteScriptAsync(scriptName, variablesPrepended);
            if (!result.Success) return null;

            if (!result.Data.HasValue || result.Data.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var rawText = result.Data.Value.GetRawText();
            if (string.IsNullOrWhiteSpace(rawText)) return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(rawText, options);
        }

        private string BuildPrependedVariables(IdentityXRayFilterRequest filter)
        {
            return $@"
$Domain = '{filter.Domain}'
$InactiveDays = {filter.InactiveDays}
$PasswordAgeWarningDays = {filter.PasswordAgeWarningDays}
$PasswordAgeCriticalDays = {filter.PasswordAgeCriticalDays}
$ComputerPasswordAgeWarningDays = {filter.ComputerPasswordAgeWarningDays}
$ComputerPasswordAgeCriticalDays = {filter.ComputerPasswordAgeCriticalDays}
$TooManyGroupsThreshold = {filter.TooManyGroupsThreshold}
$LargeGroupThreshold = {filter.LargeGroupThreshold}
$MaxSecurityEventsPerDC = {filter.MaxSecurityEventsPerDC}
$EventReadTimeoutSeconds = {filter.EventReadTimeoutSeconds}
$EventLookbackHours = {filter.LookBackHours}
";
        }

        public async Task<DomainPostureDto?> GetDomainPostureAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_DomainPosture_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out DomainPostureDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<DomainPostureDto>("DomainPosture.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<UserRiskDto?> GetUserRiskAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_UserRisk_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out UserRiskDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<UserRiskDto>("UserRisk.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<GroupRiskDto?> GetGroupRiskAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_GroupRisk_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out GroupRiskDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<GroupRiskDto>("GroupRisk.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<ComputerRiskDto?> GetComputerRiskAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_ComputerRisk_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out ComputerRiskDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<ComputerRiskDto>("ComputerRisk.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<AclExposureDto?> GetAclExposureAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_AclExposure_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out AclExposureDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<AclExposureDto>("AclExposure.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<GpoExposureDto?> GetGpoExposureAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_GpoExposure_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out GpoExposureDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<GpoExposureDto>("GpoExposure.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }

        public async Task<LogonIntelligenceDto?> GetLogonIntelligenceAsync(IdentityXRayFilterRequest filter)
        {
            string cacheKey = $"IdentityXRay_LogonIntelligence_{filter.Domain}_{filter.RefreshView}";
            if (!filter.RefreshView && _memoryCache.TryGetValue(cacheKey, out LogonIntelligenceDto? cachedResult))
            {
                return cachedResult;
            }

            string variablesPrepended = BuildPrependedVariables(filter);
            var result = await ExecuteAndDeserializeAsync<LogonIntelligenceDto>("LogonIntelligence.ps1", variablesPrepended);

            if (result != null)
            {
                _memoryCache.Set(cacheKey, result, _cacheDuration);
            }

            return result;
        }
    }
}
