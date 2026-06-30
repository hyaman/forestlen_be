using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForestIQ.Domain.Models.PowerShell;

namespace ForestIQ.Domain.Models.Dashboard
{
    public class DcInventoryResponseModel
    {
        [JsonPropertyName("GeneratedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonPropertyName("InventoryResults")]
        public List<DcInventoryModel>? InventoryResults { get; set; }
    }

    // Intended to be reused when script results are stored in the database in the future.
    public class DcInventoryModel
    {
        [JsonPropertyName("Inventory")]
        public InventoryData? Inventory { get; set; }

        [JsonPropertyName("Disks")]
        public List<DiskData>? Disks { get; set; }

        [JsonPropertyName("Services")]
        public List<ServiceData>? Services { get; set; }

        [JsonPropertyName("Roles")]
        public List<RoleData>? Roles { get; set; }

        [JsonPropertyName("Apps")]
        public List<AppData>? Apps { get; set; }

        [JsonPropertyName("Firewall")]
        public List<FirewallData>? Firewall { get; set; }

        [JsonPropertyName("PortConnectivity")]
        public List<PortConnectivityData>? PortConnectivity { get; set; }

        [JsonPropertyName("HealthSummary")]
        public HealthSummaryData? HealthSummary { get; set; }

        [JsonPropertyName("PerformanceMetrics")]
        public List<PerformanceMetricData>? PerformanceMetrics { get; set; }

        [JsonPropertyName("TopDirectories")]
        public List<TopDirectoryData>? TopDirectories { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("RawOutput")]
        public string? RawOutput { get; set; }
    }

    public class PortConnectivityData
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("Service")]
        public string? Service { get; set; }

        [JsonPropertyName("Port")]
        public int? Port { get; set; }

        [JsonPropertyName("Open")]
        public bool? Open { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class InventoryData
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("Site")]
        public string? Site { get; set; }

        [JsonPropertyName("IPv4")]
        public string? IPv4 { get; set; }

        [JsonPropertyName("IsGlobalCatalog")]
        public bool? IsGlobalCatalog { get; set; }

        [JsonPropertyName("IsReadOnlyDC")]
        public bool? IsReadOnlyDC { get; set; }

        [JsonPropertyName("OperatingSystem")]
        public string? OperatingSystem { get; set; }

        [JsonPropertyName("OSVersion")]
        public string? OSVersion { get; set; }

        [JsonPropertyName("Build")]
        public string? Build { get; set; }

