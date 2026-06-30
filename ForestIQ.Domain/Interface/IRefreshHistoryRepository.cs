using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace ForestIQ.Domain.Interface
{
    public interface IRefreshHistoryRepository
    {
        Task AddAsync(RefreshHistory refreshHistory);
        Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName);
        Task<RefreshHistory?> GetLatestAsync(SectionName sectionName);
        Task DeleteOlderThanAsync(DateTime thresholdDate);
    }
}
