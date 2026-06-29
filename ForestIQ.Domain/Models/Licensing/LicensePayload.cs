using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Licensing
{
    public class LicensePayload
    {
        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("forest_name")]
        public string ForestName { get; set; } = string.Empty;

        [JsonPropertyName("expiry_date")]
        public DateTime ExpiryDate { get; set; }
    }
}
