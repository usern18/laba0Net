using System.ComponentModel.DataAnnotations;
using OrderingService.Models;

namespace OrderingService.Dtos;

/// <summary>Product and quantity for a new order. The user identity comes from the access token.</summary>
/// <param name="ProductId">Positive identifier of an active Catalog item.</param>
/// <param name="Quantity">Positive number of units to order.</param>
public sealed record CreateOrderRequest(
    [param: Range(1, int.MaxValue)] int ProductId,
    [param: Range(1, int.MaxValue)] int Quantity);

/// <summary>Order data returned by the API, including the catalog snapshot captured at purchase.</summary>
/// <param name="Id">Stable order identifier.</param>
/// <param name="UserId">Owner identifier from the access token.</param>
/// <param name="ProductId">Catalog product identifier; no cross-database foreign key exists.</param>
/// <param name="ProductName">Product title snapshot at order creation.</param>
/// <param name="Quantity">Number of units ordered.</param>
/// <param name="TotalPrice">Total price snapshot at order creation.</param>
/// <param name="UnitPrice">Per-item price snapshot at order creation.</param>
/// <param name="Status">Current order status.</param>
/// <param name="CreatedAt">Order creation timestamp in UTC.</param>
public sealed record OrderResponse(
    int Id,
    int UserId,
    int ProductId,
    string ProductName,
    int Quantity,
    decimal TotalPrice,
    decimal UnitPrice,
    OrderStatus Status,
    DateTime CreatedAt)
{
    /// <summary>Maps the persisted order to its public API representation.</summary>
    public static OrderResponse FromModel(Models.Order order) => new(
        order.Id,
        order.UserId,
        order.ProductId,
        order.ProductName,
        order.Quantity,
        order.TotalPrice,
        order.UnitPrice,
        order.Status,
        order.CreatedAt);
}

/// <summary>Paginated orders visible to the authenticated user.</summary>
/// <param name="Items">Orders in this page.</param>
/// <param name="Page">One-based page index.</param>
/// <param name="PageSize">Number of items requested per page.</param>
/// <param name="TotalCount">Total number of matching orders before pagination.</param>
public sealed record PagedOrdersResponse(IReadOnlyList<OrderResponse> Items, int Page, int PageSize, int TotalCount);
