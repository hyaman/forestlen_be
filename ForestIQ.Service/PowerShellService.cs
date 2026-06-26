using DnsClient;
using ForestIQ.Domain;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public class PowerShellService : IPowerShellService
    {
        private const string AdDiagnosticsCacheKey = "AD_DIAGNOSTICS_GRAPH";
        private const string AdDomainsCacheKey = "AD_DOMAINS_CACHE";
        private const string AdSitesCacheKey = "AD_SITES_CACHE";

        private readonly ILogger<PowerShellService> _logger;
        private readonly IAdConnectionCache _cache;
        private readonly IMemoryCache _memoryCache;
        private readonly IEncryptionService _encryptionService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IKerberosService _kerberosService;
        private readonly IConfigureService _configureService;

        public PowerShellService(
            ILogger<PowerShellService> logger,
            IAdConnectionCache cache,
            IMemoryCache memoryCache,
            IEncryptionService encryptionService,
            IHttpContextAccessor httpContextAccessor,
            IKerberosService kerberosService,
            IConfigureService configureService)
        {
            _logger = logger;
            _cache = cache;
            _memoryCache = memoryCache;
            _encryptionService = encryptionService;
            _httpContextAccessor = httpContextAccessor;
            _kerberosService = kerberosService;
            _configureService = configureService;
        }

        public async Task<PowerShellExecutionResult> ConnectAsync(PowerShellRequest request)
        {
            _logger.LogInformation("==================================================");
            _logger.LogInformation("CONNECT ASYNC INITIATED");
            _logger.LogInformation("Target Domain: {Domain}", request.DomainName);
            _logger.LogInformation("Target RemoteHost (if provided): {Host}", request.RemoteHost);
            _logger.LogInformation("==================================================");

            var validationError = ValidateKerberosRequest(request);

            if (validationError != null)
            {
                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = validationError
                };
            }

            _logger.LogInformation("Connect Step 1: Configuring Container DNS...");
            var dnsSetupResult = await ConfigureContainerDnsAsync(request);
            _logger.LogInformation("Connect Step 1 Result: {Success} (Status: {StatusCode}) | Error: {Error}", dnsSetupResult.Success, dnsSetupResult.StatusCode, dnsSetupResult.Error ?? "None");

            if (!dnsSetupResult.Success)
            {
                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = dnsSetupResult.StatusCode,
                    Message = dnsSetupResult.Error
                };
            }

            var cachePath = _kerberosService.BuildCachePath(Guid.NewGuid().ToString("N"));

            _logger.LogInformation("Connect Step 2: Requesting Kerberos Ticket for {Domain} using {CachePath}...", request.DomainName, cachePath);
            var ticketResult = await _kerberosService.AcquireTicketAsync(request.DomainName, request.UserName, request.Password, cachePath);
            _logger.LogInformation("Connect Step 2 Result: Ticket Acquired = {Success} | Error: {Error}", ticketResult.Success, ticketResult.Error ?? "None");

            if (!ticketResult.Success)
            {
                _kerberosService.DestroyTicket(cachePath);

                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status401Unauthorized,
                    Message = "Kerberos authentication failed. Verify the DNS domain name, username, password, and that the container clock is synchronized with the domain.",
                    Error = ticketResult.Error
                };
            }

            request.KerberosCachePath = cachePath;

            var hostsToTry = new List<string>();

            if (!String.IsNullOrEmpty(request.RemoteHost))
            {
                _logger.LogInformation("Connect Step 3: Explicit RemoteHost provided. Skipping SRV DNS discovery.");
                hostsToTry.Add(request.RemoteHost);
            }
            else
            {
                _logger.LogInformation("Connect Step 3: No explicit RemoteHost provided. Running SRV DNS discovery...");
                hostsToTry = await BuildConnectHostListAsync(request);
                _logger.LogInformation("Connect Step 3 Result: Discovered {Count} hosts -> {Hosts}", hostsToTry.Count, string.Join(", ", hostsToTry));
            }


            if (hostsToTry.Count == 0)
            {
                _kerberosService.DestroyTicket(cachePath);

                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status404NotFound,
                    Message = "No domain controllers were found for the specified domain."
                };
            }

            var connectionErrors = new List<string>();
            var isDebug = Runtime.Debug.Enabled;
            foreach (var host in hostsToTry)
            {
                _logger.LogInformation(
                    "==================================================",
                    host,
                    request.DomainName);
                _logger.LogInformation("Connect Step 4: Attempting Kerberos WinRM connection to {Host} for domain {Domain}", host, request.DomainName);

                request.RemoteHost = host;

                var result = new PowerShellExecutionResult();


                if (!isDebug)
                {
                    result = await ExecutePowerShellAsync(request, GetConnectionScript(), "connect");
                }
                else
                {
                    result.Success = true ;
                }


                _logger.LogInformation("Connect Step 4 Result for {Host}: WinRM Execute Success = {Success} | Error: {Error} | Message: {Message}", host, result.Success, result.Error ?? "None", result.Message ?? "None");

                if (result.Success)
                {
                    result.KerberosCachePath = cachePath;
                    result.ConnectedHost = host;
                    result.Message = $"Connected to {host}.";
                    return result;
                }

                var hostError = result.Error ?? result.Message ?? "Connection failed.";
                connectionErrors.Add($"{host}: {hostError}");

                _logger.LogWarning(
                    "Connection to {Host} failed for domain {Domain}: {Error}",
                    host,
                    request.DomainName,
                    hostError);
            }

            _kerberosService.DestroyTicket(cachePath);

            return new PowerShellExecutionResult
            {
                Success = false,
                StatusCode = StatusCodes.Status502BadGateway,
                Message = "Failed to connect to any domain controller in the domain.",
                Error = string.Join(" | ", connectionErrors)
            };
        }
        
        public async Task<GraphResponse?> GetAdTopologyAsync(bool refreshView = false, string? domain = null, string? site = null)
        {
            var cacheKey = GetCacheKey(domain, site);

            if (refreshView)
            {
                _logger.LogInformation("Force-refreshing AD diagnostics cache.");
                _memoryCache.Remove(cacheKey);
            }
            else
            {
                if (_memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? cachedResult) && cachedResult != null)
                {
                    _logger.LogInformation("Returning AD topology from in-memory cache.");
                    if (cachedResult.Data.HasValue)
                    {
                        return FilterAndMapGraphResponse(cachedResult.Data.Value, null, null);
                    }
                    return GraphMapper.MapToGraphResponse(cachedResult.Data);
                }

                var globalCacheKey = GetCacheKey(null, null);
                if (cacheKey != globalCacheKey && _memoryCache.TryGetValue(globalCacheKey, out PowerShellExecutionResult? globalCachedResult) && globalCachedResult != null && globalCachedResult.Data.HasValue)
                {
                    _logger.LogInformation("Returning filtered AD topology from GLOBAL in-memory cache.");
                    return FilterAndMapGraphResponse(globalCachedResult.Data.Value, domain, site);
                }
            }

            var powerShellConnectionRequest = CheckAndGetSession("", domain, site);
            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-topology-script",
                GetAdTopologyScript());

            if (!result.Success || !result.Data.HasValue)
            {
                return null;
            }

            _memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? existingCache);
            var mergedData = MergeJsonElements(existingCache?.Data, result.Data.Value);

            var mergedResult = new PowerShellExecutionResult
            {
                Success = true,
                StatusCode = result.StatusCode,
                Data = mergedData
            };

            _memoryCache.Set(cacheKey, mergedResult, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(2)
            });

            _logger.LogInformation("AD topology fetched and merged in cache.");

            return FilterAndMapGraphResponse(mergedData, null, null);
        }

        public async Task<GraphResponse?> GetAdReplicationAsync(bool refreshView = false, string? domain = null, string? site = null)
        {
            var cacheKey = GetCacheKey(domain, site);

            if (!refreshView)
            {
                if (_memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? cachedResult) && cachedResult != null)
                {
                    if (cachedResult.Data.HasValue && cachedResult.Data.Value.TryGetProperty("ReplicationPartners", out var repProp) && repProp.ValueKind == JsonValueKind.Array)
                    {
                        _logger.LogInformation("Returning AD replication from in-memory cache.");
                        return FilterAndMapGraphResponse(cachedResult.Data.Value, null, null);
                    }
                }

                var globalCacheKey = GetCacheKey(null, null);
                if (cacheKey != globalCacheKey && _memoryCache.TryGetValue(globalCacheKey, out PowerShellExecutionResult? globalCachedResult) && globalCachedResult != null && globalCachedResult.Data.HasValue)
                {
                    if (globalCachedResult.Data.Value.TryGetProperty("ReplicationPartners", out var globalRepProp) && globalRepProp.ValueKind == JsonValueKind.Array)
                    {
                        _logger.LogInformation("Returning filtered AD replication from GLOBAL in-memory cache.");
                        return FilterAndMapGraphResponse(globalCachedResult.Data.Value, domain, site);
                    }
                }
            }

            var powerShellConnectionRequest = CheckAndGetSession("", domain, site);
            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-replication-script",
                GetAdReplicationScript());

            if (!result.Success || !result.Data.HasValue)
            {
                return null;
            }

            _memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? existingCache);

            var mergedData = MergeJsonElements(existingCache?.Data, result.Data.Value);

            var mergedResult = new PowerShellExecutionResult
            {
                Success = true,
                StatusCode = result.StatusCode,
                Data = mergedData
            };

            _memoryCache.Set(cacheKey, mergedResult, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(2)
            });

            _logger.LogInformation("AD replication fetched and merged in cache.");

            return FilterAndMapGraphResponse(mergedData, null, null);
        }

        public async Task<GraphResponse?> GetAdDiagnosticsProgressiveAsync(bool refreshView = false, string? domain = null, string? site = null)
        {
            var cacheKey = GetCacheKey(domain, site);

            if (!refreshView)
            {
                if (_memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? cachedResult) && cachedResult != null)
                {
                    if (cachedResult.Data.HasValue && cachedResult.Data.Value.TryGetProperty("DcDiagSummary", out var diagProp) && diagProp.ValueKind == JsonValueKind.Array)
                    {
                        _logger.LogInformation("Returning AD progressive diagnostics from in-memory cache.");
                        return FilterAndMapGraphResponse(cachedResult.Data.Value, null, null);
                    }
                }

                var globalCacheKey = GetCacheKey(null, null);
                if (cacheKey != globalCacheKey && _memoryCache.TryGetValue(globalCacheKey, out PowerShellExecutionResult? globalCachedResult) && globalCachedResult != null && globalCachedResult.Data.HasValue)
                {
                    if (globalCachedResult.Data.Value.TryGetProperty("DcDiagSummary", out var globalDiagProp) && globalDiagProp.ValueKind == JsonValueKind.Array)
                    {
                        _logger.LogInformation("Returning filtered AD progressive diagnostics from GLOBAL in-memory cache.");
                        return FilterAndMapGraphResponse(globalCachedResult.Data.Value, domain, site);
                    }
                }
            }

            var powerShellConnectionRequest = CheckAndGetSession("", domain, site);
            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-diagnostics-script",
                GetAdDiagnosticsScript());

            if (!result.Success || !result.Data.HasValue)
            {
                return null;
            }

            _memoryCache.TryGetValue(cacheKey, out PowerShellExecutionResult? existingCache);

            var mergedData = MergeJsonElements(existingCache?.Data, result.Data.Value);

            var mergedResult = new PowerShellExecutionResult
            {
                Success = true,
                StatusCode = result.StatusCode,
                Data = mergedData
            };

            _memoryCache.Set(cacheKey, mergedResult, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(2)
            });

            _logger.LogInformation("AD diagnostics fetched and merged in cache.");

            return FilterAndMapGraphResponse(mergedData, null, null);
        }

        public async Task<PowerShellExecutionResult> ExecuteCommandAsync(PowerShellExecuteRequest request)
        {
            if (!PowerShellCommandCatalog.Commands.TryGetValue(request.Action ?? "", out var command))
            {
                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = $"Unsupported action '{request.Action}'."
                };
            }

            var powerShellConnectionRequest = CheckAndGetSession(request.Action ?? "");

            return await ExecutePowerShellAsync(powerShellConnectionRequest, GetExecutionScript(), "execute", command.Command);
        }

        public async Task<PowerShellExecutionResult> ExecuteScriptAsync(PowerShellScriptRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Script))
            {
                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "Script cannot be empty."
                };
            }

            var powerShellConnectionRequest = CheckAndGetSession("execute-script");

            return await ExecutePowerShellAsync(powerShellConnectionRequest, GetExecutionScript(), "execute-script", request.Script);
        }

        public async Task<PowerShellExecutionResult> ExecuteBackgroundScriptAsync(AdConfiguration config, string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "Script cannot be empty."
                };
            }

            var request = new PowerShellRequest
            {
                DomainName = config.ForestName,
                RemoteHost = config.RemoteHost,
                UserName = config.UserName,
                Password = _encryptionService.Unprotect(config.EncryptedPassword),
                DnsServer = JsonSerializer.Deserialize<List<string>>(config.DnsServersJson ?? "[]").FirstOrDefault(),
                Action = "execute-background-script"
            };


            _logger.LogInformation("Connect Step 1: Configuring Container DNS...");
            var dnsSetupResult = await ConfigureContainerDnsAsync(request);
            _logger.LogInformation("Connect Step 1 Result: {Success} (Status: {StatusCode}) | Error: {Error}", dnsSetupResult.Success, dnsSetupResult.StatusCode, dnsSetupResult.Error ?? "None");

            return await ExecutePowerShellAsync(request, GetExecutionScript(), "execute-background-script", scriptContent);
        }

        public IEnumerable<PowerShellCommandDefinition> GetAvailableCommands()
        {
            var connection = CheckAndGetSession();

            var commands = PowerShellCommandCatalog.Commands
                .Values
                .OrderBy(x => x.Category)
                .ThenBy(x => x.Name);

            return commands.Select(x => new PowerShellCommandDefinition
            {
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                Category = x.Category
            });
        }

        public void ClearCache()
        {
            if (_memoryCache is MemoryCache cache)
            {
                cache.Compact(1.0); // Removes 100% of items. Clear() is available in .NET 7+, but Compact(1.0) is widely compatible and achieves the same.
            }
        }

        public async Task<List<string>> GetAdDomainsAsync(bool refreshView = false)
        {
            if (refreshView)
            {
                _memoryCache.Remove(AdDomainsCacheKey);
            }
            else if (_memoryCache.TryGetValue(AdDomainsCacheKey, out List<string>? cachedResult) && cachedResult != null)
            {
                return cachedResult;
            }

            var powerShellConnectionRequest = CheckAndGetSession();
            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-domains-script",
                GetAdDomainsScript());

            if (!result.Success || !result.Data.HasValue) return new List<string>();

            var dataElement = result.Data.Value;
            var list = new List<string>();
            if (dataElement.TryGetProperty("Items", out var itemsProp))
            {
                list = JsonSerializer.Deserialize<List<string>>(itemsProp.GetRawText()) ?? new List<string>();
            }

            _memoryCache.Set(AdDomainsCacheKey, list, TimeSpan.FromHours(24));
            return list;
        }

        public async Task<List<string>> GetAdSitesAsync(bool refreshView = false)
        {
            if (refreshView)
            {
                _memoryCache.Remove(AdSitesCacheKey);
            }
            else if (_memoryCache.TryGetValue(AdSitesCacheKey, out List<string>? cachedResult) && cachedResult != null)
            {
                return cachedResult;
            }

            var powerShellConnectionRequest = CheckAndGetSession();
            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-sites-script",
                GetAdSitesScript());

            if (!result.Success || !result.Data.HasValue) return new List<string>();

            var dataElement = result.Data.Value;
            var list = new List<string>();
            if (dataElement.TryGetProperty("Items", out var itemsProp))
            {
                list = JsonSerializer.Deserialize<List<string>>(itemsProp.GetRawText()) ?? new List<string>();
            }

            _memoryCache.Set(AdSitesCacheKey, list, TimeSpan.FromHours(24));
            return list;
        }

        public async Task<DomainDiscoveryResponse> DiscoverHostsAsync(PowerShellRequest request)
        {
            _logger.LogInformation("==================================================");
            _logger.LogInformation("DISCOVER HOSTS ASYNC INITIATED");
            _logger.LogInformation("Target Domain: {Domain}", request.DomainName);
            _logger.LogInformation("Target RemoteHost (if provided): {Host}", request.RemoteHost);
            _logger.LogInformation("==================================================");

            var response = new DomainDiscoveryResponse();
            var domainName = request.DomainName;

            if (string.IsNullOrWhiteSpace(domainName))
            {
                _logger.LogWarning("DiscoverHostsAsync failed: Domain name is empty.");
                return response;
            }

            _logger.LogInformation("Discover Step 1: Configuring Container DNS...");
           
            var dnsSetupResult = await ConfigureContainerDnsAsync(request);
           
            _logger.LogInformation("Discover Step 1 Result: {Success} (Status: {StatusCode}) | Error: {Error}", dnsSetupResult.Success, dnsSetupResult.StatusCode, dnsSetupResult.Error ?? "None");

            if (!dnsSetupResult.Success)
            {
                response.DomainControllerDiscoveryError = dnsSetupResult.Error;
                return response;
            }

            _logger.LogInformation("Discover Step 2: Running AD DNS Discovery for domain {Domain}...", domainName);
           
            var hosts = await DiscoverDomainControllerHostsAsync(domainName);
            
            _logger.LogInformation("Discover Step 2 Result: Found {Count} hosts: {Hosts}", hosts.Count, string.Join(", ", hosts));

            if (hosts.Count == 0)
            {
                _logger.LogWarning("All resolution attempts failed for domain: {Domain}", domainName);
                return response;
            }

            _logger.LogInformation("Discover Step 3: Checking WinRM ports (5985/5986) for {Count} discovered hosts...", hosts.Count);

            var tasks = hosts.Select(async host =>
            {
                var httpTask = IsPortOpenAsync(host, 5985, 2000, CancellationToken.None);
                var httpsTask = IsPortOpenAsync(host, 5986, 2000, CancellationToken.None);
                var portResults = await Task.WhenAll(httpTask, httpsTask);

                bool httpOpen = portResults[0];
                bool httpsOpen = portResults[1];

                string connectionType = (httpOpen, httpsOpen) switch
                {
                    (true, true) => "Both",
                    (true, false) => "HTTP",
                    (false, true) => "HTTPS",
                    _ => "None"
                };

                int port = httpsOpen ? 5986 : (httpOpen ? 5985 : 0);

                _logger.LogInformation("Host {Host} Port Check Result: HTTP(5985)={HTTP}, HTTPS(5986)={HTTPS}", host, httpOpen, httpsOpen);

                return new HostDiscoveryResult
                {
                    Host = host,
                    IsOnline = httpOpen || httpsOpen,
                    ConnectionType = connectionType,
                    Port = port,
                    HostIp = host
                };
            }).ToList();

            response.Hosts.AddRange(await Task.WhenAll(tasks));
            _logger.LogInformation("Discover Step 3 Result: Port scanning complete.");

            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return response;
            }

            var domainControllersResult = await DiscoverForestDomainControllersAsync(request, hosts);
           
            if (!domainControllersResult.Success)
            {
                response.DomainControllerDiscoveryError = domainControllersResult.Error;
                return response;
            }

            response.DomainControllers.AddRange(domainControllersResult.DomainControllers);
            return response;
        }

        #region Private Methods 
       
        private string GetCacheKey(string? domain, string? site)
        {
            return $"{AdDiagnosticsCacheKey}_{domain ?? "all"}_{site ?? "all"}";
        }

        private async Task<GraphResponse?> FetchAndCacheDiagnosticsAsync(string? domain, string? site)
        {
            var powerShellConnectionRequest = CheckAndGetSession("", domain, site);

            var result = await ExecutePowerShellAsync(
                powerShellConnectionRequest,
                GetExecutionScript(),
                "get-ad-health-report-script",
                GetAdHealthReportScript());

            if (!result.Success || !result.Data.HasValue)
            {
                return null;
            }

            var cacheKey = GetCacheKey(domain, site);
            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                SlidingExpiration = TimeSpan.FromHours(2)
            });

            _logger.LogInformation("AD diagnostics fetched and stored in cache.");

            return GraphMapper.MapToGraphResponse(result.Data);
        }

        private JsonElement MergeJsonElements(JsonElement? baseElement, JsonElement newElement)
        {
            if (!baseElement.HasValue) return newElement;

            var baseNode = System.Text.Json.Nodes.JsonObject.Create(baseElement.Value) ?? new System.Text.Json.Nodes.JsonObject();
            var newNode = System.Text.Json.Nodes.JsonObject.Create(newElement) ?? new System.Text.Json.Nodes.JsonObject();

            foreach (var kvp in newNode)
            {
                baseNode[kvp.Key] = kvp.Value?.DeepClone();
            }

            return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(baseNode.ToJsonString());
        }

        private GraphResponse? FilterAndMapGraphResponse(JsonElement jsonElement, string? domain = null, string? site = null)
        {
            var rawText = jsonElement.GetRawText();
            var topologyModel = JsonSerializer.Deserialize<ForestIQ.Domain.Models.PowerShell.AdTopologyModel>(rawText);
            var replicationModel = JsonSerializer.Deserialize<ForestIQ.Domain.Models.PowerShell.AdReplicationModel>(rawText);
            var diagModel = JsonSerializer.Deserialize<ForestIQ.Domain.Models.PowerShell.AdDiagnosticsModel>(rawText);

            if (!string.IsNullOrEmpty(domain))
            {
                if (topologyModel != null)
                {
                    topologyModel.Domains = topologyModel.Domains?.Where(d => string.Equals(d.DomainName, domain, StringComparison.OrdinalIgnoreCase)).ToList();
                    topologyModel.DomainControllers = topologyModel.DomainControllers?.Where(dc => dc.HostName?.EndsWith(domain, StringComparison.OrdinalIgnoreCase) == true).ToList();
                    topologyModel.TopLevelOUs = topologyModel.TopLevelOUs?.Where(ou => string.Equals(ou.Domain, domain, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (replicationModel != null)
                {
                    replicationModel.ReplicationPartners = replicationModel.ReplicationPartners?.Where(rp => string.Equals(rp.Domain, domain, StringComparison.OrdinalIgnoreCase)).ToList();
                    replicationModel.ReplicationFailures = replicationModel.ReplicationFailures?.Where(rf => rf.Server?.EndsWith(domain, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                if (diagModel != null)
                {
                    diagModel.PortConnectivity = diagModel.PortConnectivity?.Where(p => p.DomainController?.EndsWith(domain, StringComparison.OrdinalIgnoreCase) == true).ToList();
                    diagModel.DcDiagSummary = diagModel.DcDiagSummary?.Where(d => d.DomainController?.EndsWith(domain, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }
            }

            if (!string.IsNullOrEmpty(site))
            {
                if (topologyModel != null)
                {
                    topologyModel.Sites = topologyModel.Sites?.Where(s => string.Equals(s.Name, site, StringComparison.OrdinalIgnoreCase)).ToList();
                    topologyModel.DomainControllers = topologyModel.DomainControllers?.Where(dc => string.Equals(dc.Site, site, StringComparison.OrdinalIgnoreCase)).ToList();
                    topologyModel.Subnets = topologyModel.Subnets?.Where(s => string.Equals(s.Site, site, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (replicationModel != null)
                {
                    replicationModel.ReplicationPartners = replicationModel.ReplicationPartners?.Where(rp => string.Equals(rp.SiteName, site, StringComparison.OrdinalIgnoreCase) || string.Equals(rp.DestinationSite, site, StringComparison.OrdinalIgnoreCase)).ToList();
                    replicationModel.ReplicationFailures = replicationModel.ReplicationFailures?.Where(rf => string.Equals(rf.SiteName, site, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (diagModel != null)
                {
                    diagModel.PortConnectivity = diagModel.PortConnectivity?.Where(p => string.Equals(p.SiteName, site, StringComparison.OrdinalIgnoreCase)).ToList();
                    diagModel.DcDiagSummary = diagModel.DcDiagSummary?.Where(d => string.Equals(d.SiteName, site, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return GraphMapper.MapToGraphResponse(topologyModel, replicationModel, diagModel);
        }
        
        #endregion

        #region Discover Domain Controllers Helpers

        private async Task<(bool Success, List<DomainControllerDiscoveryResult> DomainControllers, string? Error)> DiscoverForestDomainControllersAsync(PowerShellRequest request, List<string> discoveredHosts)
        {
            var cachePath = _kerberosService.BuildCachePath(Guid.NewGuid().ToString("N"));

            try
            {
                request.KerberosCachePath = cachePath;

                var ticketResult = await _kerberosService.AcquireTicketAsync(request.DomainName, request.UserName, request.Password, cachePath);

                if (!ticketResult.Success)
                {
                    return (false, [], ticketResult.Error ?? "Kerberos authentication failed for domain controller discovery.");
                }

                var hostsToTry = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.RemoteHost) && !IPAddress.TryParse(request.RemoteHost, out _))
                {
                    hostsToTry.Add(request.RemoteHost.Trim());
                }

                hostsToTry.AddRange(discoveredHosts.Where(host => !IPAddress.TryParse(host, out _)));
                hostsToTry = hostsToTry
                    .Where(host => !string.IsNullOrWhiteSpace(host))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var connectionErrors = new List<string>();

                foreach (var host in hostsToTry)
                {
                    request.RemoteHost = host;

                    var result = await ExecutePowerShellAsync(request, GetExecutionScript(), "discover-domain-controllers", GetForestDomainControllersDiscoveryScript());

                    if (result.Success && result.Data.HasValue)
                    {
                        var domainControllers = DeserializeDomainControllers(result.Data.Value);

                        return (true, domainControllers, null);
                    }

                    var error = result.Error ?? result.Message ?? "Domain controller discovery failed.";
                    connectionErrors.Add($"{host}: {error}");
                }

                return (false, [], string.Join(" | ", connectionErrors));
            }
            finally
            {
                _kerberosService.DestroyTicket(cachePath);
                request.KerberosCachePath = null;
            }
        }

        private static List<DomainControllerDiscoveryResult> DeserializeDomainControllers(JsonElement data)
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<DomainControllerDiscoveryResult>>(data.GetRawText()) ?? [];
            }

            if (data.ValueKind == JsonValueKind.Object)
            {
                var domainController = JsonSerializer.Deserialize<DomainControllerDiscoveryResult>(data.GetRawText());
                return domainController == null ? [] : [domainController];
            }

            return [];
        }

        private static bool IsPrivateIp(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 || 
                  (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || 
                   bytes[0] == 192 && bytes[1] == 168;
        }

        private async Task<IPAddress[]?> TryStandardDnsAsync(string domainName)
        {
            try
            {
                var ips = await System.Net.Dns.GetHostAddressesAsync(domainName);
                var privateIps = ips?.Where(IsPrivateIp).ToArray();

                if (privateIps?.Length > 0)
                {
                    _logger.LogInformation("Resolved '{Domain}' via standard DNS to private IPs: {IPs}", domainName, string.Join(", ", privateIps.Select(i => i.ToString())));
                    return privateIps;
                }
                else if (ips?.Length > 0)
                {
                    _logger.LogWarning("Standard DNS resolved '{Domain}' to public IPs ({IPs}), which were ignored.", domainName, string.Join(", ", ips.Select(i => i.ToString())));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Standard DNS failed for '{Domain}'", domainName);
            }
            return null;
        }

        private async Task<List<string>> DiscoverDomainControllerHostsAsync(string domainName)
        {
            var srvHosts = await TrySrvDiscoveryAsync(domainName);
            if (srvHosts.Count > 0)
            {
                return srvHosts;
            }

            IPAddress[]? ipAddresses = await TryStandardDnsAsync(domainName);

            if (ipAddresses == null || ipAddresses.Length == 0)
            {
                ipAddresses = await TryAdDnsAsync(domainName);
            }

            if (ipAddresses == null || ipAddresses.Length == 0)
            {
                return new List<string>();
            }

            return ipAddresses.Select(ip => ip.ToString()).Distinct().ToList();
        }

        private async Task<List<string>> TrySrvDiscoveryAsync(string domainName)
        {
            var srvQuery = $"_ldap._tcp.dc._msdcs.{domainName.Trim().ToLowerInvariant()}";
            var dnsServers = GetLocalDnsServers();

            foreach (var dnsServer in dnsServers)
            {
                var hosts = await QuerySrvHostsAsync(srvQuery, new LookupClient(new IPEndPoint(dnsServer, 53)));
                if (hosts.Count > 0)
                {
                    _logger.LogInformation(
                        "Resolved domain controllers for '{Domain}' via SRV on DNS server '{DnsServer}': {Hosts}",
                        domainName,
                        dnsServer,
                        string.Join(", ", hosts));
                    return hosts;
                }
            }

            var fallbackHosts = await QuerySrvHostsAsync(srvQuery, new LookupClient());
            if (fallbackHosts.Count > 0)
            {
                _logger.LogInformation(
                    "Resolved domain controllers for '{Domain}' via system DNS SRV: {Hosts}",
                    domainName,
                    string.Join(", ", fallbackHosts));
            }

            return fallbackHosts;
        }

        private static async Task<List<string>> QuerySrvHostsAsync(string srvQuery, LookupClient dnsClient)
        {
            try
            {
                var result = await dnsClient.QueryAsync(srvQuery, QueryType.SRV);

                return result.Answers
                    .SrvRecords()
                    .OrderBy(record => record.Priority)
                    .ThenByDescending(record => record.Weight)
                    .Select(record => record.Target.Value.TrimEnd('.'))
                    .Where(host => !string.IsNullOrWhiteSpace(host) && !IPAddress.TryParse(host, out _))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<List<string>> BuildConnectHostListAsync(PowerShellRequest request)
        {
            var hosts = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.RemoteHost) && !IPAddress.TryParse(request.RemoteHost, out _))
            {
                hosts.Add(request.RemoteHost.Trim());
            }

            var discoveredHosts = await TrySrvDiscoveryAsync(request.DomainName);
            foreach (var host in discoveredHosts)
            {
                if (!hosts.Contains(host, StringComparer.OrdinalIgnoreCase))
                {
                    hosts.Add(host);
                }
            }

            return hosts;
        }

        private async Task<IPAddress[]?> TryAdDnsAsync(string domainName)
        {
            var dnsServers = GetLocalDnsServers();

            if (dnsServers == null || dnsServers.Length == 0)
            {
                _logger.LogWarning("No DNS servers found on local network interfaces");
                return null;
            }

            foreach (var dnsServer in dnsServers)
            {
                try
                {
                    _logger.LogInformation("Trying DNS server '{DnsServer}' for '{Domain}'", dnsServer, domainName);

                    // ? dnsServer is already IPAddress  no Parse() needed
                    var endpoint = new IPEndPoint(dnsServer, 53);
                    var dnsClient = new LookupClient(endpoint);

                    var result = await dnsClient.QueryAsync(domainName, QueryType.A);

                    var ips = result.Answers
                        .ARecords()
                        .Select(r => r.Address)
                        .ToArray();

                    var privateIps = ips.Where(IsPrivateIp).ToArray();

                    if (privateIps.Length > 0)
                    {
                        _logger.LogInformation(
                            "Resolved '{Domain}' via DNS server '{DnsServer}' to private IPs: {IPs}",
                            domainName, dnsServer, string.Join(", ", privateIps.Select(i => i.ToString())));
                        return privateIps;
                    }
                    else if (ips.Length > 0)
                    {
                        _logger.LogWarning("DNS server '{DnsServer}' resolved '{Domain}' to public IPs ({IPs}), which were ignored.", dnsServer, domainName, string.Join(", ", ips.Select(i => i.ToString())));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DNS server '{DnsServer}' failed for '{Domain}'", dnsServer, domainName);
                }
            }

            _logger.LogWarning("All DNS servers failed to resolve '{Domain}'", domainName);
            return null;
        }

        private async Task<(bool Success, string? Error, int StatusCode)> ConfigureContainerDnsAsync(PowerShellRequest request)
        {
            var dnsServers = CollectDnsServers(request);
            if (dnsServers.Count == 0)
            {
                return (true, null, StatusCodes.Status200OK);
            }

            var validDnsServers = new List<string>();
            foreach (var dnsServer in dnsServers)
            {
                if (!IPAddress.TryParse(dnsServer, out var ipAddress) || IPAddress.IsLoopback(ipAddress))
                {
                    return (false, $"Invalid DNS server '{dnsServer}'. Provide a non-loopback IP address.", StatusCodes.Status400BadRequest);
                }

                validDnsServers.Add(ipAddress.ToString());
            }

            validDnsServers = validDnsServers
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Runtime.ActiveDirectory.DnsServers = validDnsServers;

            if (!OperatingSystem.IsLinux())
            {
                _logger.LogInformation(
                    "DNS servers were provided for login and stored for application DNS lookups: {DnsServers}",
                    string.Join(", ", validDnsServers));

                return (true, null, StatusCodes.Status200OK);
            }

            try
            {
                var resolvConfLines = validDnsServers.Select(dnsServer => $"nameserver {dnsServer}").ToArray();

                await File.WriteAllLinesAsync("/etc/resolv.conf", resolvConfLines);

                _logger.LogInformation("Updated /etc/resolv.conf with DNS servers from login request: {DnsServers}", string.Join(", ", validDnsServers));

                return (true, null, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update /etc/resolv.conf with login DNS servers.");
                return (false, "Failed to update container DNS. Ensure the container user can write /etc/resolv.conf.", StatusCodes.Status500InternalServerError);
            }
        }

        private static List<string> CollectDnsServers(PowerShellRequest request)
        {
            var values = new List<string>();

            if (request.DnsServers != null)
            {
                values.AddRange(request.DnsServers);
            }

            values.Add(request.DnsServer ?? string.Empty);
            values.Add(request.Dns ?? string.Empty);

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value.Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private IPAddress[] GetLocalDnsServers()
        {
            var configuredDnsServers = Runtime.ActiveDirectory.DnsServers
                .Where(dnsServer => !string.IsNullOrWhiteSpace(dnsServer))
                .Select(dnsServer =>
                {
                    var value = dnsServer.Trim();
                    return IPAddress.TryParse(value, out var ipAddress)
                        ? ipAddress
                        : null;
                })
                .Where(ip =>
                    ip != null &&
                    ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                .Select(ip => ip!)
                .Distinct()
                .ToArray();

            if (configuredDnsServers.Length > 0)
            {
                return configuredDnsServers;
            }

            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni =>
                    ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().DnsAddresses)
                .Where(ip =>
                    ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                .Distinct()
                .ToArray();
        }

        #endregion

        #region Script Execution Helpers

        private async Task<PowerShellExecutionResult> ExecutePowerShellAsync(PowerShellRequest request, string scriptContent, string scriptPrefix, string? command = null)
        {
            var isDebug = Runtime.Debug.Enabled;
            var saveResponse = Runtime.Debug.SaveScriptResponse;

            var dataPath = "/app/data";
            // For local Windows development without Docker:
            if (!Directory.Exists(dataPath) && System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                dataPath = Path.Combine(System.AppContext.BaseDirectory, "data");
                if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            }

            var debugFilePath = Path.Combine(dataPath, $"debug_{scriptPrefix}.json");

            var remoteHost = request.RemoteHost;

            if (scriptPrefix != "connect" && (!isDebug || string.IsNullOrWhiteSpace(request.KerberosCachePath)))
            {
                var ticketReady = await EnsureKerberosTicketAsync(request);
                if (!ticketReady.Success)
                {
                    return new PowerShellExecutionResult
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Kerberos ticket is missing or expired. Please log in again.",
                        Error = ticketReady.Error
                    };
                }
            }

            var scriptPath = Path.Combine(Path.GetTempPath(), $"{scriptPrefix}-{Guid.NewGuid():N}.ps1");

            try
            {
                await File.WriteAllTextAsync(scriptPath, scriptContent);

                var startInfo = BuildProcessStartInfo(scriptPath, request, remoteHost, command);

                using var process = new Process
                {
                    StartInfo = startInfo
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = StripAnsiCodes(await outputTask).Trim();
                var error = StripAnsiCodes(await errorTask).Trim();

                if (string.IsNullOrWhiteSpace(output))
                {
                    return new PowerShellExecutionResult
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status502BadGateway,
                        Error = error,
                        Message = "PowerShell returned no output."
                    };
                }

                PowerShellResponse result;

                try
                {
                    result = JsonSerializer.Deserialize<PowerShellResponse>(output) ?? new PowerShellResponse();
                    
                    if (saveResponse)
                    {
                        try
                        {
                            if (!Directory.Exists(dataPath))
                            {
                                Directory.CreateDirectory(dataPath);
                            }
                            await File.WriteAllTextAsync(debugFilePath, output);
                            _logger.LogInformation("Saved debug script response to {File}", debugFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save debug script response to {File}", debugFilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize PowerShell response.");

                    return new PowerShellExecutionResult
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status502BadGateway,
                        Error = ex.Message,
                        Message = "Invalid JSON returned from PowerShell."
                    };
                }

                return new PowerShellExecutionResult
                {
                    Success = result?.Success ?? false,
                    StatusCode = result?.Success == true ? StatusCodes.Status200OK : StatusCodes.Status502BadGateway,
                    Data = result?.Data,
                    Error = result?.Error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PowerShell execution failed.");

                return new PowerShellExecutionResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Error = ex.Message,
                    Message = "Unexpected error occurred."
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete script file.");
                }
            }
        }

        private static ProcessStartInfo BuildProcessStartInfo(string scriptPath, PowerShellRequest request, string? remoteHost, string? command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/pwsh",
                Arguments =
                    $"-NoLogo -NoProfile -NonInteractive -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment["PS_REMOTE_DOMAIN"] = request.DomainName ?? string.Empty;

            startInfo.Environment["PS_REMOTE_USERNAME"] = request.UserName ?? string.Empty;
            startInfo.Environment["PS_REMOTE_PASSWORD"] = request.Password ?? string.Empty;

            startInfo.Environment["PS_REMOTE_HOST"] = request.RemoteHost ?? string.Empty;

            startInfo.Environment["PS_FILTER_DOMAIN"] = request.FilterDomain ?? string.Empty;
            startInfo.Environment["PS_FILTER_SITE"] = request.FilterSite ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(request.KerberosCachePath))
            {
                startInfo.Environment["KRB5CCNAME"] = $"FILE:{request.KerberosCachePath}";
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                startInfo.Environment["PS_REMOTE_COMMAND"] = command;
            }

            return startInfo;
        }

        private static string StripAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return Regex.Replace(
                input,
                @"\x1B\[[0-?]*[ -/]*[@-~]",
                "");
        }

        private static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port, cancellationToken);
                var delayTask = Task.Delay(timeoutMs, cancellationToken);

                var completedTask = await Task.WhenAny(connectTask.AsTask(), delayTask);
                if (completedTask == connectTask.AsTask() && client.Connected)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore connection failures
            }
            return false;
        }

        #endregion

        #region Session Check

        private PowerShellRequest CheckAndGetSession(string action = "", string? filterDomain = null, string? filterSite = null)
        {
            var sessionId = _httpContextAccessor.HttpContext?.User.FindFirst("connectionId")?.Value ?? throw new UnauthorizedAccessException("ConnectionId not found.");

            var connection = _cache.Get(sessionId);

            if (connection == null)
            {
                throw new UnauthorizedAccessException("Session expired.");
            }

            PowerShellRequest powerShellConnectionRequest = new PowerShellRequest
            {
                DomainName = connection.DomainName,
                RemoteHost = connection.RemoteHost,
                UserName = connection.UserName,
                Password = _encryptionService.Unprotect(connection.EncryptedPassword),
                KerberosCachePath = connection.KerberosCachePath,
                Action = action,
                FilterDomain = filterDomain,
                FilterSite = filterSite
            };

            return powerShellConnectionRequest;
        }

        private static string? ValidateKerberosRequest(PowerShellRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DomainName))
            {
                return "Domain name is required.";
            }

            if (!request.DomainName.Contains('.'))
            {
                return "Enter the DNS domain name (for example contoso.com), not the NetBIOS name.";
            }

            if (!string.IsNullOrWhiteSpace(request.RemoteHost) && IPAddress.TryParse(request.RemoteHost, out _))
            {
                return "Kerberos requires a domain controller hostname (for example dc01.contoso.com), not an IP address.";
            }

            return null;
        }

        private async Task<(bool Success, string? Error)> EnsureKerberosTicketAsync(PowerShellRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.KerberosCachePath) && File.Exists(request.KerberosCachePath))
            {
                return (true, null);
            }

            if (string.IsNullOrWhiteSpace(request.KerberosCachePath))
            {
                request.KerberosCachePath = _kerberosService.BuildCachePath(Guid.NewGuid().ToString("N"));
            }

            return await _kerberosService.AcquireTicketAsync(
                request.DomainName,
                request.UserName,
                request.Password,
                request.KerberosCachePath);
        }

        #endregion

        #region PowerShell Scripts

        private static string GetConnectionScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetConnectionScript.ps1"));
        }

        private static string GetForestDomainControllersDiscoveryScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetForestDomainControllersDiscoveryScript.ps1"));
        }

        private static string GetExecutionScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetExecutionScript.ps1"));
        }

        public static string GetAdHealthReportScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdHealthReportScript.ps1"));
        }

        public static string GetAdTopologyScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdTopologyScript.ps1"));
        }

        public static string GetAdReplicationScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdReplicationScript.ps1"));
        }

        public static string GetAdDiagnosticsScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdDiagnosticsScript.ps1"));
        }

        public static string GetAdDomainsScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdDomainsScript.ps1"));
        }

        public static string GetAdSitesScript()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Scripts", "GetAdSitesScript.ps1"));
        }

        #endregion
    }
}
