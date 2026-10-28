namespace OrderingService.Models;

/// <summary>Lifecycle states supported by an order.</summary>
public enum OrderStatus
{
    /// <summary>The order has been created and is active.</summary>
    Pending,
    /// <summary>The owner has cancelled the order.</summary>
    Cancelled
}

/// <summary>Persisted order and immutable Catalog snapshot.</summary>
public sealed class Order
{
    /// <summary>Database-generated identifier.</summary>
    public int Id { get; set; }
    /// <summary>Owner identifier copied from the validated JWT subject.</summary>
    public int UserId { get; set; }
    /// <summary>Catalog product identifier, without a cross-service foreign key.</summary>
    public int ProductId { get; set; }
    /// <summary>Catalog title at the time the order was created.</summary>
    public string ProductName { get; set; } = string.Empty;
    /// <summary>Catalog unit price at order creation.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Number of units ordered.</summary>
    public int Quantity { get; set; }
    /// <summary>Total price at order creation.</summary>
    public decimal TotalPrice { get; set; }
    /// <summary>Current lifecycle status.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Optional deduplication key, unique per user.</summary>
    public string? IdempotencyKey { get; set; }
}
