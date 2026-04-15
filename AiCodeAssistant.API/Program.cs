using AiCodeAssistant.Application.Services;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Infrastructure.Analyzers;
using AiCodeAssistant.Infrastructure.Detection;
using AiCodeAssistant.Infrastructure.Scanning;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IGraphDataService, MockDataService>();
builder.Services.AddScoped<INodeExplanationService, NodeExplanationService>();
builder.Services.AddScoped<IProjectScanner, FileSystemProjectScanner>();
builder.Services.AddScoped<IFrameworkDetector, DotNetFrameworkDetector>();
builder.Services.AddScoped<ICodebaseAnalyzer, GenericCodebaseAnalyzer>();
builder.Services.AddScoped<ICodebaseAnalysisService, CodebaseAnalysisService>();
builder.Services.AddScoped<ICodeGraphSimplifier, CodeGraphSimplifier>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseCors("ClientPolicy");
app.MapControllers();

app.Run();
