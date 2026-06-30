using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.PowerShell
{
    // Intended to be reused when script results are stored in the database in the future.
    public class AdTopologyModel
    {
        [JsonPropertyName("GeneratedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonPropertyName("DomainForestInfo")]
        public DomainForestInfoData? DomainForestInfo { get; set; }

        [JsonPropertyName("ForestInfo")]
        public ForestInfoData? ForestInfo { get; set; }

        [JsonPropertyName("Domains")]
        [JsonConverter(typeof(SingleOrArrayConverter<DomainData>))]
        public List<DomainData>? Domains { get; set; }

        [JsonPropertyName("Sites")]
        [JsonConverter(typeof(SingleOrArrayConverter<SiteData>))]
        public List<SiteData>? Sites { get; set; }

        [JsonPropertyName("SiteLinkReport")]
        [JsonConverter(typeof(SingleOrArrayConverter<SiteLinkReportData>))]
        public List<SiteLinkReportData>? SiteLinkReport { get; set; }

        [JsonPropertyName("Subnets")]
        [JsonConverter(typeof(SingleOrArrayConverter<SubnetData>))]
        public List<SubnetData>? Subnets { get; set; }

        [JsonPropertyName("DomainControllers")]
        [JsonConverter(typeof(SingleOrArrayConverter<DomainControllerData>))]
        public List<DomainControllerData>? DomainControllers { get; set; }

        [JsonPropertyName("SiteSubnetDCMapping")]
        [JsonConverter(typeof(SingleOrArrayConverter<SiteSubnetDCMappingData>))]
        public List<SiteSubnetDCMappingData>? SiteSubnetDCMapping { get; set; }

        [JsonPropertyName("DcLocator")]
        public DcLocatorData? DcLocator { get; set; }

        [JsonPropertyName("TopLevelOUs")]
        [JsonConverter(typeof(SingleOrArrayConverter<TopLevelOuData>))]
        public List<TopLevelOuData>? TopLevelOUs { get; set; }

        [JsonPropertyName("SecurityPosture")]
        public SecurityPostureData? SecurityPosture { get; set; }

        [JsonPropertyName("Errors")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptErrorData>))]
        public List<ScriptErrorData>? Errors { get; set; }

        [JsonPropertyName("Timings")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptTimingData>))]
        public List<ScriptTimingData>? Timings { get; set; }
    }

    public class DomainForestInfoData
    {
        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("NetBIOSName")]
        public string? NetBIOSName { get; set; }

        [JsonPropertyName("DomainMode")]
        public string? DomainMode { get; set; }

        [JsonPropertyName("ForestName")]
        public string? ForestName { get; set; }

        [JsonPropertyName("ForestMode")]
        public string? ForestMode { get; set; }

        [JsonPropertyName("UserCount")]
        public int? UserCount { get; set; }

        [JsonPropertyName("GroupCount")]
        public int? GroupCount { get; set; }

        [JsonPropertyName("PDCEmulator")]
        public string? PDCEmulator { get; set; }

        [JsonPropertyName("RIDMaster")]
        public string? RIDMaster { get; set; }

        [JsonPropertyName("InfrastructureMaster")]
        public string? InfrastructureMaster { get; set; }

        [JsonPropertyName("SchemaMaster")]
        public string? SchemaMaster { get; set; }

        [JsonPropertyName("DomainNamingMaster")]
        public string? DomainNamingMaster { get; set; }
    }

    public class ForestInfoData
    {
        [JsonPropertyName("ForestName")]
        public string? ForestName { get; set; }

        [JsonPropertyName("RootDomain")]
        public string? RootDomain { get; set; }

        [JsonPropertyName("Domains")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Domains { get; set; }

        [JsonPropertyName("GlobalCatalogs")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? GlobalCatalogs { get; set; }

        [JsonPropertyName("Sites")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Sites { get; set; }

        [JsonPropertyName("ForestMode")]
        public string? ForestMode { get; set; }

        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("NetBIOSName")]
        public string? NetBIOSName { get; set; }

        [JsonPropertyName("DomainMode")]
        public string? DomainMode { get; set; }

        [JsonPropertyName("UserCount")]
        public int? UserCount { get; set; }

        [JsonPropertyName("GroupCount")]
        public int? GroupCount { get; set; }

        [JsonPropertyName("PDCEmulator")]
        public string? PDCEmulator { get; set; }

        [JsonPropertyName("RIDMaster")]
        public string? RIDMaster { get; set; }

        [JsonPropertyName("InfrastructureMaster")]
        public string? InfrastructureMaster { get; set; }

        [JsonPropertyName("SchemaMaster")]
        public string? SchemaMaster { get; set; }

        [JsonPropertyName("DomainNamingMaster")]
        public string? DomainNamingMaster { get; set; }
    }

    public class DomainData
    {
        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("NetBIOSName")]
        public string? NetBIOSName { get; set; }

        [JsonPropertyName("DomainMode")]
        public string? DomainMode { get; set; }

        [JsonPropertyName("ParentDomain")]
        public string? ParentDomain { get; set; }

        [JsonPropertyName("ChildDomains")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? ChildDomains { get; set; }

        [JsonPropertyName("PDCEmulator")]
        public string? PDCEmulator { get; set; }

        [JsonPropertyName("RIDMaster")]
        public string? RIDMaster { get; set; }

        [JsonPropertyName("InfrastructureMaster")]
        public string? InfrastructureMaster { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("Exception")]
        public string? Exception { get; set; }
    }

    public class SiteData
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }

    public class SubnetData
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Site")]
        public string? Site { get; set; }

        [JsonPropertyName("Location")]
        public string? Location { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }

    public class SiteLinkReportData
    {
        [JsonPropertyName("SiteLinkName")]
        public string? SiteLinkName { get; set; }

        [JsonPropertyName("Cost")]
        public int? Cost { get; set; }

        [JsonPropertyName("ReplicationIntervalMinutes")]
        public int? ReplicationIntervalMinutes { get; set; }

        [JsonPropertyName("ScheduleExists")]
        public string? ScheduleExists { get; set; }

        [JsonPropertyName("ScheduleExistsBool")]
        public bool? ScheduleExistsBool { get; set; }

        [JsonPropertyName("SitesIncluded")]
        public string? SitesIncluded { get; set; }

        [JsonPropertyName("SitesIncludedArray")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? SitesIncludedArray { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("StatusReason")]
        public string? StatusReason { get; set; }
    }

    public class DomainControllerData
    {
        [JsonPropertyName("HostName")]
        public string? HostName { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Site")]
        public string? Site { get; set; }

        [JsonPropertyName("IPv4Address")]
        public string? IPv4Address { get; set; }

        [JsonPropertyName("IsGlobalCatalog")]
        public bool? IsGlobalCatalog { get; set; }

        [JsonPropertyName("IsReadOnly")]
        public bool? IsReadOnly { get; set; }

        [JsonPropertyName("OperatingSystem")]
        public string? OperatingSystem { get; set; }

        [JsonPropertyName("OperatingSystemVersion")]
        public string? OperatingSystemVersion { get; set; }

        [JsonPropertyName("UptimeHours")]
        public double? UptimeHours { get; set; }

        [JsonPropertyName("OperationMasterRoles")]
        [JsonConverter(typeof(SingleOrArrayConverter<PowerShellEnumData>))]
        public List<PowerShellEnumData>? OperationMasterRoles { get; set; }
    }

    public class SiteSubnetDCMappingData
    {
        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("Subnets")]
        public string? Subnets { get; set; }

        [JsonPropertyName("SubnetList")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? SubnetList { get; set; }

        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("DCName")]
        public string? DCName { get; set; }

        [JsonPropertyName("DCIPAddress")]
        public string? DCIPAddress { get; set; }

        [JsonPropertyName("IsGlobalCatalog")]
        public bool? IsGlobalCatalog { get; set; }

        [JsonPropertyName("IsReadOnly")]
        public bool? IsReadOnly { get; set; }

        [JsonPropertyName("OperatingSystem")]
        public string? OperatingSystem { get; set; }

        [JsonPropertyName("FSMORoles")]
        public string? FSMORoles { get; set; }
    }

    public class DcLocatorData
    {
        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("Address")]
        public string? Address { get; set; }

        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("ForestName")]
        public string? ForestName { get; set; }

        [JsonPropertyName("DcSiteName")]
        public string? DcSiteName { get; set; }

        [JsonPropertyName("OurSiteName")]
        public string? OurSiteName { get; set; }

        [JsonPropertyName("Flags")]
        public string? Flags { get; set; }
    }

    public class TopLevelOuData
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("DistinguishedName")]
        public string? DistinguishedName { get; set; }

        [JsonPropertyName("Domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("ObjectCount")]
        public int? ObjectCount { get; set; }
    }

    public class SecurityPostureData
    {
        [JsonPropertyName("RecycleBinEnabled")]
        public bool? RecycleBinEnabled { get; set; }

        [JsonPropertyName("ComplexityEnabled")]
        public bool? ComplexityEnabled { get; set; }

        [JsonPropertyName("MaxPasswordAgeDays")]
        public int? MaxPasswordAgeDays { get; set; }

        [JsonPropertyName("MinPasswordAgeDays")]
        public int? MinPasswordAgeDays { get; set; }

        [JsonPropertyName("MinPasswordLength")]
        public int? MinPasswordLength { get; set; }

        [JsonPropertyName("PasswordHistoryCount")]
        public int? PasswordHistoryCount { get; set; }

        [JsonPropertyName("LockoutDurationMins")]
        public double? LockoutDurationMins { get; set; }

        [JsonPropertyName("LockoutObservationMins")]
        public double? LockoutObservationMins { get; set; }

        [JsonPropertyName("LockoutThreshold")]
        public int? LockoutThreshold { get; set; }
    }
}
