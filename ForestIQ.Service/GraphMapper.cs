using ForestIQ.Domain.DTO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForestIQ.Service
{
    public static class GraphMapper
    {
        public static GraphResponse MapToGraphResponse(JsonElement? data)   
        {
            var response = new GraphResponse();

            if (!data.HasValue)
            {
                return response;
            }

            var root = data.Value;
            var generatedAt = GetString(root, "GeneratedAt");
            var domainForestInfo = GetObject(root, "DomainForestInfo") ?? GetObject(root, "ForestInfo");
            var forestInfo = GetObject(root, "ForestInfo");
            var domains = GetArray(root, "Domains");
            var sites = GetArray(root, "Sites");
            var subnets = GetArray(root, "Subnets");
            var domainControllers = GetArray(root, "DomainControllers");
            var siteLinks = GetArray(root, "SiteLinkReport");
            var portConnectivity = GetArray(root, "PortConnectivity");
            var dcDiagSummary = GetArray(root, "DcDiagSummary");
            var repadminSummary = GetObject(root, "RepadminSummary");
            var replicationPartners = GetArray(root, "ReplicationPartners");
            var replicationFailures = GetArray(root, "ReplicationFailures");
            var replicationConnections = GetArray(root, "ReplicationConnections");
            var errors = GetArray(root, "Errors");
            var timings = GetArray(root, "Timings");
            var dcLocator = GetObject(root, "DcLocator");
            var topLevelOUs = GetArray(root, "TopLevelOUs");
            var securityPosture = GetObject(root, "SecurityPosture");

            var forestName = GetString(domainForestInfo, "ForestName", "Unknown Forest");
            var rootDomain = GetString(forestInfo, "RootDomain", GetString(domainForestInfo, "DomainName", forestName));
            var forestId = $"node-forest-{SlugRoot(forestName)}";
            var primaryDomainId = $"node-domain-{SlugRoot(rootDomain)}";
            var domainIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var siteIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dcIdsByFqdn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dcIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dcFqdnsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var forestChildIds = new List<string>();
            var domainSiteIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var siteDcIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var configurationServers = GetArray(root, "ConfigurationServers");
            var referencedDcs = GetReferencedDcs(forestInfo, repadminSummary, replicationFailures, replicationPartners, configurationServers);
            var placeholderDcs = referencedDcs.Where(refDc => !domainControllers.Any(dc => 
                GetString(dc, "Name", GetString(dc, "HostName")).Equals(refDc.Name, StringComparison.OrdinalIgnoreCase) || 
                GetString(dc, "HostName").StartsWith(refDc.Name + ".", StringComparison.OrdinalIgnoreCase))).ToList();

            var forestDomains = GetArray(forestInfo, "Domains");
            var isDomainFiltered = forestDomains.Count > 0 && domains.Count < forestDomains.Count;

            if (isDomainFiltered)
            {
                placeholderDcs.Clear();
            }

            var domainElements = domains.Count > 0
                ? domains
                : domainForestInfo.HasValue
                    ? [domainForestInfo.Value]
                    : [];

            foreach (var domain in domainElements)
            {
                var domainName = GetString(domain, "DomainName", rootDomain);
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    continue;
                }

                var domainId = $"node-domain-{SlugRoot(domainName)}";
                domainIdsByName[domainName] = domainId;
                forestChildIds.Add(domainId);
                domainSiteIds[domainId] = [];
            }

            if (!domainIdsByName.ContainsKey(rootDomain))
            {
                domainIdsByName[rootDomain] = primaryDomainId;
                forestChildIds.Insert(0, primaryDomainId);
                domainSiteIds[primaryDomainId] = [];
            }

            var replicationHealthSummary = GetObject(root, "ReplicationHealthSummary");
            var (forestHealth, forestReason) = GetForestHealth(repadminSummary, replicationHealthSummary, domainElements);
            var activeReplicationFailures = replicationFailures.Count(f => GetString(f, "Status").Equals("Failure Found", StringComparison.OrdinalIgnoreCase));
            var globalCatalogs = GetArray(forestInfo, "GlobalCatalogs")
                .Select(gc => gc.ValueKind == JsonValueKind.String ? gc.GetString() : GetString(gc, "Name", GetString(gc, "HostName")))
                .Where(gc => !string.IsNullOrWhiteSpace(gc))
                .Select(gc => gc!)
                .ToList();
            var domainNames = domainElements
                .Select(domain => GetString(domain, "DomainName"))
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var siteNames = sites
                .Select(site => GetString(site, "Name"))
                .Where(site => !string.IsNullOrWhiteSpace(site))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
           // var dcDiagIssueCount = dcDiagSummary.Count(diag => !GetString(diag, "Status", "Healthy").Equals("Healthy", StringComparison.OrdinalIgnoreCase));
           // var siteLinkWarningCount = siteLinks.Count(link => !GetString(link, "Status", "Healthy").Equals("Healthy", StringComparison.OrdinalIgnoreCase) &&
           //                                                    !GetString(link, "Status").Equals("Informational", StringComparison.OrdinalIgnoreCase));
           // var closedPortChecks = portConnectivity.Count(port => !GetBool(port, "Open"));
           // var slowestTiming = timings
           //     .OrderByDescending(timing => GetDouble(timing, "ElapsedSeconds"))
           //     .FirstOrDefault();
            var forestMeta = new Dictionary<string, object>
            {
                { "functionalLevel", GetString(domainForestInfo, "ForestMode") },
                { "description", $"Active Directory Forest: {forestName}" },
                { "rootDomain", rootDomain },
                { "domainCount", forestChildIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() },
                { "siteCount", sites.Count },
                { "lastDiscoveredAt", generatedAt },
                { "attributes", CompactAttributes(new Dictionary<string, object?>
                    {
                        { "generatedAt", generatedAt },
                        { "rootDomain", rootDomain },
                        { "globalCatalogs", globalCatalogs.Count },
                        { "statusMessage", forestReason }
                    }
                ) },
                { "statusMessage", forestReason },
                { "schemaMaster", GetString(domainForestInfo, "SchemaMaster") },
                { "domainNamingMaster", GetString(domainForestInfo, "DomainNamingMaster") },
                { "totalDomainControllers", domainControllers.Count },
                { "totalSubnets", subnets.Count },
                { "totalUsers", GetInt(domainForestInfo, "UserCount") },
                { "totalGroups", GetInt(domainForestInfo, "GroupCount") },
                { "globalCatalogCount", globalCatalogs.Count },
                { "globalCatalogs", globalCatalogs },
                { "domainNames", domainNames },
                { "siteNames", siteNames },
                { "writableDomainControllers", domainControllers.Count(dc => !GetBool(dc, "IsReadOnly")) },
                { "readOnlyDomainControllers", domainControllers.Count(dc => GetBool(dc, "IsReadOnly")) },
                { "configurationServerCount", configurationServers.Count },
                { "siteLinkCount", siteLinks.Count },
                //{ "siteLinkWarningCount", siteLinkWarningCount },
                { "replicationPartnerCount", replicationPartners.Count },
                { "replicationConnectionCount", replicationConnections.Count },
                { "activeReplicationFailures", activeReplicationFailures },
                //{ "replicationHealth", new
                //    {
                //        healthy = GetBool(replicationHealthSummary, "Healthy", GetBool(repadminSummary, "Healthy", true)),
                //        activeFailures = GetInt(replicationHealthSummary, "ActiveFailures", activeReplicationFailures)
                //    }
                //},
                //{ "repadminSummary", new
                //    {
                //        healthy = GetBool(repadminSummary, "Healthy", true),
                //        sourceDsaCount = GetArray(repadminSummary, "SourceDSA").Count,
                //        destinationDsaCount = GetArray(repadminSummary, "DestinationDSA").Count,
                //        operationalErrorCount = GetArray(repadminSummary, "OperationalErrors").Count
                //    }
                //},
                //{ "dcDiagSummary", new
                //    {
                //        healthyCount = dcDiagSummary.Count(diag => GetString(diag, "Status", "Healthy").Equals("Healthy", StringComparison.OrdinalIgnoreCase)),
                //        issueCount = dcDiagIssueCount,
                //        warningCount = dcDiagSummary.Count(diag => GetString(diag, "Status").Equals("Warning", StringComparison.OrdinalIgnoreCase)),
                //        criticalCount = dcDiagSummary.Count(diag => GetString(diag, "Status").Equals("Critical", StringComparison.OrdinalIgnoreCase) ||
                //                                                    GetString(diag, "Status").Equals("Error", StringComparison.OrdinalIgnoreCase))
                //    }
                //},
                //{ "portConnectivitySummary", new
                //    {
                //        totalChecks = portConnectivity.Count,
                //        openChecks = portConnectivity.Count(port => GetBool(port, "Open")),
                //        closedChecks = closedPortChecks,
                //        affectedDomainControllers = portConnectivity
                //            .Where(port => !GetBool(port, "Open"))
                //            .Select(port => GetString(port, "DomainController"))
                //            .Where(dc => !string.IsNullOrWhiteSpace(dc))
                //            .Distinct(StringComparer.OrdinalIgnoreCase)
                //            .Count()
                //    }
                //},
                { "topLevelOuCount", topLevelOUs.Count },
                { "scriptErrorCount", errors.Count },
                { "scriptErrors", errors.Select(e => new { Section = GetString(e, "Section"), Error = GetString(e, "Error") }) },
                { "scriptTimings", timings.Select(t => new { Section = GetString(t, "Section"), ElapsedSeconds = GetDouble(t, "ElapsedSeconds") }) }
            };

            if (dcLocator.HasValue)
            {
                forestMeta["dcLocator"] = new
                {
                    domainController = GetString(dcLocator, "DomainController"),
                    ipAddress = GetString(dcLocator, "Address"),
                    domainName = GetString(dcLocator, "DomainName"),
                    forestName = GetString(dcLocator, "ForestName"),
                    dcSite = GetString(dcLocator, "DcSiteName"),
                    clientSite = GetString(dcLocator, "OurSiteName"),
                    flags = GetString(dcLocator, "Flags")
                };
            }

            if (securityPosture.HasValue)
            {
                forestMeta["securityPosture"] = new
                {
                    recycleBinEnabled = GetBool(securityPosture, "RecycleBinEnabled"),
                    complexityEnabled = GetBool(securityPosture, "ComplexityEnabled"),
                    minPasswordLength = GetInt(securityPosture, "MinPasswordLength"),
                    maxPasswordAgeDays = GetInt(securityPosture, "MaxPasswordAgeDays"),
                    minPasswordAgeDays = GetInt(securityPosture, "MinPasswordAgeDays"),
                    passwordHistoryCount = GetInt(securityPosture, "PasswordHistoryCount"),
                    lockoutThreshold = GetInt(securityPosture, "LockoutThreshold"),
                    lockoutDurationMins = GetInt(securityPosture, "LockoutDurationMins"),
                    lockoutObservationMins = GetInt(securityPosture, "LockoutObservationMins")
                };
            }

            //if (slowestTiming.ValueKind != JsonValueKind.Undefined)
            //{
            //    forestMeta["slowestScriptTiming"] = new
            //    {
            //        section = GetString(slowestTiming, "Section"),
            //        elapsedSeconds = GetDouble(slowestTiming, "ElapsedSeconds")
            //    };
            //}

            var forestNode = new GraphNode
            {
                Id = forestId,
                Label = forestName,
                Type = "FOREST",
                Health = forestHealth,
                ParentId = null,
                Depth = 0,
                ChildIds = forestChildIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Meta = forestMeta
            };
            response.Nodes.Add(forestNode);

            var domainIndex = 0;
            foreach (var domain in domainElements)
            {
                var domainName = GetString(domain, "DomainName", rootDomain);
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    continue;
                }
                var domainId = domainIdsByName[domainName];
                var status = GetString(domain, "Status", "OK");
                var isPrimary = domainName.Equals(rootDomain, StringComparison.OrdinalIgnoreCase);
                var domainDcCount = domainControllers.Count(dc => GetDomainFromFqdn(GetString(dc, "HostName")).Equals(domainName, StringComparison.OrdinalIgnoreCase));

                if (isPrimary && domainDcCount == 0)
                {
                    domainDcCount = domainControllers.Count;
                }

                var domainNode = new GraphNode
                {
                    Id = domainId,
                    Label = domainName,
                    Type = "DOMAIN",
                    Health = IsOkStatus(status) ? "HEALTHY" : "CRITICAL",
                    ParentId = forestId,
                    Depth = 1,
                    Meta = new Dictionary<string, object>
                    {
                        { "netbiosName", GetString(domain, "NetBIOSName", GuessNetbios(domainName)) },
                        { "functionalLevel", GetString(domain, "DomainMode", isPrimary ? GetString(domainForestInfo, "DomainMode", "Unknown") : "Unknown") },
                        { "parentForestId", forestId },
                        { "description", isPrimary ? "Primary Domain" : "Child Partner Domain" },
                        { "dcCount", domainDcCount },
                        { "fsmoRoles", Array.Empty<string>() },
                        { "attributes", CompactAttributes(new Dictionary<string, object?>
                            {
                                { "status", IsOkStatus(status) ? "OK" : "Error" },
                                { "statusMessage", IsOkStatus(status) ? "Domain is reachable and responding normally" : $"Domain reported an error: {status}" },
                                { "errorMessage", IsOkStatus(status) ? null : status },
                                { "pdcEmulator", GetString(domain, "PDCEmulator", GetString(domainForestInfo, "PDCEmulator")) },
                                { "ridMaster", GetString(domain, "RIDMaster", GetString(domainForestInfo, "RIDMaster")) },
                                { "infrastructureMaster", GetString(domain, "InfrastructureMaster", GetString(domainForestInfo, "InfrastructureMaster")) },
                                { "schemaMaster", GetString(domainForestInfo, "SchemaMaster") },
                                { "domainNamingMaster", GetString(domainForestInfo, "DomainNamingMaster") }
                            }
                        ) },
                        { "parentDomain", GetString(domain, "ParentDomain") },
                        { "childDomains", GetArray(domain, "ChildDomains").Select(s => s.GetString()).Where(s => !string.IsNullOrEmpty(s)) }
                    }
                };

                if (isPrimary)
                {
                    domainNode.Meta["userCount"] = GetInt(domainForestInfo, "UserCount");
                    domainNode.Meta["groupCount"] = GetInt(domainForestInfo, "GroupCount");
                    
                    if (dcLocator.HasValue)
                    {
                        domainNode.Meta["closestDcLocator"] = new
                        {
                            domainController = GetString(dcLocator, "DomainController"),
                            ipAddress = GetString(dcLocator, "Address"),
                            dcSite = GetString(dcLocator, "DcSiteName"),
                            clientSite = GetString(dcLocator, "OurSiteName")
                        };
                    }
                    
                    if (securityPosture.HasValue)
                    {
                        domainNode.Meta["securityPosture"] = new
                        {
                            recycleBinEnabled = GetBool(securityPosture, "RecycleBinEnabled"),
                            complexityEnabled = GetBool(securityPosture, "ComplexityEnabled"),
                            minPasswordLength = GetInt(securityPosture, "MinPasswordLength"),
                            maxPasswordAgeDays = GetInt(securityPosture, "MaxPasswordAgeDays"),
                            minPasswordAgeDays = GetInt(securityPosture, "MinPasswordAgeDays"),
                            passwordHistoryCount = GetInt(securityPosture, "PasswordHistoryCount"),
                            lockoutThreshold = GetInt(securityPosture, "LockoutThreshold"),
                            lockoutDurationMins = GetInt(securityPosture, "LockoutDurationMins"),
                            lockoutObservationMins = GetInt(securityPosture, "LockoutObservationMins")
                        };
                    }
                    
                    domainNode.Meta["topLevelOUs"] = topLevelOUs.Select(ou => 
                    {
                        var name = GetString(ou, "Name");
                        var count = GetInt(ou, "ObjectCount");
                        return new
                        {
                            name = name,
                            type = GetString(ou, "Type", "Organizational Unit"),
                            domain = GetString(ou, "Domain", domainName),
                            description = GetFriendlyOuDescription(name),
                            distinguishedName = GetString(ou, "DistinguishedName"),
                            objectCount = count,
                            objectCountLabel = count > 0 ? $"Contains {count} Object{(count == 1 ? "" : "s")}" : "Empty"
                        };
                    }).Where(ou => !string.IsNullOrWhiteSpace(ou.name)).ToList();
                }

                response.Nodes.Add(domainNode);
                response.Edges.Add(new GraphEdge
                {
                    Id = isPrimary ? "edge-domain-forest" : $"edge-domain-{SlugRoot(domainName)}-forest",
                    SourceId = domainId,
                    TargetId = forestId,
                    RelationshipType = "DOMAIN_MEMBER_OF_FOREST"
                });

                domainIndex++;
            }

            var siteCoordinates = new[] { 150, 450, 750, 1050, 1350 };
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var siteName = GetString(site, "Name", "Unknown Site");
                var siteId = $"node-site-{siteName}";
                var parentDomainId = ResolveSiteDomainId(siteName, domainIdsByName, primaryDomainId, forestId, domainControllers);
                var siteDcCount = domainControllers.Count(dc => GetString(dc, "Site").Equals(siteName, StringComparison.OrdinalIgnoreCase));
                var sitePlaceholderCount = placeholderDcs.Count(dc => string.Equals(dc.SiteName, siteName, StringComparison.OrdinalIgnoreCase));
                var totalSiteDcCount = siteDcCount + sitePlaceholderCount;
                
                if (isDomainFiltered && totalSiteDcCount == 0)
                {
                    continue;
                }
                
                var subnetCidrs = subnets
                    .Where(subnet => NormalizeSiteName(GetString(subnet, "Site")).Equals(siteName, StringComparison.OrdinalIgnoreCase))
                    .Select(subnet => GetString(subnet, "Name"))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
                var subnetDetails = subnets
                    .Where(subnet => NormalizeSiteName(GetString(subnet, "Site")).Equals(siteName, StringComparison.OrdinalIgnoreCase))
                    .Select(subnet => new
                    {
                        cidr = GetString(subnet, "Name"),
                        location = GetString(subnet, "Location"),
                        description = GetString(subnet, "Description"),
                        site = siteName
                    })
                    .Where(subnet => !string.IsNullOrWhiteSpace(subnet.cidr))
                    .ToList();
                var siteLinkDetails = BuildSiteLinks(siteName, siteLinks);
                var siteLinkSummary = BuildSiteLinkSummary(siteName, siteLinks);
                var siteDomainControllers = domainControllers
                    .Where(dc => GetString(dc, "Site").Equals(siteName, StringComparison.OrdinalIgnoreCase))
                    .Select(dc =>
                    {
                        var fqdn = GetString(dc, "HostName");
                        var (health, statusMessage) = GetDcHealth(fqdn, replicationFailures, repadminSummary, dcDiagSummary);
                        return new
                        {
                            name = GetString(dc, "Name", fqdn),
                            fqdn,
                            ipAddress = GetString(dc, "IPv4Address"),
                            domain = GetDomainFromFqdn(fqdn),
                            isGlobalCatalog = GetBool(dc, "IsGlobalCatalog"),
                            isReadOnly = GetBool(dc, "IsReadOnly"),
                            operatingSystem = GetString(dc, "OperatingSystem"),
                            operatingSystemVersion = GetString(dc, "OperatingSystemVersion"),
                            uptimeHours = GetDouble(dc, "UptimeHours"),
                            fsmoRoles = GetFsmoRoles(dc),
                            health,
                            statusMessage
                        };
                    })
                    .ToList();

                siteIdsByName[siteName] = siteId;
                siteDcIds[siteId] = [];
                domainSiteIds.TryAdd(parentDomainId, []);
                domainSiteIds[parentDomainId].Add(siteId);
                var (siteHealth, siteReason) = totalSiteDcCount == 0 ? ("CRITICAL", "No domain controllers found in this site") : GetSiteHealth(siteName, replicationFailures);

                response.Nodes.Add(new GraphNode
                {
                    Id = siteId,
                    Label = siteName,
                    Type = "SITE",
                    Health = siteHealth,
                    ParentId = parentDomainId,
                    Depth = 2,
                    Meta = new Dictionary<string, object>
                    {
                        { "dcCount", totalSiteDcCount },
                        { "subnetCount", subnetCidrs.Count },
                        { "siteLinkCount", siteLinkSummary.Total },
                        { "siteLinkWarningCount", siteLinkSummary.Warning },
                        { "description", GetString(site, "Description") },
                        { "subnetCidrs", subnetCidrs },
                        { "subnetDetails", subnetDetails },
                        { "domainControllers", siteDomainControllers },
                        { "siteLinkSummary", siteLinkSummary },
                        { "siteLinkCost", siteLinkDetails },
                        { "statusMessage", siteReason }
                    }
                });

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-site-{siteName}-dom",
                    SourceId = siteId,
                    TargetId = parentDomainId,
                    RelationshipType = "SITE_BELONGS_TO_DOMAIN"
                });
            }

            foreach (var node in response.Nodes.Where(node => node.Type == "DOMAIN"))
            {
                if (domainSiteIds.TryGetValue(node.Id, out var childIds))
                {
                    node.ChildIds = childIds;
                }
            }

            var dcIndexBySite = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var dc in domainControllers)
            {
                var fqdn = GetString(dc, "HostName", "Unknown DC");
                var dcName = GetString(dc, "Name", fqdn);
                var siteName = GetString(dc, "Site");
                var siteId = siteIdsByName.TryGetValue(siteName, out var sid) ? sid : null;
                
                if (siteId == null) continue;

                var domainId = ResolveDcDomainId(fqdn, domainIdsByName, primaryDomainId, forestId);
                var dcId = $"node-dc-{SlugDc(dcName)}";
                var siteOrdinal = dcIndexBySite.GetValueOrDefault(siteName);
                dcIndexBySite[siteName] = siteOrdinal + 1;

                dcIdsByFqdn[fqdn] = dcId;
                dcIdsByName[dcName] = dcId;
                dcFqdnsById[dcId] = fqdn;

                if (siteId != null)
                {
                    siteDcIds.TryAdd(siteId, []);
                    siteDcIds[siteId].Add(dcId);
                }

                var (health, dcStatusMessage) = GetDcHealth(fqdn, replicationFailures, repadminSummary, dcDiagSummary);
                var attrs = BuildDcAttributes(fqdn, health, dcStatusMessage, replicationFailures, repadminSummary);
                var dcDomain = GetDomainFromFqdn(fqdn);
                var dcInboundPartners = replicationPartners
                    .Where(partner => GetString(partner, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var dcOutboundPartners = replicationPartners
                    .Where(partner => GetString(partner, "SourceDCName").Equals(dcName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var dcActiveFailures = replicationFailures
                    .Where(failure => GetString(failure, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) &&
                                      GetString(failure, "Status").Equals("Failure Found", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var dcNamingContexts = dcInboundPartners
                    .Select(partner => GetString(partner, "NamingContext", GetString(partner, "Partition")))
                    .Where(context => !string.IsNullOrWhiteSpace(context))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var dcDiag = dcDiagSummary.FirstOrDefault(diag => GetString(diag, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase));
                var portSummary = BuildPortSummary(fqdn, portConnectivity);
                var worstLatencyMinutes = dcInboundPartners
                    .Select(partner => GetDouble(partner, "LatencyMinutes", GetDouble(partner, "ReplicationLatencyMinutes")))
                    .DefaultIfEmpty(0)
                    .Max();
                var lastSuccessfulReplication = dcInboundPartners
                    .Select(partner => GetNullableString(partner, "LastReplicationSuccess"))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .OrderByDescending(value => value, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                var meta = new Dictionary<string, object>
                {
                    { "fqdn", fqdn },
                    { "ipAddress", GetString(dc, "IPv4Address") },
                    { "domain", dcDomain },
                    { "siteName", siteName },
                    { "parentDomainId", domainId },
                    { "fsmoRoles", GetFsmoRoles(dc) },
                    { "isGlobalCatalog", GetBool(dc, "IsGlobalCatalog") },
                    { "isReadOnly", GetBool(dc, "IsReadOnly") },
                    { "operatingSystem", GetString(dc, "OperatingSystem") },
                    { "operatingSystemVersion", GetString(dc, "OperatingSystemVersion") },
                    { "uptimeHours", GetDouble(dc, "UptimeHours") },
                    { "replicationSummary", new
                        {
                            inboundPartnerCount = dcInboundPartners
                                .Select(partner => GetString(partner, "SourceDCName"))
                                .Where(partner => !string.IsNullOrWhiteSpace(partner))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count(),
                            outboundPartnerCount = dcOutboundPartners
                                .Select(partner => GetString(partner, "DomainController"))
                                .Where(partner => !string.IsNullOrWhiteSpace(partner))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count(),
                            activeFailureCount = dcActiveFailures.Count,
                            historicalFailureCount = replicationFailures.Count(failure =>
                                GetString(failure, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) &&
                                GetString(failure, "Status").Equals("Historical Failure", StringComparison.OrdinalIgnoreCase)),
                            worstLatencyMinutes,
                            lastSuccessAt = lastSuccessfulReplication,
                            namingContextCount = dcNamingContexts.Count,
                            namingContexts = dcNamingContexts.Select(BuildNamingContextInfo).ToList()
                        }
                    },
                    { "diagnosticsSummary", new
                        {
                            status = dcDiag.ValueKind == JsonValueKind.Undefined ? "Unknown" : GetString(dcDiag, "Status", "Unknown"),
                            category = dcDiag.ValueKind == JsonValueKind.Undefined ? "Unknown" : GetString(dcDiag, "Category", "Unknown"),
                            details = dcDiag.ValueKind == JsonValueKind.Undefined ? "No DCDiag result was collected for this domain controller." : GetString(dcDiag, "Details")
                        }
                    },
                    { "portSummary", portSummary },
                    { "attributes", attrs },
                    { "statusMessage", dcStatusMessage },
                };

                if (siteId != null)
                {
                    meta["parentSiteId"] = siteId;
                }

                var dcHistoricalFailures = replicationFailures
                    .Where(f => GetString(f, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) && GetString(f, "Status").Equals("Historical Failure", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new
                    {
                        partner = GetString(f, "Partner"),
                        server = GetString(f, "Server"),
                        firstFailureTime = GetNullableString(f, "FirstFailureTime"),
                        lastError = GetString(f, "LastError"),
                        errorMessage = GetWin32ErrorMessage(GetInt(f, "LastError"), GetString(f, "LastError", "Replication failure found."))
                    })
                    .ToList();

                if (dcHistoricalFailures.Count > 0)
                {
                    meta["historicalFailures"] = dcHistoricalFailures;
                }

                var portStatus = BuildPortStatus(fqdn, portConnectivity);
                if (portStatus.Count > 0)
                {
                    meta["portStatus"] = portStatus;
                }

                var diagTests = BuildDiagTests(fqdn, dcDiagSummary);
                if (diagTests.Count > 0)
                {
                    meta["diagTests"] = diagTests;
                }

                response.Nodes.Add(new GraphNode
                {
                    Id = dcId,
                    Label = dcName,
                    Type = "DOMAIN_CONTROLLER",
                    Health = health,
                    ParentId = siteId ?? domainId,
                    Depth = 3,
                    Meta = meta
                });

                if (siteId != null)
                {
                    response.Edges.Add(new GraphEdge
                    {
                        Id = $"edge-dc-{SlugDc(dcName)}-site",
                        SourceId = dcId,
                        TargetId = siteId,
                        RelationshipType = "DC_LOCATED_IN_SITE",
                        Style = "dashed"
                    });
                }

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-dc-{SlugDc(dcName)}-domain",
                    SourceId = dcId,
                    TargetId = domainId,
                    RelationshipType = "DC_MEMBER_OF_DOMAIN"
                });
            }

            foreach (var node in response.Nodes.Where(node => node.Type == "SITE"))
            {
                if (siteDcIds.TryGetValue(node.Id, out var childIds))
                {
                    node.ChildIds = childIds;
                }
            }

            foreach (var refDc in placeholderDcs)
            {
                var fqdn = !string.IsNullOrWhiteSpace(refDc.Fqdn) ? refDc.Fqdn : refDc.Name;
                var siteName = refDc.SiteName;
                var siteId = siteIdsByName.TryGetValue(siteName, out var sid) ? sid : null;
                
                if (siteId == null) continue;

                var domainId = ResolveDcDomainId(fqdn, domainIdsByName, primaryDomainId, forestId);
                var dcId = $"node-dc-{SlugDc(refDc.Name)}";
                var siteOrdinal = string.IsNullOrWhiteSpace(siteName) ? 0 : dcIndexBySite.GetValueOrDefault(siteName);
                if (!string.IsNullOrWhiteSpace(siteName))
                {
                    dcIndexBySite[siteName] = siteOrdinal + 1;
                }

                dcIdsByFqdn[fqdn] = dcId;
                dcIdsByName[refDc.Name] = dcId;
                dcFqdnsById[dcId] = fqdn;

                if (siteId != null)
                {
                    siteDcIds.TryAdd(siteId, []);
                    siteDcIds[siteId].Add(dcId);
                    
                    var siteNode = response.Nodes.FirstOrDefault(n => n.Id == siteId);
                    if (siteNode != null && !siteNode.ChildIds.Contains(dcId))
                    {
                        siteNode.ChildIds.Add(dcId);
                    }
                }
                else
                {
                    var domainNode = response.Nodes.FirstOrDefault(n => n.Id == domainId);
                    if (domainNode != null && !domainNode.ChildIds.Contains(dcId))
                    {
                        domainNode.ChildIds.Add(dcId);
                    }
                }

                var meta = new Dictionary<string, object>
                {
                    { "fqdn", fqdn },
                    { "ipAddress", "Unknown" },
                    { "parentDomainId", domainId },
                    { "fsmoRoles", Array.Empty<string>() },
                    { "isGlobalCatalog", refDc.Source == "GlobalCatalog" },
                    { "isReadOnly", false },
                    { "operatingSystem", "Unknown" },
                    { "operatingSystemVersion", "Unknown" },
                    { "uptimeHours", 0 },
                    { "isPlaceholder", true },
                    { "isDiscovered", false },
                    { "discoveryStatus", "Unreachable" },
                    { "source", refDc.Source },
                    { "attributes", new Dictionary<string, object>
                        {
                            { "status", "Unreachable" },
                            { "statusMessage", "Referenced in AD metadata but could not be contacted." },
                            { "source", refDc.Source }
                        }
                    },
                    { "statusMessage", "Referenced in AD metadata but could not be contacted." }
                };

                if (siteId != null)
                {
                    meta["parentSiteId"] = siteId;
                }

                response.Nodes.Add(new GraphNode
                {
                    Id = dcId,
                    Label = refDc.Name,
                    Type = "DOMAIN_CONTROLLER",
                    Health = "CRITICAL",
                    ParentId = siteId ?? domainId,
                    Depth = 3,
                    Meta = meta
                });

                if (siteId != null)
                {
                    response.Edges.Add(new GraphEdge
                    {
                        Id = $"edge-dc-{SlugDc(refDc.Name)}-site",
                        SourceId = dcId,
                        TargetId = siteId,
                        RelationshipType = "DC_LOCATED_IN_SITE",
                        Style = "dashed"
                    });
                }

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-dc-{SlugDc(refDc.Name)}-domain",
                    SourceId = dcId,
                    TargetId = domainId,
                    RelationshipType = "DC_MEMBER_OF_DOMAIN"
                });
            }



            AddSubnetNodesAndEdges(response, subnets, siteIdsByName);

            AddReplicationPartnerEdges(response, replicationPartners, replicationConnections, dcIdsByFqdn, dcIdsByName, dcFqdnsById);
            AddReplicationFailureEdges(response, replicationFailures, dcIdsByFqdn, dcFqdnsById);
            AddReplicationConnectionEdges(response, replicationConnections, dcIdsByName, dcFqdnsById);


            response.GeneratedAt = DateTime.Now;
            return response;
        }

        private static void AddReplicationConnectionEdges(GraphResponse response, List<JsonElement> replicationConnections, Dictionary<string, string> dcIdsByName, Dictionary<string, string> dcFqdnsById)
        {
            foreach (var conn in replicationConnections)
            {
                var sourceDC = GetString(conn, "SourceDC");
                var targetDC = GetString(conn, "TargetDC");

                if (dcIdsByName.TryGetValue(sourceDC, out var sourceId) && dcIdsByName.TryGetValue(targetDC, out var targetId))
                {
                    if (response.Edges.Any(e => e.SourceId == targetId && e.TargetId == sourceId && e.RelationshipType == "REPLICATION_LINK"))
                    {
                        continue;
                    }

                    var type = GetString(conn, "ConnectionType");
                    var status = GetString(conn, "EnabledConnection").Equals("True", StringComparison.OrdinalIgnoreCase) ? "Healthy" : "Disabled";
                    var name = GetString(conn, "ConnectionName");
                    var transport = GetString(conn, "TransportProtocol");
                    
                    var contextsArray = GetArray(conn, "ReplicatedNamingContexts");
                    var contexts = contextsArray.Select(s => s.ValueKind == JsonValueKind.String ? s.GetString() : (s.ValueKind == JsonValueKind.Object ? GetString(s, "Value") : "")).Where(s => !string.IsNullOrEmpty(s)).ToList();
                    
                    response.Edges.Add(new GraphEdge
                    {
                        Id = $"edge-kcc-{SlugDc(name)}-{targetId}-{sourceId}",
                        SourceId = targetId, // DestinationDC
                        TargetId = sourceId, // SourceDC
                        RelationshipType = "REPLICATION_LINK",
                        Style = status == "Healthy" ? "solid" : "dashed",
                        ReplicationData = new
                        {
                            connectionType = type,
                            transportProtocol = transport,
                            status = status == "Healthy" ? "UNKNOWN" : "DISABLED", // Align status values with frontend expectations
                            name = name,
                            replicatedNamingContexts = contexts,
                            created = GetString(conn, "Created"),
                            modified = GetString(conn, "Modified"),
                            
                            latencySeconds = 0,
                            latencyMinutes = 0,
                            lastSuccessAt = (string?)null,
                            lastAttemptAt = (string?)null,
                            failureCount = 0,
                            sourceDcFqdn = dcFqdnsById.TryGetValue(targetId, out var srcFqdn) ? srcFqdn : targetDC, // Remember sourceId/targetId were swapped!
                            targetDcFqdn = dcFqdnsById.TryGetValue(sourceId, out var tgtFqdn) ? tgtFqdn : sourceDC,
                            errorMessage = (string?)null,
                            statusMessage = status == "Healthy" ? "Configured in topology, but active replication hasn't occurred yet" : "Topology connection disabled"
                        }
                    });
                }
            }
        }

        private static void AddSubnetNodesAndEdges(GraphResponse response,List<JsonElement> subnets,Dictionary<string, string> siteIdsByName)
        {
            foreach (var subnet in subnets)
            {
                var cidr = GetString(subnet, "Name");
                var siteName = NormalizeSiteName(GetString(subnet, "Site"));
                if (string.IsNullOrWhiteSpace(cidr) || !siteIdsByName.TryGetValue(siteName, out var siteId))
                {
                    continue;
                }

                var subnetId = $"node-subnet-{SlugSubnet(cidr)}";
                var siteNode = response.Nodes.FirstOrDefault(node => node.Id == siteId);
                if (siteNode != null && !siteNode.ChildIds.Contains(subnetId))
                {
                    siteNode.ChildIds.Add(subnetId);
                }

                response.Nodes.Add(new GraphNode
                {
                    Id = subnetId,
                    Label = cidr,
                    Type = "SUBNET",
                    Health = "HEALTHY",
                    ParentId = siteId,
                    Depth = 3,
                    Meta = new Dictionary<string, object>
                    {
                        { "cidr", cidr },
                        { "parentSiteId", siteId },
                        { "location", GetString(subnet, "Location") },
                        { "description", GetString(subnet, "Description") }
                    }
                });

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-subnet-{SlugSubnet(cidr)}-site",
                    SourceId = subnetId,
                    TargetId = siteId,
                    RelationshipType = "SUBNET_BELONGS_TO_SITE"
                });
            }
        }

        private static void AddReplicationPartnerEdges(GraphResponse response,List<JsonElement> replicationPartners,List<JsonElement> replicationConnections,Dictionary<string, string> dcIdsByFqdn,Dictionary<string, string> dcIdsByName,Dictionary<string, string> dcFqdnsById)
        {
            foreach (var partner in replicationPartners)
            {
                var sourceFqdn = GetString(partner, "DomainController");
                var partnerDn = GetString(partner, "ReplicationPartner");
                if (string.IsNullOrWhiteSpace(sourceFqdn) || partnerDn.StartsWith("Unable to collect", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sourceDcName = GetString(partner, "SourceDCName");
                var targetName = !string.IsNullOrWhiteSpace(sourceDcName) && !sourceDcName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                    ? sourceDcName
                    : ExtractDcNameFromPartner(partnerDn);
                if (!dcIdsByFqdn.TryGetValue(sourceFqdn, out var sourceId) || !dcIdsByName.TryGetValue(targetName, out var targetId))
                {
                    continue;
                }

                var latencyMinutes = GetDouble(partner, "LatencyMinutes");
                if (latencyMinutes == 0) latencyMinutes = GetDouble(partner, "ReplicationLatencyMinutes"); // fallback
                
                var failureCount = GetInt(partner, "ConsecutiveFailures");
                
                var rawStatus = GetString(partner, "Health");
                if (string.IsNullOrWhiteSpace(rawStatus)) rawStatus = GetString(partner, "ReplicationStatus", GetString(partner, "PartnerStatus"));
                var status = GetReplicationStatus(latencyMinutes, failureCount, rawStatus);
                
                var replStatusMessage = status switch
                {
                    "FAILED" => $"Replication has failed (consecutive failures: {failureCount})",
                    "CRITICAL" => $"Replication latency is critical ({Math.Round(latencyMinutes)} minutes)",
                    "HIGH WARNING" => $"Replication latency is high warning ({Math.Round(latencyMinutes)} minutes)",
                    "WARNING" => $"Replication latency is warning ({Math.Round(latencyMinutes)} minutes)",
                    "UNKNOWN" => "Replication latency is unknown",
                    _ => "Replication is healthy"
                };
                var sourceLabel = SlugDc(response.Nodes.FirstOrDefault(node => node.Id == sourceId)?.Label ?? sourceFqdn);
                var targetLabel = SlugDc(response.Nodes.FirstOrDefault(node => node.Id == targetId)?.Label ?? targetName);

                // Attempt to find the matching KCC connection to merge topology data into this active partner edge.
                // In ReplicationPartner: Destination is sourceId, Source is targetId.
                // In ReplicationConnection: TargetDC is Destination, SourceDC is Source.
                var matchingConnection = replicationConnections.FirstOrDefault(conn =>
                {
                    var kccSourceDC = GetString(conn, "SourceDC");
                    var kccTargetDC = GetString(conn, "TargetDC");
                    return (dcIdsByName.TryGetValue(kccSourceDC, out var kSourceId) && kSourceId == targetId) &&
                           (dcIdsByName.TryGetValue(kccTargetDC, out var kTargetId) && kTargetId == sourceId);
                });

                var connectionMatched = matchingConnection.ValueKind != JsonValueKind.Undefined;
                var connectionType = connectionMatched ? GetString(matchingConnection, "ConnectionType") : GetBool(partner, "IsIntersite") ? "InterSite" : "IntraSite";
                var transportProtocol = connectionMatched ? GetString(matchingConnection, "TransportProtocol") : "RPC";
                var connName = connectionMatched ? GetString(matchingConnection, "ConnectionName") : null;
                var connCreated = matchingConnection.ValueKind != JsonValueKind.Undefined ? GetString(matchingConnection, "Created") : null;
                var connModified = matchingConnection.ValueKind != JsonValueKind.Undefined ? GetString(matchingConnection, "Modified") : null;
                var contexts = connectionMatched
                    ? GetArray(matchingConnection, "ReplicatedNamingContexts")
                        .Select(s => s.ValueKind == JsonValueKind.String ? s.GetString() : (s.ValueKind == JsonValueKind.Object ? GetString(s, "Value") : ""))
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(s => s!)
                        .ToList()
                    : new[] { GetString(partner, "NamingContext", GetString(partner, "Partition")) }
                        .Where(context => !string.IsNullOrWhiteSpace(context))
                        .ToList();
                var namingContext = GetString(partner, "NamingContext", GetString(partner, "Partition"));
                var contextDetails = contexts.Select(BuildNamingContextInfo).ToList();

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-repl-{sourceLabel}-{targetLabel}",
                    SourceId = sourceId,
                    TargetId = targetId,
                    RelationshipType = "REPLICATION_LINK",
                    Style = GetReplicationStyle(status),
                    ReplicationData = new
                    {
                        connectionMatched,
                        connectionType = connectionType,
                        transportProtocol = transportProtocol,
                        name = connName,
                        replicatedNamingContexts = contexts,
                        replicatedNamingContextDetails = contextDetails,
                        created = connCreated,
                        modified = connModified,
                        sourceSite = GetString(partner, "SourceSite"),
                        destinationSite = GetString(partner, "DestinationSite", GetString(partner, "SiteName")),
                        partnerType = GetString(partner, "PartnerType"),
                        namingContext,
                        namingContextDetails = BuildNamingContextInfo(namingContext),
                        replicationScope = new
                        {
                            configuredPartitionCount = contexts.Count,
                            configuredPartitions = contextDetails,
                            metricPartition = BuildNamingContextInfo(namingContext),
                            connectionObjectMatched = connectionMatched,
                            connectionObjectStatus = connectionMatched ? "Matched" : "Not matched",
                            displaySummary = contexts.Count == 1
                                ? $"Replicating {BuildNamingContextLabel(contexts[0])}"
                                : $"Replicating {contexts.Count} directory partitions"
                        },

                        latencySeconds = (int)(latencyMinutes * 60),
                        latencyMinutes = latencyMinutes,
                        lastSuccessAt = GetNullableString(partner, "LastReplicationSuccess"),
                        lastAttemptAt = GetNullableString(partner, "LastReplicationAttempt"),
                        failureCount,
                        status,
                        sourceDcFqdn = sourceFqdn,
                        targetDcFqdn = dcFqdnsById.TryGetValue(targetId, out var fqdn) ? fqdn : targetName,
                        errorMessage = (status == "WARNING" || status == "HIGH WARNING" || status == "CRITICAL") ? $"Replication latency: {Math.Round(latencyMinutes)} minutes" : null,
                        statusMessage = replStatusMessage
                    }
                });
            }
        }

        private static void AddReplicationFailureEdges(GraphResponse response,List<JsonElement> replicationFailures,Dictionary<string, string> dcIdsByFqdn,Dictionary<string, string> dcFqdnsById)
        {
            var fallbackTargetId = response.Nodes.FirstOrDefault(node => node.Type == "DOMAIN_CONTROLLER" && node.Label.StartsWith("HQ", StringComparison.OrdinalIgnoreCase))?.Id
                ?? response.Nodes.FirstOrDefault(node => node.Type == "DOMAIN_CONTROLLER")?.Id;

            foreach (var failure in replicationFailures)
            {
                var status = GetString(failure, "Status");
                if (status.Equals("Healthy", StringComparison.OrdinalIgnoreCase) || status.Equals("Historical Failure", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sourceFqdn = GetString(failure, "DomainController");
                if (!dcIdsByFqdn.TryGetValue(sourceFqdn, out var sourceId) || fallbackTargetId == null || sourceId == fallbackTargetId)
                {
                    continue;
                }

                var sourceLabel = SlugDc(response.Nodes.FirstOrDefault(node => node.Id == sourceId)?.Label ?? sourceFqdn);
                var targetFqdn = dcFqdnsById.GetValueOrDefault(fallbackTargetId, "");

                response.Edges.Add(new GraphEdge
                {
                    Id = $"edge-repl-{sourceLabel}-fail",
                    SourceId = sourceId,
                    TargetId = fallbackTargetId,
                    RelationshipType = "REPLICATION_LINK",
                    Style = "pulsing-error",
                    ReplicationData = new
                    {
                        latencySeconds = 0,
                        lastSuccessAt = (string?)null,
                        lastAttemptAt = GetNullableString(failure, "FirstFailureTime"),
                        failureCount = GetInt(failure, "FailureCount", 1) == 0 ? 1 : GetInt(failure, "FailureCount", 1),
                        status = "FAILED",
                        statusMessage = "Replication has critically failed with no recent successful sync",
                        sourceDcFqdn = sourceFqdn,
                        targetDcFqdn = targetFqdn,
                        errorMessage = GetWin32ErrorMessage(GetInt(failure, "LastError"), GetString(failure, "LastError", "Replication failure found."))
                    }
                });
            }
        }

        private static List<object> BuildSiteLinks(string siteName, List<JsonElement> siteLinks)
        {
            var links = new List<object>();
            foreach (var link in siteLinks)
            {
                if (!GetSiteNamesFromSiteLink(link).Contains(siteName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var linkName = GetString(link, "SiteLinkName");
                if (linkName.StartsWith("HQ_2_", StringComparison.OrdinalIgnoreCase) && siteName.Equals("HQ_Office", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                links.Add(new
                {
                    siteLinkName = linkName,
                    cost = GetInt(link, "Cost"),
                    replicationIntervalMinutes = GetInt(link, "ReplicationIntervalMinutes"),
                    scheduleExists = GetString(link, "ScheduleExists", GetBool(link, "ScheduleExistsBool") ? "Yes" : "No"),
                    status = GetString(link, "Status"),
                    statusReason = GetString(link, "StatusReason")
                });
            }

            return links;
        }

        private static SiteLinkSummary BuildSiteLinkSummary(string siteName, List<JsonElement> siteLinks)
        {
            var links = siteLinks
                .Where(link => GetSiteNamesFromSiteLink(link).Contains(siteName, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var intervals = links
                .Select(link => GetInt(link, "ReplicationIntervalMinutes"))
                .Where(interval => interval > 0)
                .ToList();
            var costs = links
                .Select(link => GetInt(link, "Cost"))
                .Where(cost => cost > 0)
                .ToList();

            return new SiteLinkSummary
            {
                Total = links.Count,
                Healthy = links.Count(link => GetString(link, "Status").Equals("Healthy", StringComparison.OrdinalIgnoreCase)),
                Warning = links.Count(link => GetString(link, "Status").Equals("Warning", StringComparison.OrdinalIgnoreCase)),
                Informational = links.Count(link => GetString(link, "Status").Equals("Informational", StringComparison.OrdinalIgnoreCase)),
                CustomSchedule = links.Count(link => GetBool(link, "ScheduleExistsBool") || GetString(link, "ScheduleExists").Equals("Yes", StringComparison.OrdinalIgnoreCase)),
                LowestCost = costs.Count > 0 ? costs.Min() : null,
                FastestIntervalMinutes = intervals.Count > 0 ? intervals.Min() : null,
                SlowestIntervalMinutes = intervals.Count > 0 ? intervals.Max() : null
            };
        }

        private static object BuildNamingContextInfo(string distinguishedName)
        {
            var type = GetNamingContextType(distinguishedName);

            return new
            {
                distinguishedName,
                type,
                label = BuildNamingContextLabel(distinguishedName),
                description = type switch
                {
                    "Domain" => "Domain partition containing domain objects such as users, groups, computers, OUs, and GPO links.",
                    "Configuration" => "Forest-wide configuration partition containing sites, services, and replication topology.",
                    "Schema" => "Forest schema partition defining AD object classes and attributes.",
                    "ForestDnsZones" => "Forest-wide DNS application partition.",
                    "DomainDnsZones" => "Domain DNS application partition.",
                    _ => "Active Directory naming context."
                }
            };
        }

        private static string BuildNamingContextLabel(string distinguishedName)
        {
            var type = GetNamingContextType(distinguishedName);
            var dnsName = DnToDnsName(distinguishedName);

            return type switch
            {
                "Domain" when !string.IsNullOrWhiteSpace(dnsName) => $"Domain: {dnsName}",
                "Configuration" when !string.IsNullOrWhiteSpace(dnsName) => $"Configuration: {dnsName}",
                "Schema" when !string.IsNullOrWhiteSpace(dnsName) => $"Schema: {dnsName}",
                "ForestDnsZones" when !string.IsNullOrWhiteSpace(dnsName) => $"Forest DNS Zones: {dnsName}",
                "DomainDnsZones" when !string.IsNullOrWhiteSpace(dnsName) => $"Domain DNS Zones: {dnsName}",
                _ => string.IsNullOrWhiteSpace(distinguishedName) ? "Unknown partition" : distinguishedName
            };
        }

        private static string GetNamingContextType(string distinguishedName)
        {
            if (string.IsNullOrWhiteSpace(distinguishedName)) return "Unknown";
            if (distinguishedName.StartsWith("CN=Schema,CN=Configuration,", StringComparison.OrdinalIgnoreCase)) return "Schema";
            if (distinguishedName.StartsWith("CN=Configuration,", StringComparison.OrdinalIgnoreCase)) return "Configuration";
            if (distinguishedName.StartsWith("DC=ForestDnsZones,", StringComparison.OrdinalIgnoreCase)) return "ForestDnsZones";
            if (distinguishedName.StartsWith("DC=DomainDnsZones,", StringComparison.OrdinalIgnoreCase)) return "DomainDnsZones";
            if (Regex.IsMatch(distinguishedName, @"^(DC=[^,]+,?)+$", RegexOptions.IgnoreCase)) return "Domain";
            return "Other";
        }

        private static string DnToDnsName(string distinguishedName)
        {
            if (string.IsNullOrWhiteSpace(distinguishedName)) return string.Empty;

            return string.Join(".",
                Regex.Matches(distinguishedName, @"(?:^|,)DC=([^,]+)", RegexOptions.IgnoreCase)
                    .Select(match => match.Groups[1].Value)
                    .Where(part => !part.Equals("DomainDnsZones", StringComparison.OrdinalIgnoreCase) &&
                                   !part.Equals("ForestDnsZones", StringComparison.OrdinalIgnoreCase)));
        }

        private static List<object> BuildPortStatus(string fqdn, List<JsonElement> ports)
        {
            return ports
                .Where(port => GetString(port, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase))
                .Select(port => new
                {
                    service = GetString(port, "Service"),
                    port = GetInt(port, "Port"),
                    open = GetBool(port, "Open")
                })
                .Cast<object>()
                .ToList();
        }

        private static PortSummary BuildPortSummary(string fqdn, List<JsonElement> ports)
        {
            var dcPorts = ports
                .Where(port => GetString(port, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var closedServices = dcPorts
                .Where(port => !GetBool(port, "Open"))
                .Select(port => GetString(port, "Service"))
                .Where(service => !string.IsNullOrWhiteSpace(service))
                .ToList();

            return new PortSummary
            {
                Total = dcPorts.Count,
                Open = dcPorts.Count(port => GetBool(port, "Open")),
                Closed = closedServices.Count,
                AllOpen = dcPorts.Count > 0 && closedServices.Count == 0,
                ClosedServices = closedServices
            };
        }

        private static List<object> BuildDiagTests(string fqdn, List<JsonElement> dcDiagSummary)
        {
            var diagIssue = dcDiagSummary.FirstOrDefault(diag => GetString(diag, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase));
            if (diagIssue.ValueKind == JsonValueKind.Undefined)
            {
                return [];
            }

            return
            [
                new { name = GetString(diagIssue, "Category", "Overall Health"), status = GetString(diagIssue, "Status", "Unknown") }
            ];
        }

        private static Dictionary<string, object> BuildDcAttributes(string fqdn, string health, string dcStatusMessage, List<JsonElement> failures, JsonElement? repadminSummary)
        {
            var failure = failures.FirstOrDefault(item => GetString(item, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) && GetString(item, "Status").Equals("Failure Found", StringComparison.OrdinalIgnoreCase));
            var repadminError = GetArray(repadminSummary, "OperationalErrors").FirstOrDefault(item => GetString(item, "Server").Equals(fqdn, StringComparison.OrdinalIgnoreCase));

            return CompactAttributes(new Dictionary<string, object?>
            {
                { "status", health == "CRITICAL" ? "Offline / Unreachable" : health == "WARNING" ? "Online with warnings" : "Online" },
                { "statusMessage", dcStatusMessage },
                { "errorMessage", failure.ValueKind == JsonValueKind.Undefined ? null : GetString(failure, "LastError") },
                { "repadminErrorCode", repadminError.ValueKind == JsonValueKind.Undefined ? null : GetInt(repadminError, "ErrorCode") }
            });
        }

        private static Dictionary<string, object> CompactAttributes(Dictionary<string, object?> attributes)
        {
            return attributes
                .Where(attribute => attribute.Value != null && (attribute.Value is not string text || !string.IsNullOrWhiteSpace(text)))
                .ToDictionary(attribute => attribute.Key, attribute => attribute.Value!);
        }

        private static (string Health, string Reason) GetForestHealth(JsonElement? repadminSummary, JsonElement? replicationHealthSummary, List<JsonElement> domains)
        {
            var failedDomain = domains.FirstOrDefault(domain => !IsOkStatus(GetString(domain, "Status", "OK")));
            if (failedDomain.ValueKind != JsonValueKind.Undefined)
            {
                return ("WARNING", $"Domain '{GetString(failedDomain, "DomainName")}' is reporting an error state.");
            }

            if (replicationHealthSummary.HasValue)
            {
                if (!GetBool(replicationHealthSummary, "Healthy", true))
                {
                    return ("WARNING", "Active replication failures or operational errors detected in the forest.");
                }
            }
            else if (!GetBool(repadminSummary, "Healthy", true))
            {
                return ("WARNING", "Repadmin reports operational errors in the forest.");
            }

            return ("HEALTHY", "Forest is fully operational with no known issues.");
        }

        private static (string Health, string Reason) GetSiteHealth(string siteName, List<JsonElement> failures)
        {
            var failure = failures.FirstOrDefault(f => GetString(f, "SiteName").Equals(siteName, StringComparison.OrdinalIgnoreCase) && GetString(f, "Status").Equals("Failure Found", StringComparison.OrdinalIgnoreCase));
            if (failure.ValueKind != JsonValueKind.Undefined)
            {
                var targetDc = ExtractDcNameFromPartner(GetString(failure, "Partner"));
                var errMsg = GetWin32ErrorMessage(GetInt(failure, "LastError"), GetString(failure, "LastError"));
                return ("CRITICAL", $"Replication failure between {GetString(failure, "Server")} and {targetDc}: {errMsg}");
            }

            return ("HEALTHY", "Site is operational with no replication failures.");
        }

        private static (string Health, string Reason) GetDcHealth(string fqdn, List<JsonElement> failures, JsonElement? repadminSummary, List<JsonElement> dcDiagSummary)
        {
            var failure = failures.FirstOrDefault(f => GetString(f, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) && GetString(f, "Status").Equals("Failure Found", StringComparison.OrdinalIgnoreCase));
            if (failure.ValueKind != JsonValueKind.Undefined)
            {
                var errMsg = GetWin32ErrorMessage(GetInt(failure, "LastError"), GetString(failure, "LastError"));
                return ("CRITICAL", $"Replication failure: {errMsg}");
            }

            var repError = GetArray(repadminSummary, "OperationalErrors").FirstOrDefault(error => GetString(error, "Server").Equals(fqdn, StringComparison.OrdinalIgnoreCase));
            if (repError.ValueKind != JsonValueKind.Undefined)
            {
                var errCode = GetInt(repError, "ErrorCode");
                return ("WARNING", $"Repadmin operational error: {GetWin32ErrorMessage(errCode, errCode.ToString())}");
            }

            var diagIssue = dcDiagSummary.FirstOrDefault(diag => GetString(diag, "DomainController").Equals(fqdn, StringComparison.OrdinalIgnoreCase) && !GetString(diag, "Status").Equals("Healthy", StringComparison.OrdinalIgnoreCase));
            if (diagIssue.ValueKind != JsonValueKind.Undefined)
            {
                return (GetString(diagIssue, "Status", "WARNING").ToUpperInvariant(), GetString(diagIssue, "Details", "DcDiag detected an issue."));
            }

            return ("HEALTHY", "Domain controller is online and healthy.");
        }

        private static string GetReplicationStatus(double latencyMinutes, int failureCount, string rawStatus)
        {
            if (failureCount > 0 || rawStatus.Equals("FAIL", StringComparison.OrdinalIgnoreCase) || rawStatus.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return "FAILED";
            }

            if (rawStatus.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase)) return "CRITICAL";
            if (rawStatus.Equals("HIGH WARNING", StringComparison.OrdinalIgnoreCase)) return "HIGH WARNING";
            if (rawStatus.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || latencyMinutes > 60) return "WARNING";
            if (rawStatus.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)) return "UNKNOWN";

            return "HEALTHY";
        }

        private static string GetReplicationStyle(string status)
        {
            return status switch
            {
                "FAILED" or "CRITICAL" => "pulsing-error",
                "WARNING" or "HIGH WARNING" => "bold-warning",
                _ => "solid"
            };
        }

        private static List<string> GetFsmoRoles(JsonElement dc)
        {
            if (!dc.TryGetProperty("OperationMasterRoles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return roles.EnumerateArray()
                .Select(role => role.ValueKind == JsonValueKind.Object ? GetString(role, "Value") : role.GetString() ?? "")
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(ToUpperSnake)
                .ToList();
        }

        private static string ResolveSiteDomainId(string siteName, Dictionary<string, string> domainIdsByName, string primaryDomainId, string forestId, List<JsonElement> domainControllers)
        {
            var siteDcs = domainControllers.Where(dc => GetString(dc, "Site").Equals(siteName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (siteDcs.Count > 0)
            {
                var topDomainHost = siteDcs.Select(dc => GetNullableString(dc, "HostName"))
                                           .Where(h => !string.IsNullOrEmpty(h) && h.Contains('.'))
                                           .Select(h => h.Substring(h.IndexOf('.') + 1))
                                           .GroupBy(d => d)
                                           .OrderByDescending(g => g.Count())
                                           .Select(g => g.Key)
                                           .FirstOrDefault();
                                           
                if (!string.IsNullOrEmpty(topDomainHost))
                {
                    var matchedDomain = domainIdsByName.Keys.FirstOrDefault(k => string.Equals(k, topDomainHost, StringComparison.OrdinalIgnoreCase));
                    if (matchedDomain != null) return domainIdsByName[matchedDomain];
                }
            }

            if (siteName.Contains("uk", StringComparison.OrdinalIgnoreCase))
            {
                var ukDomain = domainIdsByName.FirstOrDefault(domain => domain.Key.Contains("uk", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(ukDomain.Value))
                {
                    return ukDomain.Value;
                }
            }

            if (domainIdsByName.Values.Contains(primaryDomainId))
            {
                return primaryDomainId;
            }

            return forestId;
        }

        private static string ResolveDcDomainId(string fqdn, Dictionary<string, string> domainIdsByName, string primaryDomainId, string forestId)
        {
            var domainName = GetDomainFromFqdn(fqdn);
            if (domainIdsByName.TryGetValue(domainName, out var domainId))
            {
                return domainId;
            }
            
            return domainIdsByName.Values.Contains(primaryDomainId) ? primaryDomainId : forestId;
        }

        private static string ExtractDcNameFromPartner(string partnerDn)
        {
            var parts = partnerDn.Split(',');
            if (parts.Length > 1 && parts[1].StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1][3..];
            }

            return partnerDn;
        }

        private static IEnumerable<string> GetSiteNamesFromSiteLink(JsonElement link)
        {
            if (link.TryGetProperty("SitesIncludedArray", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in array.EnumerateArray())
                {
                    yield return NormalizeSiteName(site.GetString());
                }
            }

            var sitesIncluded = GetString(link, "SitesIncluded");
            foreach (var siteDn in Regex.Matches(sitesIncluded, @"CN=([^,]+),CN=Sites", RegexOptions.IgnoreCase).Select(match => match.Groups[1].Value))
            {
                yield return NormalizeSiteName(siteDn);
            }
        }

        private static string NormalizeSiteName(string? site)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return string.Empty;
            }

            var value = site.Trim();
            return value.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
                ? value.Split(',')[0].Replace("CN=", "", StringComparison.OrdinalIgnoreCase).Trim()
                : value;
        }

        private static string GetDomainFromFqdn(string fqdn)
        {
            var firstDot = fqdn.IndexOf('.');
            return firstDot < 0 ? string.Empty : fqdn[(firstDot + 1)..];
        }

        private static string SlugRoot(string value)
        {
            var first = value.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
            return Slug(first);
        }

        private static string SlugDc(string value)
        {
            return Slug(value.Replace("-DC", "-", StringComparison.OrdinalIgnoreCase).Replace("DC", "", StringComparison.OrdinalIgnoreCase));
        }

        private static string SlugSubnet(string value)
        {
            return Slug(value.Replace("/", "-", StringComparison.OrdinalIgnoreCase));
        }

        private static string Slug(string value)
        {
            return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        }

        private static string ToUpperSnake(string value)
        {
            return Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
        }

        private static string GuessNetbios(string domainName)
        {
            return (domainName.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? domainName).ToUpperInvariant();
        }

        private static string GetWin32ErrorMessage(int errorCode, string fallback)
        {
            if (errorCode <= 0) return fallback;

            var adError = errorCode switch
            {
                5 => "Access is denied.",
                49 => "Invalid credentials.",
                1256 => "The remote system is not available.",
                1396 => "Logon Failure: The target account name is incorrect.",
                1722 => "The RPC server is unavailable.",
                1753 => "There are no more endpoints available from the endpoint mapper.",
                1908 => "Could not find the domain controller for this domain.",
                8446 => "The replication operation failed to allocate memory.",
                8451 => "The replication operation encountered a database error.",
                8453 => "Replication access was denied.",
                8524 => "The DSA operation is unable to proceed because of a DNS lookup failure.",
                8589 => "The DS cannot derive a service principal name (SPN) with which to mutually authenticate the target server.",
                8606 => "Insufficient attributes were given to create an object.",
                8614 => "Active Directory cannot replicate with this server because the time since the last replication with this server has exceeded the tombstone lifetime.",
                _ => null
            };

            if (adError != null) return adError;

            try
            {
                var message = new System.ComponentModel.Win32Exception(errorCode).Message;
                return string.IsNullOrWhiteSpace(message) || message.Contains("Unknown error", StringComparison.OrdinalIgnoreCase) ? fallback : message;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsOkStatus(string status)
        {
            return string.IsNullOrWhiteSpace(status) || status.Equals("OK", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ReferencedDc> GetReferencedDcs(JsonElement? forestInfo, JsonElement? repadminSummary, List<JsonElement> replicationFailures, List<JsonElement> replicationPartners, List<JsonElement> configurationServers)
        {
            var dict = new Dictionary<string, ReferencedDc>(StringComparer.OrdinalIgnoreCase);

            foreach (var server in configurationServers)
            {
                var name = GetString(server, "Name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var fqdn = GetNullableString(server, "HostName") ?? "";
                var site = GetNullableString(server, "Site") ?? "";
                dict[name] = new ReferencedDc { Name = name, Fqdn = fqdn, SiteName = site, Source = "ConfigurationPartition" };
            }

            var globalCatalogs = GetArray(forestInfo, "GlobalCatalogs");
            foreach (var gc in globalCatalogs)
            {
                var fqdn = gc.ValueKind == JsonValueKind.String ? gc.GetString() : null;
                if (string.IsNullOrWhiteSpace(fqdn)) continue;
                var name = fqdn.Split('.')[0];
                dict[name] = new ReferencedDc { Name = name, Fqdn = fqdn, Source = "GlobalCatalog" };
            }

            foreach (var dsa in GetArray(repadminSummary, "SourceDSA").Concat(GetArray(repadminSummary, "DestinationDSA")))
            {
                var name = GetString(dsa, "Server");
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!dict.TryGetValue(name, out var refDc))
                {
                    refDc = new ReferencedDc { Name = name, Source = "Repadmin" };
                    dict[name] = refDc;
                }
            }

            foreach (var failure in replicationFailures)
            {
                var partner = GetString(failure, "Partner");
                if (!string.IsNullOrWhiteSpace(partner) && partner.StartsWith("CN=NTDS Settings,CN=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = partner.Split(',');
                    if (parts.Length >= 4)
                    {
                        var name = parts[1].Substring(3); // Remove CN=
                        var site = parts[3].Substring(3); // Remove CN=
                        
                        if (!dict.TryGetValue(name, out var refDc))
                        {
                            refDc = new ReferencedDc { Name = name, Source = "ReplicationFailure" };
                            dict[name] = refDc;
                        }
                        
                        if (string.IsNullOrWhiteSpace(refDc.SiteName))
                        {
                            refDc.SiteName = site;
                        }
                    }
                }
            }

            foreach (var partner in replicationPartners)
            {
                var partnerDn = GetString(partner, "ReplicationPartner");
                if (!string.IsNullOrWhiteSpace(partnerDn) && partnerDn.StartsWith("CN=NTDS Settings,CN=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = partnerDn.Split(',');
                    if (parts.Length >= 4)
                    {
                        var name = parts[1].Substring(3); // Remove CN=
                        var site = parts[3].Substring(3); // Remove CN=
                        
                        if (!dict.TryGetValue(name, out var refDc))
                        {
                            refDc = new ReferencedDc { Name = name, Source = "ReplicationPartner" };
                            dict[name] = refDc;
                        }
                        
                        if (string.IsNullOrWhiteSpace(refDc.SiteName))
                        {
                            refDc.SiteName = site;
                        }
                    }
                }
            }

            return dict.Values.ToList();
        }

        private static JsonElement? GetObject(JsonElement root, string property)
        {
            return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;
        }

        private static List<JsonElement> GetArray(JsonElement? root, string property)
        {
            if (!root.HasValue || !root.Value.TryGetProperty(property, out var value))
            {
                return [];
            }

            return value.ValueKind switch
            {
                JsonValueKind.Array => value.EnumerateArray().ToList(),
                JsonValueKind.Object => [value],
                _ => []
            };
        }

        private static int GetArrayLength(JsonElement? root, string property)
        {
            return GetArray(root, property).Count;
        }

        private static string GetString(JsonElement? root, string property, string fallback = "")
        {
            if (!root.HasValue || !root.Value.TryGetProperty(property, out var value))
            {
                return fallback;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? fallback,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Object when value.TryGetProperty("Value", out var enumValue) => enumValue.GetString() ?? fallback,
                _ => fallback
            };
        }

        private static string? GetNullableString(JsonElement root, string property)
        {
            var value = GetString(root, property);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static int GetInt(JsonElement? root, string property, int fallback = 0)
        {
            if (!root.HasValue || !root.Value.TryGetProperty(property, out var value))
            {
                return fallback;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            return int.TryParse(GetString(root, property), out var parsed) ? parsed : fallback;
        }

        private static double GetDouble(JsonElement? root, string property, double fallback = 0)
        {
            if (!root.HasValue || !root.Value.TryGetProperty(property, out var value))
            {
                return fallback;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue))
            {
                return doubleValue;
            }

            return double.TryParse(GetString(root, property), out var parsed) ? parsed : fallback;
        }

        private static bool GetBool(JsonElement? root, string property, bool fallback = false)
        {
            if (!root.HasValue || !root.Value.TryGetProperty(property, out var value))
            {
                return fallback;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : fallback,
                _ => fallback
            };
        }
        private static string GetFriendlyOuDescription(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Organizational Unit.";
            return name.Trim().ToLowerInvariant() switch
            {
                "domain controllers" => "Contains all Domain Controllers in the domain.",
                "users" => "Contains user accounts and groups.",
                "computers" => "Contains computer objects.",
                "builtin" => "Built-in system groups and accounts.",
                "managed service accounts" => "Managed Service Accounts for automated tasks.",
                "system" => "Built-in system objects.",
                "program data" => "Data for domain applications.",
                "microsoft exchange security groups" => "Security groups used by Microsoft Exchange.",
                _ => "Organizational Unit."
            };
        }
    }
}
