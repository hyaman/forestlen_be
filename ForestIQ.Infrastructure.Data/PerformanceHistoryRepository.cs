using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using ForestIQ.Domain.Models.Dashboard;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ForestIQ.Infrastructure.Data
{
    public class PerformanceHistoryRepository : IPerformanceHistoryRepository
    {
        private readonly ForestIqDbContext _dbContext;

        public PerformanceHistoryRepository(ForestIqDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveEntryAsync(DcPerformanceHistoryEntry entry)
        {
            // 1. Add the new entry
            _dbContext.PerformanceHistory.Add(entry);
            await _dbContext.SaveChangesAsync();

            // 2. Delete entries older than 24 hours for this server to prevent database bloat
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            
            // ExecuteDeleteAsync is highly efficient for bulk deletes in EF Core 7+
            await _dbContext.PerformanceHistory
                .Where(h => h.ServerName == entry.ServerName && h.Timestamp < cutoffTime)
                .ExecuteDeleteAsync();
        }

        public async Task<DcPerformanceHistoryModel> GetHistoryAsync(string serverName)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);

            var entries = await _dbContext.PerformanceHistory
                .AsNoTracking()
                .Where(h => h.ServerName == serverName && h.Timestamp >= cutoffTime)
                .OrderBy(h => h.Timestamp)
                .ToListAsync();

            var model = new DcPerformanceHistoryModel();
            
            foreach (var entry in entries)
            {
                model.Timestamps.Add(entry.Timestamp.ToString("HH:mm"));
                model.CpuLoad.Add(entry.CpuLoad);
                model.MemoryUsage.Add(entry.MemoryUsage);
                model.NetworkIo.Add(entry.NetworkIo);
            }

            return model;
        }
    }
}
