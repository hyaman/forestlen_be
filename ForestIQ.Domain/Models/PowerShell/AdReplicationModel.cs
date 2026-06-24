using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ForestIQ.Domain.Models.PowerShell
{
    // Intended to be reused when script results are stored in the database in the future.
    public class AdReplicationModel
    {
        [JsonPropertyName("GeneratedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonPropertyName("ReplicationPartners")]
        [JsonConverter(typeof(SingleOrArrayConverter<ReplicationPartnerData>))]
        public List<ReplicationPartnerData>? ReplicationPartners { get; set; }

        [JsonPropertyName("ReplicationFailures")]
        [JsonConverter(typeof(SingleOrArrayConverter<ReplicationFailureData>))]
        public List<ReplicationFailureData>? ReplicationFailures { get; set; }

        [JsonPropertyName("ReplicationConnections")]
        [JsonConverter(typeof(SingleOrArrayConverter<ReplicationConnectionData>))]
        public List<ReplicationConnectionData>? ReplicationConnections { get; set; }

        [JsonPropertyName("RepadminSummary")]
        public RepadminSummaryData? RepadminSummary { get; set; }

        [JsonPropertyName("ReplicationHealthSummary")]
        public ReplicationHealthSummaryData? ReplicationHealthSummary { get; set; }

        [JsonPropertyName("Errors")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptErrorData>))]
        public List<ScriptErrorData>? Errors { get; set; }

        [JsonPropertyName("Timings")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptTimingData>))]
        public List<ScriptTimingData>? Timings { get; set; }
    }

    public class ReplicationPartnerData
    {
        [JsonPropertyName("Domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("SourceDCName")]
        public string? SourceDCName { get; set; }

        [JsonPropertyName("SourceSite")]
        public string? SourceSite { get; set; }

        [JsonPropertyName("SourceDCFullDN")]
        public string? SourceDCFullDN { get; set; }

        [JsonPropertyName("DestinationDC")]
        public string? DestinationDC { get; set; }

        [JsonPropertyName("DestinationSite")]
        public string? DestinationSite { get; set; }

        [JsonPropertyName("IsIntersite")]
        public bool? IsIntersite { get; set; }

        [JsonPropertyName("PartnerType")]
        public PowerShellEnumData? PartnerType { get; set; }

        [JsonPropertyName("Partition")]
        public string? Partition { get; set; }

        [JsonPropertyName("LastReplicationSuccess")]
        public DateTime? LastReplicationSuccess { get; set; }

        [JsonPropertyName("LastReplicationAttempt")]
        public DateTime? LastReplicationAttempt { get; set; }

        [JsonPropertyName("LatencyMinutes")]
        public double? LatencyMinutes { get; set; }

        [JsonPropertyName("ConsecutiveFailures")]
        public int? ConsecutiveFailures { get; set; }

        [JsonPropertyName("LastReplicationResult")]
        public int? LastReplicationResult { get; set; }

        [JsonPropertyName("Health")]
        public string? Health { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("DCIPAddress")]
        public string? DCIPAddress { get; set; }

        [JsonPropertyName("ReplicationPartner")]
        public string? ReplicationPartner { get; set; }

        [JsonPropertyName("NamingContext")]
        public string? NamingContext { get; set; }

        [JsonPropertyName("ReplicationLatencyMinutes")]
        public double? ReplicationLatencyMinutes { get; set; }

        [JsonPropertyName("ReplicationLatencyHours")]
        public double? ReplicationLatencyHours { get; set; }

        [JsonPropertyName("ReplicationLatencyStatus")]
        public string? ReplicationLatencyStatus { get; set; }

        [JsonPropertyName("LastReplicationResultMessage")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? LastReplicationResultMessage { get; set; }

        [JsonPropertyName("ReplicationStatus")]
        public string? ReplicationStatus { get; set; }

        [JsonPropertyName("PartnerStatus")]
        public string? PartnerStatus { get; set; }
    }

    public class ReplicationFailureData
    {
        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("Server")]
        public string? Server { get; set; }

        [JsonPropertyName("Partner")]
        public string? Partner { get; set; }

        [JsonPropertyName("FirstFailureTime")]
        public DateTime? FirstFailureTime { get; set; }

        [JsonPropertyName("FailureCount")]
        public int? FailureCount { get; set; }

        [JsonPropertyName("LastError")]
        public JsonElement? LastError { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class ReplicationConnectionData
    {
        [JsonPropertyName("ConnectionName")]
        public string? ConnectionName { get; set; }

        [JsonPropertyName("SourceDC")]
        public string? SourceDC { get; set; }

        [JsonPropertyName("SourceSite")]
        public string? SourceSite { get; set; }

        [JsonPropertyName("TargetDC")]
        public string? TargetDC { get; set; }

        [JsonPropertyName("TargetSite")]
        public string? TargetSite { get; set; }

        [JsonPropertyName("ConnectionType")]
        public string? ConnectionType { get; set; }

        [JsonPropertyName("TransportProtocol")]
        public string? TransportProtocol { get; set; }

        [JsonPropertyName("EnabledConnection")]
        public bool? EnabledConnection { get; set; }

        [JsonPropertyName("Options")]
        public int? Options { get; set; }

        [JsonPropertyName("ReplicatedNamingContexts")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? ReplicatedNamingContexts { get; set; }

        [JsonPropertyName("PartiallyReplicatedNamingContexts")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? PartiallyReplicatedNamingContexts { get; set; }

        [JsonPropertyName("ReplicationReasons")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? ReplicationReasons { get; set; }

        [JsonPropertyName("DistinguishedName")]
        public string? DistinguishedName { get; set; }

        [JsonPropertyName("Created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("Modified")]
        public DateTime? Modified { get; set; }
    }

    public class RepadminSummaryData
    {
        [JsonPropertyName("Healthy")]
        public bool? Healthy { get; set; }

        [JsonPropertyName("SourceDSA")]
        [JsonConverter(typeof(SingleOrArrayConverter<RepadminDsaData>))]
        public List<RepadminDsaData>? SourceDSA { get; set; }

        [JsonPropertyName("DestinationDSA")]
        [JsonConverter(typeof(SingleOrArrayConverter<RepadminDsaData>))]
        public List<RepadminDsaData>? DestinationDSA { get; set; }

        [JsonPropertyName("OperationalErrors")]
        [JsonConverter(typeof(SingleOrArrayConverter<RepadminOperationalErrorData>))]
        public List<RepadminOperationalErrorData>? OperationalErrors { get; set; }
    }

    public class RepadminDsaData
    {
        [JsonPropertyName("Server")]
        public string? Server { get; set; }

        [JsonPropertyName("LargestDelta")]
        public string? LargestDelta { get; set; }

        [JsonPropertyName("Failures")]
        public int? Failures { get; set; }

        [JsonPropertyName("Total")]
        public int? Total { get; set; }

        [JsonPropertyName("FailurePercent")]
        public int? FailurePercent { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }
    }

    public class RepadminOperationalErrorData
    {
        [JsonPropertyName("ErrorCode")]
        public int? ErrorCode { get; set; }

        [JsonPropertyName("Server")]
        public string? Server { get; set; }
    }

    public class ReplicationHealthSummaryData
    {
        [JsonPropertyName("ActiveFailures")]
        public int? ActiveFailures { get; set; }

        [JsonPropertyName("Healthy")]
        public bool? Healthy { get; set; }
    }
}
