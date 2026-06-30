using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    // Intended to be reused when script results are stored in the database in the future.
    public class DcLogonSessionModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("LiveSessions")]
        public List<LiveSessionData>? LiveSessions { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("RawOutput")]
        public string? RawOutput { get; set; }
    }

    public class LiveSessionData
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("UserName")]
        public string? UserName { get; set; }

        [JsonPropertyName("SessionName")]
        public string? SessionName { get; set; }

        [JsonPropertyName("ID")]
        public string? ID { get; set; }

        [JsonPropertyName("State")]
        public string? State { get; set; }

        [JsonPropertyName("IdleTime")]
        public string? IdleTime { get; set; }

        [JsonPropertyName("LogonTime")]
        public string? LogonTime { get; set; }

        [JsonPropertyName("IsRemote")]
        public bool? IsRemote { get; set; }
    }
}
