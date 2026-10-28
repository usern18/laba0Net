using Microsoft.EntityFrameworkCore;
using OrderingService.Data;
using OrderingService.Dtos;
using OrderingService.Models;

namespace OrderingService.Services;

internal sealed class OrderService(OrderingDbContext db, ICatalogClient catalog, ILogger<OrderService> logger) : IOrderService
{
    public async Task<(Order? Order, bool Conflict)> CreateAsync(int userId, CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                order => order.UserId == userId && order.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                var sameRequest = existing.ProductId == request.ProductId && existing.Quantity == request.Quantity;
                return sameRequest ? (existing, false) : (null, true);
            }
        }

        var item = await catalog.GetItemAsync(request.ProductId, cancellationToken);
        if (item is null || !item.IsActive)
            return (null, false);

        var order = new Order
        {
            UserId = userId,
            ProductId = item.Id,
            ProductName = item.Title,
            UnitPrice = item.Price,
            Quantity = request.Quantity,
            TotalPrice = item.Price * request.Quantity,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey
        };
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            db.Entry(order).State = EntityState.Detached;
            var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is null)
                throw;
            var sameRequest = existing.ProductId == request.ProductId && existing.Quantity == request.Quantity;
            return sameRequest ? (existing, false) : (null, true);
        }
        logger.LogInformation("Created order {OrderId} for user {UserId}", order.Id, userId);
        return (order, false);
    }

    public async Task<PagedOrdersResponse> GetPageAsync(int userId, bool isAdmin, int page, int pageSize, OrderStatus? status, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = db.Orders.AsNoTracking();
        if (!isAdmin)
            query = query.Where(order => order.UserId == userId);
        if (status is not null)
            query = query.Where(order => order.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new PagedOrdersResponse(orders.Select(OrderResponse.FromModel).ToArray(), page, pageSize, totalCount);
    }

    public async Task<OrderResponse?> GetByIdAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.Id == id && (isAdmin || order.UserId == userId), cancellationToken);
        return order is null ? null : OrderResponse.FromModel(order);
    }

    public async Task<bool> CancelAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var order = await db.Orders.SingleOrDefaultAsync(order => order.Id == id && (isAdmin || order.UserId == userId), cancellationToken);
        if (order is null)
            return false;
        if (order.Status == OrderStatus.Cancelled)
            return true;
        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cancelled order {OrderId} for user {UserId}", order.Id, userId);
        return true;
    }
}
