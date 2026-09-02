using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Data
{
    public class PosDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<StoreSettings> StoreSettings { get; set; } = null!;

        public PosDbContext() { }

        public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=pos.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>().HasIndex(p => p.Barcode).IsUnique();
            modelBuilder.Entity<Member>().HasIndex(m => m.Phone).IsUnique();
            modelBuilder.Entity<Member>().HasIndex(m => m.MemberNo).IsUnique();
            modelBuilder.Entity<Order>().HasIndex(o => o.OrderNo).IsUnique();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                    {
                        property.SetPrecision(18);
                        property.SetScale(2);
                    }
                }
            }

            modelBuilder.Entity<StoreSettings>().HasData(new StoreSettings
            {
                Id = 1,
                StoreName = "万客隆美发用品专卖西门店"
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries();
            var utcNow = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var lastModifiedProp = entry.Entity.GetType().GetProperty("LastModified");
                    if (lastModifiedProp != null)
                    {
                        lastModifiedProp.SetValue(entry.Entity, utcNow);
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
