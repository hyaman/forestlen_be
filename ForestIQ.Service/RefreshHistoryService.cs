using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public class RefreshHistoryService : IRefreshHistoryService
    {
        private readonly IRefreshHistoryRepository _repository;

        public RefreshHistoryService(IRefreshHistoryRepository repository)
        {
            _repository = repository;
        }

        public async Task AddRefreshHistoryAsync(SectionName sectionName, string? triggeredBy)
        {
            var history = new RefreshHistory
            {
                SectionName = sectionName,
                RefreshTime = DateTime.Now,
                TriggeredBy = triggeredBy,
                CreatedAt = DateTime.Now
            };

            await _repository.AddAsync(history);
        }

        public async Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName)
        {
            return await _repository.GetHistoryAsync(sectionName);
        }

        public async Task<RefreshHistory?> GetLatestAsync(SectionName sectionName)
        {
            return await _repository.GetLatestAsync(sectionName);
        }
    }
}
