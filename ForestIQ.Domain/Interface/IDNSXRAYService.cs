using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.DNSXRAY;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Domain.Interface
{
    public interface IDNSXRAYService
    {
        Task<List<DnsServerDto>?> GetDnsServersAsync(DnsXrayFilterRequest filter);
        Task<List<ZoneDto>?> GetZonesAsync(DnsXrayFilterRequest filter);
        Task<List<RecordDto>?> GetRecordsAsync(DnsXrayFilterRequest filter);
        Task<List<RecordStatisticDto>?> GetRecordStatisticsAsync(DnsXrayFilterRequest filter);
        Task<List<DuplicateRecordDto>?> GetDuplicateRecordsAsync(DnsXrayFilterRequest filter);
        Task<List<IntegrityFindingDto>?> GetRecordIntegrityAsync(DnsXrayFilterRequest filter);
        Task<List<BestPracticeCheckDto>?> GetBestPracticesAsync(DnsXrayFilterRequest filter);
        Task<List<SoaComparisonDto>?> GetSoaInformationAsync(DnsXrayFilterRequest filter);
        Task<List<ResolutionTestDto>?> GetResolutionTestsAsync(DnsXrayFilterRequest filter);
        Task<DnsReplicationReportDto?> GetReplicationsAsync(DnsXrayFilterRequest filter);
        Task<List<RecordDto>?> GetCleanupCandidatesAsync(DnsXrayFilterRequest filter);
        Task<CountDto?> GetCleanupCandidatesCountAsync(DnsXrayFilterRequest filter);
        Task<DashboardSummaryDto?> GetDashboardSummaryAsync(DnsXrayFilterRequest filter);
    }
}
