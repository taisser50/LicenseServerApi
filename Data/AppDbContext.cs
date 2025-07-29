// LicenseServerApi/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using LicenseServerApi.Models; // 💡 هذا السطر هو الذي يربط AppDbContext بكلاس Voucher

namespace LicenseServerApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<LicenseRecord> Licenses { get; set; } = default!;
        public DbSet<Voucher> Vouchers { get; set; } = default!; // استخدام كلاس Voucher

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LicenseRecord>()
                .HasIndex(l => l.HWID);

            modelBuilder.Entity<Voucher>()
                .HasIndex(v => v.VoucherCode)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}