using Microsoft.EntityFrameworkCore;
using OrderingService.Models;

namespace OrderingService.Data;

/// <summary>EF Core context for OrderingService-owned order data.</summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    /// <summary>Orders owned by this service.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();
        order.HasKey(x => x.Id);
        order.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        order.Property(x => x.UnitPrice).HasPrecision(18, 2);
        order.Property(x => x.TotalPrice).HasPrecision(18, 2);
        order.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        order.Property(x => x.IdempotencyKey).HasMaxLength(128);
        order.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        order.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
