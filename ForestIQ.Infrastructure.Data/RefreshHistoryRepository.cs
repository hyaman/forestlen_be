using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            return await _context.RefreshHistories
                .Where(r => r.SectionName == sectionName)
                .OrderByDescending(r => r.RefreshTime)
                .Take(10)
                .ToListAsync();
        }

        public async Task<RefreshHistory?> GetLatestAsync(SectionName sectionName)
        {
            return await _context.RefreshHistories
                .Where(r => r.SectionName == sectionName)
                .OrderByDescending(r => r.RefreshTime)
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
