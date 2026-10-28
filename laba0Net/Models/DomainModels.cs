namespace laba0Net.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}

public record CreateProductRequest(string Name, string Category, decimal Price, int StockQuantity);

public record CreateOrderRequest(
    [param: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int UserId,
    [param: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int ProductId,
    [param: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int Quantity);