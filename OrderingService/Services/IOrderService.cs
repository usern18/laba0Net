using OrderingService.Dtos;
using OrderingService.Models;

namespace OrderingService.Services;

/// <summary>Business operations for user orders.</summary>
public interface IOrderService
{
    /// <summary>Creates an order after Catalog validation; returns an idempotent result or key conflict.</summary>
    Task<(Order? Order, bool Conflict)> CreateAsync(int userId, CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    /// <summary>Gets an ordered page of orders visible to the user or administrator.</summary>
    Task<PagedOrdersResponse> GetPageAsync(int userId, bool isAdmin, int page, int pageSize, OrderStatus? status, CancellationToken cancellationToken);
    /// <summary>Gets one order visible to the user or administrator.</summary>
    Task<OrderResponse?> GetByIdAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken);
    /// <summary>Cancels an accessible order; returns false when it does not exist or is not owned.</summary>
    Task<bool> CancelAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken);
}
