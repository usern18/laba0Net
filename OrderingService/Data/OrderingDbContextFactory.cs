using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderingService.Data;

/// <summary>Creates the context for EF Core design-time migration tooling without requiring runtime secrets.</summary>
public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    /// <summary>Creates the design-time OrderingService context.</summary>
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseSqlite("Data Source=ordering.design.db")
            .Options;
        return new OrderingDbContext(options);
    }
}
