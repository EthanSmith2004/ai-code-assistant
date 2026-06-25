using StoreApi.Repositories;
using StoreApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ProductRepository>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
