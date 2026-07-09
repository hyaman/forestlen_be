using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ForestIQ.Domain.Interface
{
    public interface IRefreshHistoryRepository
    {
        Task AddAsync(RefreshHistory refreshHistory);
        Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName);
        Task<RefreshHistory?> GetLatestAsync(int HistoryId, Guid? DiscoveryId, string? DcName);
        Task DeleteOlderThanAsync(DateTime thresholdDate);
    }
}
