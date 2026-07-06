using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ForestIQ.Service
{
    public class RefreshHistoryService : IRefreshHistoryService
    {
        private readonly IRefreshHistoryRepository _repository;

        public RefreshHistoryService(IRefreshHistoryRepository repository)
        {
            _repository = repository;
        }

        public async Task AddRefreshHistoryAsync(AddRefreshHistoryRequest request)
        {
            var history = new RefreshHistory
            {
                SectionName = request.SectionName,
                RefreshTime = DateTime.Now,
                TriggeredBy = null,
                CreatedAt = DateTime.Now,
                JsonData = request.JsonData,
                DiscoverID = request.DiscoverID,
                DCName = request.DCName
            };

            await _repository.AddAsync(history);
        }

        public async Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName)
        {
            return await _repository.GetHistoryAsync(sectionName);
        }

        public async Task<RefreshHistory?> GetLatestAsync(int HistoryId, Guid? DiscoveryId, string? DcName)
        {
            return await _repository.GetLatestAsync(HistoryId, DiscoveryId, DcName);
        }
    }
}
