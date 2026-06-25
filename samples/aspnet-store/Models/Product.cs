namespace StoreApi.Models;

public record CreateProductRequest(string Name, decimal Price);

public class Product
{
    private static int _nextId = 1;

    public Product(string name, decimal price)
    {
        Id = _nextId++;
        Name = name;
        Price = price;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Price { get; }
}
