using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ForestIQ.Infrastructure.Data
{
    public class ForestIqDbContext : DbContext
    {
        public ForestIqDbContext(DbContextOptions<ForestIqDbContext> options) : base(options)
        {
        }

        public DbSet<AdConfiguration> AdConfigurations { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<DcPerformanceHistoryEntry> PerformanceHistory { get; set; }
        public DbSet<RefreshHistory> RefreshHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AdConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ForestName).IsUnique();

                entity.Property(e => e.ForestName).IsRequired();
                entity.Property(e => e.UserName).IsRequired();
                entity.Property(e => e.RemoteHost);
                entity.Property(e => e.EncryptedPassword).IsRequired();
                entity.Property(e => e.DnsServersJson).IsRequired();
                entity.Property(e => e.CreatedAtUtc).IsRequired();
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
            });
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();

                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.EncryptedPassword).IsRequired();
                entity.Property(e => e.Role).IsRequired();
            });

            modelBuilder.Entity<DcPerformanceHistoryEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ServerName, e.Timestamp });

                entity.Property(e => e.ServerName).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
            });

            modelBuilder.Entity<RefreshHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SectionName);
                entity.HasIndex(e => e.RefreshTime).IsDescending();

                entity.Property(e => e.SectionName)
                      .HasConversion(
                          v => v.ToEnumString(),
                          v => v.ToEnum<SectionName>()
                      )
                      .IsRequired();
                entity.Property(e => e.RefreshTime).IsRequired();
            });
        }
    }
}
