using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    // Intended to be reused when script results are stored in the database in the future.
    public class DcAuthSummaryModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("AuthProtocolSummary")]
        public List<AuthProtocolSummaryData>? AuthProtocolSummary { get; set; }

        [JsonPropertyName("TopAuthenticatedUsers")]
        public List<AuthenticatedUserData>? TopAuthenticatedUsers { get; set; }

        [JsonPropertyName("RecentNTLMUsers")]
        public List<AuthenticatedUserData>? RecentNTLMUsers { get; set; }

        [JsonPropertyName("RecentKerberosUsers")]
        public List<AuthenticatedUserData>? RecentKerberosUsers { get; set; }

        [JsonPropertyName("TopSourceComputers")]
        public List<SourceComputerData>? TopSourceComputers { get; set; }

        [JsonPropertyName("LogonsOverTime")]
        public List<LogonOverTimeData>? LogonsOverTime { get; set; }

        [JsonPropertyName("SuccessfulLogons")]
        public int? SuccessfulLogons { get; set; }

        [JsonPropertyName("KerberosTGTRequests")]
        public int? KerberosTGTRequests { get; set; }

        [JsonPropertyName("KerberosServiceTickets")]
        public int? KerberosServiceTickets { get; set; }

        [JsonPropertyName("NTLMValidations")]
        public int? NTLMValidations { get; set; }

        [JsonPropertyName("PerformanceMetrics")]
        public List<PerformanceMetricData>? PerformanceMetrics { get; set; }
    }

    public class SourceComputerData
    {
        [JsonPropertyName("SourceWorkstation")]
        public string? SourceWorkstation { get; set; }

        [JsonPropertyName("Count")]
        public int? Count { get; set; }
    }

    public class AuthEventData
    {
        [JsonPropertyName("TimeCreated")]
        public System.Text.Json.JsonElement? TimeCreated { get; set; }

        [JsonPropertyName("UserName")]
        public string? UserName { get; set; }

        [JsonPropertyName("LogonType")]
        public string? LogonType { get; set; }

        [JsonPropertyName("SourceIP")]
        public string? SourceIP { get; set; }

        [JsonPropertyName("SourceWorkstation")]
        public string? SourceWorkstation { get; set; }

        [JsonPropertyName("AuthenticationPackage")]
        public string? AuthenticationPackage { get; set; }

        [JsonPropertyName("LogonProcess")]
        public string? LogonProcess { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("TicketOptions")]
        public string? TicketOptions { get; set; }

        [JsonPropertyName("ServiceName")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("PackageName")]
        public string? PackageName { get; set; }

        [JsonPropertyName("AuthProtocol")]
        public string? AuthProtocol { get; set; }

        [JsonPropertyName("EventID")]
        public int? EventID { get; set; }

        [JsonPropertyName("EventType")]
        public string? EventType { get; set; }
    }

    public class AuthProtocolSummaryData
    {
        [JsonPropertyName("Protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("Count")]
        public int? Count { get; set; }
    }

    public class AuthenticatedUserData
    {
        [JsonPropertyName("UserName")]
        public string? UserName { get; set; }

        [JsonPropertyName("Count")]
        public int? Count { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }
    }

    public class LogonOverTimeData
    {
        [JsonPropertyName("Time")]
        public string? Time { get; set; }

        [JsonPropertyName("Kerberos")]
        public int? Kerberos { get; set; }

        [JsonPropertyName("NTLM")]
        public int? NTLM { get; set; }
    }
}
