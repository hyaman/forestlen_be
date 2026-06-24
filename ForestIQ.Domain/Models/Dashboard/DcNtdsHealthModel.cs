using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    // Intended to be reused when script results are stored in the database in the future.
    public class DcNtdsHealthModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("Health")]
        public string? Health { get; set; }

        [JsonPropertyName("ErrorsLast7Days")]
        public int? ErrorsLast7Days { get; set; }

        [JsonPropertyName("RecentErrors")]
        public List<NtdsEventSummaryData>? RecentErrors { get; set; }
    }

    public class NtdsEventSummaryData
    {
        [JsonPropertyName("TimeCreated")]
        public DateTime? TimeCreated { get; set; }

        [JsonPropertyName("EventID")]
        public int? EventID { get; set; }

        [JsonPropertyName("Provider")]
        public string? Provider { get; set; }

        [JsonPropertyName("Message")]
        public string? Message { get; set; }
    }
}
