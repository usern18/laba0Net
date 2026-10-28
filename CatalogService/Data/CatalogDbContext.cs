using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.Property(i => i.Title).HasMaxLength(200).IsRequired();
            e.Property(i => i.Description).HasMaxLength(2000);
            e.Property(i => i.Price).HasPrecision(18, 2);
            e.Property(i => i.StockQuantity).HasDefaultValue(0);
            e.Property(i => i.IsActive).HasDefaultValue(true);
            e.Property(i => i.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(i => i.Title);
            e.HasOne(i => i.Category).WithMany(c => c.Items).HasForeignKey(i => i.CategoryId);
        });

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Концерти" },
            new Category { Id = 2, Name = "Театр" },
            new Category { Id = 3, Name = "Спорт" }
        );

        modelBuilder.Entity<Item>().HasData(
            new Item { Id = 1001, Title = "Концерт Океан Ельзи", Description = "НСК Олімпійський", Price = 1500, CategoryId = 1, IsActive = true, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Item { Id = 1002, Title = "Гамлет", Description = "Класична постановка", Price = 450, CategoryId = 2, IsActive = true, CreatedAt = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new Item { Id = 1003, Title = "Динамо — Шахтар", Description = "УПЛ", Price = 800, CategoryId = 3, IsActive = true, CreatedAt = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}