        [JsonPropertyName("Manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("BIOS")]
        public string? BIOS { get; set; }

        [JsonPropertyName("CPU")]
        public string? CPU { get; set; }

        [JsonPropertyName("CPUCores")]
        public JsonElement? CPUCores { get; set; }

        [JsonPropertyName("LogicalProcessors")]
        public JsonElement? LogicalProcessors { get; set; }

        [JsonPropertyName("CPULoadPercent")]
        public JsonElement? CPULoadPercent { get; set; }

        [JsonPropertyName("TotalMemoryGB")]
        public JsonElement? TotalMemoryGB { get; set; }

        [JsonPropertyName("FreeMemoryGB")]
        public JsonElement? FreeMemoryGB { get; set; }

        [JsonPropertyName("MemoryUsedPercent")]
        public JsonElement? MemoryUsedPercent { get; set; }

        [JsonPropertyName("LastBoot")]
        public DateTime? LastBoot { get; set; }

        [JsonPropertyName("UptimeDays")]
        public JsonElement? UptimeDays { get; set; }

        [JsonPropertyName("NTDSDatabasePath")]
        public string? NTDSDatabasePath { get; set; }

        [JsonPropertyName("NTDSLogPath")]
        public string? NTDSLogPath { get; set; }

        [JsonPropertyName("NTDSSizeGB")]
        public JsonElement? NTDSSizeGB { get; set; }

        [JsonPropertyName("LatestHotfix")]
        public string? LatestHotfix { get; set; }

        [JsonPropertyName("LatestHotfixDate")]
        public object? LatestHotfixDate { get; set; }

        [JsonPropertyName("DaysSinceLastPatch")]
        public JsonElement? DaysSinceLastPatch { get; set; }

        [JsonPropertyName("PendingReboot")]
        public bool? PendingReboot { get; set; }

        [JsonPropertyName("DefenderEnabled")]
        public JsonElement? DefenderEnabled { get; set; }

        [JsonPropertyName("DefenderSigAge")]
        public JsonElement? DefenderSigAge { get; set; }

        [JsonPropertyName("DefenderVersion")]
        public string? DefenderVersion { get; set; }

        [JsonPropertyName("DefenderEngineVersion")]
        public string? DefenderEngineVersion { get; set; }

        [JsonPropertyName("NetworkAdapters")]
        public List<NetworkAdapterData>? NetworkAdapters { get; set; }

        [JsonPropertyName("DnsServers")]
        public List<DnsServerData>? DnsServers { get; set; }

        [JsonPropertyName("Certificates")]
        public List<CertificateData>? Certificates { get; set; }

        [JsonPropertyName("RecentHotfixes")]
        public List<HotfixData>? RecentHotfixes { get; set; }

        [JsonPropertyName("FSMORoles")]
        [JsonConverter(typeof(SingleOrArrayConverter<PowerShellEnumData>))]
        public List<PowerShellEnumData>? FSMORoles { get; set; }

        [JsonPropertyName("Platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("OverallStatus")]
        public string? OverallStatus { get; set; }

        [JsonPropertyName("PendingUpdates")]
        public JsonElement? PendingUpdates { get; set; }

        [JsonPropertyName("SysvolReplicationStatus")]
        public string? SysvolReplicationStatus { get; set; }

        [JsonPropertyName("LdapSigning")]
        public string? LdapSigning { get; set; }

        [JsonPropertyName("SmbSigning")]
        public string? SmbSigning { get; set; }

        [JsonPropertyName("ADWSStatus")]
        public string? ADWSStatus { get; set; }

        [JsonPropertyName("W32TimeStatus")]
        public string? W32TimeStatus { get; set; }
    }

    public class DiskData
    {
        [JsonPropertyName("Drive")]
        public string? Drive { get; set; }

        [JsonPropertyName("VolumeName")]
        public string? VolumeName { get; set; }

        [JsonPropertyName("FileSystem")]
        public string? FileSystem { get; set; }

        [JsonPropertyName("SizeGB")]
        public JsonElement? SizeGB { get; set; }

        [JsonPropertyName("FreeGB")]
        public JsonElement? FreeGB { get; set; }

        [JsonPropertyName("FreePercent")]
        public JsonElement? FreePercent { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class ServiceData
    {
        [JsonPropertyName("ServiceName")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("Status")]
        public JsonElement? Status { get; set; }

        [JsonPropertyName("StartType")]
        public JsonElement? StartType { get; set; }

        [JsonPropertyName("Health")]
        public string? Health { get; set; }
    }

    public class RoleData
    {
        [JsonPropertyName("RoleName")]
        public string? RoleName { get; set; }

        [JsonPropertyName("Display")]
        public string? Display { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class AppData
    {
        [JsonPropertyName("SoftwareName")]
        public string? SoftwareName { get; set; }

        [JsonPropertyName("Version")]
        public string? Version { get; set; }

        [JsonPropertyName("Publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("InstallDate")]
        public string? InstallDate { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class FirewallData
    {
        [JsonPropertyName("Profile")]
        public string? Profile { get; set; }

        [JsonPropertyName("Enabled")]
        public JsonElement? Enabled { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class NetworkAdapterData
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("MacAddress")]
        public string? MacAddress { get; set; }

        [JsonPropertyName("LinkSpeed")]
        public string? LinkSpeed { get; set; }

        [JsonPropertyName("IPv4")]
        public object? IPv4 { get; set; }
    }

    public class DnsServerData
    {
        [JsonPropertyName("InterfaceAlias")]
        public string? InterfaceAlias { get; set; }

        [JsonPropertyName("DnsServers")]
        public string? DnsServers { get; set; }
    }

    public class CertificateData
    {
        [JsonPropertyName("Subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("Issuer")]
        public string? Issuer { get; set; }

        [JsonPropertyName("Thumbprint")]
        public string? Thumbprint { get; set; }

        [JsonPropertyName("NotAfter")]
        public DateTime? NotAfter { get; set; }

        [JsonPropertyName("DaysToExpiry")]
        public JsonElement? DaysToExpiry { get; set; }
    }

    public class HotfixData
    {
        [JsonPropertyName("HotFixID")]
        public string? HotFixID { get; set; }

        [JsonPropertyName("InstalledOn")]
        public object? InstalledOn { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }

    public class HealthSummaryData
    {
        [JsonPropertyName("DiskHealth")]
        public string? DiskHealth { get; set; }

        [JsonPropertyName("ServiceHealth")]
        public string? ServiceHealth { get; set; }

        [JsonPropertyName("CertificateHealth")]
        public string? CertificateHealth { get; set; }

        [JsonPropertyName("PortHealth")]
        public string? PortHealth { get; set; }
    }

    public class PerformanceMetricData
    {
        [JsonPropertyName("Section")]
        public string? Section { get; set; }

        [JsonPropertyName("ElapsedMilliseconds")]
        public long? ElapsedMilliseconds { get; set; }
    }

    public class TopDirectoryData
    {
        [JsonPropertyName("Path")]
        public string? Path { get; set; }

        [JsonPropertyName("SizeGB")]
        public JsonElement? SizeGB { get; set; }
    }
}
