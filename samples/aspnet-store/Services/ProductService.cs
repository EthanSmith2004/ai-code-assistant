using StoreApi.Models;
using StoreApi.Repositories;

namespace StoreApi.Services;

public class ProductService
{
    private readonly ProductRepository _repository;

    public ProductService(ProductRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<Product> GetAll() => _repository.GetAll();

    public Product GetById(int id) => _repository.Find(id);

    public Product Create(CreateProductRequest request) => _repository.Add(new Product(request.Name, request.Price));
}
