using StoreApi.Models;

namespace StoreApi.Repositories;

public class ProductRepository
{
    private readonly List<Product> _products = new();

    public IEnumerable<Product> GetAll() => _products;

    public Product Find(int id) => _products.First(product => product.Id == id);

    public Product Add(Product product)
    {
        _products.Add(product);
        return product;
    }
}
