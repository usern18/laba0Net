using System.Collections.Concurrent;
using laba0Net.Models;

namespace laba0Net.Data;

public class MonolithStore
{
    public ConcurrentDictionary<int, Product> Products { get; } = new();
    public ConcurrentDictionary<int, Order> Orders { get; } = new();
    private int _pId = 0, _oId = 0;

    public MonolithStore()
    {
        Products[1] = new Product { Id = Interlocked.Increment(ref _pId), Name = "Квиток на подію", Category = "Квитки", Price = 500, StockQuantity = 50 };
        Products[2] = new Product { Id = Interlocked.Increment(ref _pId), Name = "Підручник з архітектури", Category = "Книги", Price = 350, StockQuantity = 20 };
    }

    public int NextProductId() => Interlocked.Increment(ref _pId);
    public int NextOrderId() => Interlocked.Increment(ref _oId);
}