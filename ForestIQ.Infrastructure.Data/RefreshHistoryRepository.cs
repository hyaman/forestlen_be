using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ForestIQ.Infrastructure.Data
{
    public class RefreshHistoryRepository : IRefreshHistoryRepository
    {
        private readonly ForestIqDbContext _context;

        public RefreshHistoryRepository(ForestIqDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RefreshHistory refreshHistory)
        {
            await _context.RefreshHistories.AddAsync(refreshHistory);
            await _context.SaveChangesAsync();
        }

        public async Task<List<RefreshHistory>> GetHistoryAsync(SectionName sectionName)
        {
            var latestIdsQuery = _context.RefreshHistories
                .Where(r => r.SectionName == sectionName && r.DiscoverID != null && r.JsonData != null)
                .GroupBy(r => r.DiscoverID)
                .Select(g => g.OrderByDescending(x => x.RefreshTime).Select(x => x.Id).FirstOrDefault());

            var groupedData = await _context.RefreshHistories
                .Where(r => latestIdsQuery.Contains(r.Id))
                .Select(x => new
                {
                    x.Id,
                    x.SectionName,
                    x.RefreshTime,
                    x.CreatedAt,
                    x.DiscoverID,
                    x.DCName
                })
                .ToListAsync();

            var ungroupedData = await _context.RefreshHistories
                .Where(r => r.SectionName == sectionName && r.DiscoverID == null)
                .Select(x => new
                {
                    x.Id,
                    x.SectionName,
                    x.RefreshTime,
                    x.CreatedAt,
                    x.DiscoverID,
                    x.DCName
                })
                .ToListAsync();

            return groupedData
                .Concat(ungroupedData)
                .Select(x => new RefreshHistory
                {
                    Id = x.Id,
                    SectionName = x.SectionName,
                    RefreshTime = x.RefreshTime,
                    CreatedAt = x.CreatedAt,
                    DiscoverID = x.DiscoverID,
                    DCName = x.DCName
                })
                .OrderByDescending(x => x.RefreshTime)
                .ToList();


        }

        public async Task<RefreshHistory?> GetLatestAsync(int HistoryId, Guid? DiscoveryId, string? Dcname)
        {
            if(DiscoveryId == null)
            {
                return await _context.RefreshHistories.Where(r => r.Id == HistoryId).FirstOrDefaultAsync();
            }

            var query = _context.RefreshHistories.Where(r => r.DiscoverID == DiscoveryId);

            if (!string.IsNullOrEmpty(Dcname))
            {
                query = query.Where(r => r.DCName == Dcname);
            }

            return await query
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync();
        }

        public async Task DeleteOlderThanAsync(DateTime date)
        {
            var oldRecords = await _context.RefreshHistories
                .Where(x => x.RefreshTime < date)
                .ToListAsync();

            if (oldRecords.Any())
            {
                _context.RefreshHistories.RemoveRange(oldRecords);
                await _context.SaveChangesAsync();
            }
        }
    }
}
