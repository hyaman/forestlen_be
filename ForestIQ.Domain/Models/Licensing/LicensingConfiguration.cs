using System;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Licensing
{
    public class LicensingConfiguration
    {
        [JsonPropertyName("container_setup_date")]
        public DateTime? ContainerSetupDate { get; set; }

        [JsonPropertyName("license_key")]
        public string? LicenseKey { get; set; }

        [JsonPropertyName("environment_binding")]
        public EnvironmentBindingConfig? EnvironmentBinding { get; set; }
    }

    public class EnvironmentBindingConfig
    {
        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }

        [JsonPropertyName("ad_forest")]
        public string? AdForest { get; set; }
    }
}
