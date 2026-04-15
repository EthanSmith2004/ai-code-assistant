using AiCodeAssistant.Application.Services;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Infrastructure.Analyzers;
using AiCodeAssistant.Infrastructure.Detection;
using AiCodeAssistant.Infrastructure.Persistence;
using AiCodeAssistant.Infrastructure.Scanning;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var codeSightConnectionString = GetRequiredConnectionString(builder.Configuration);

builder.Services.AddDbContext<CodeSightDbContext>(options =>
{
    options.UseMySQL(codeSightConnectionString);
});
builder.Services.AddScoped<IGraphDataService, MockDataService>();
builder.Services.AddScoped<INodeExplanationService, NodeExplanationService>();
builder.Services.AddScoped<IProjectScanner, FileSystemProjectScanner>();
builder.Services.AddScoped<IFrameworkDetector, DotNetFrameworkDetector>();
builder.Services.AddScoped<ICodebaseAnalyzer, GenericCodebaseAnalyzer>();
builder.Services.AddScoped<ICodebaseAnalysisService, CodebaseAnalysisService>();
builder.Services.AddScoped<ICodeGraphSimplifier, CodeGraphSimplifier>();
builder.Services.AddScoped<IProjectPersistenceService, ProjectPersistenceService>();
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

static string GetRequiredConnectionString(IConfiguration configuration)
{
    return configuration.GetConnectionString("CodeSightDatabase")
           ?? throw new InvalidOperationException(
               "Missing connection string 'CodeSightDatabase'. Add it under ConnectionStrings in appsettings.Development.json, user secrets, or an environment variable.");
}
