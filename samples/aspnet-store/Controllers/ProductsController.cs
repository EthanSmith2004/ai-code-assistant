using Microsoft.AspNetCore.Mvc;
using StoreApi.Services;
using StoreApi.Models;

namespace StoreApi.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetAll() => Ok(_productService.GetAll());

    [HttpGet("{id}")]
    public ActionResult<Product> GetById(int id) => Ok(_productService.GetById(id));

    [HttpPost]
    public ActionResult<Product> Create(CreateProductRequest request) => Ok(_productService.Create(request));
}
