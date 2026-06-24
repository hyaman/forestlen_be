using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.PowerShell
{
    // Intended to be reused when script results are stored in the database in the future.
    public class AdDiagnosticsModel
    {
        [JsonPropertyName("GeneratedAt")]
        public DateTime? GeneratedAt { get; set; }

        [JsonPropertyName("PortConnectivity")]
        [JsonConverter(typeof(SingleOrArrayConverter<PortConnectivityData>))]
        public List<PortConnectivityData>? PortConnectivity { get; set; }

        [JsonPropertyName("DcDiagSummary")]
        [JsonConverter(typeof(SingleOrArrayConverter<DcDiagSummaryData>))]
        public List<DcDiagSummaryData>? DcDiagSummary { get; set; }

        [JsonPropertyName("Errors")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptErrorData>))]
        public List<ScriptErrorData>? Errors { get; set; }

        [JsonPropertyName("Timings")]
        [JsonConverter(typeof(SingleOrArrayConverter<ScriptTimingData>))]
        public List<ScriptTimingData>? Timings { get; set; }
    }

    public class PortConnectivityData
    {
        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("Service")]
        public string? Service { get; set; }

        [JsonPropertyName("Port")]
        public int? Port { get; set; }

        [JsonPropertyName("Open")]
        public bool? Open { get; set; }
    }

    public class DcDiagSummaryData
    {
        [JsonPropertyName("DomainController")]
        public string? DomainController { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("Category")]
        public string? Category { get; set; }

        [JsonPropertyName("Details")]
        public string? Details { get; set; }

        [JsonPropertyName("RawOutput")]
        public string? RawOutput { get; set; }
    }
}
