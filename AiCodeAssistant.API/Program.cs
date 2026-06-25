using System.Security.Claims;
using AiCodeAssistant.API.Auth;
using AiCodeAssistant.API.Samples;
using AiCodeAssistant.Application.Services;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Infrastructure.Ai;
using AiCodeAssistant.Infrastructure.CodeAnalysis.Endpoints;
using AiCodeAssistant.Infrastructure.CodeAnalysis.Extractors;
using AiCodeAssistant.Infrastructure.Analyzers;
using AiCodeAssistant.Infrastructure.Detection;
using AiCodeAssistant.Infrastructure.Persistence;
using AiCodeAssistant.Infrastructure.Scanning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var codeSightConnectionString = GetRequiredConnectionString(builder.Configuration);
var jwtOptions = GetRequiredJwtOptions(builder.Configuration);

builder.Services.AddDbContext<CodeSightDbContext>(options =>
{
    options.UseNpgsql(codeSightConnectionString);
});
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.AddHttpClient<IChatCompletionClient, GroqChatClient>();
builder.Services.AddScoped<IGraphDataService, MockDataService>();
builder.Services.AddScoped<INodeExplanationService, NodeExplanationService>();
builder.Services.AddScoped<IProjectScanner, FileSystemProjectScanner>();
builder.Services.AddScoped<IFrameworkDetector, DotNetFrameworkDetector>();
builder.Services.AddScoped<IFrameworkDetector, NodeFrameworkDetector>();
builder.Services.AddScoped<IFrameworkDetector, PythonFrameworkDetector>();
builder.Services.AddScoped<IFrameworkDetector, GoFrameworkDetector>();
builder.Services.AddScoped<IFrameworkDetector, JvmFrameworkDetector>();
builder.Services.AddScoped<IFrameworkDetector, RustFrameworkDetector>();
builder.Services.AddScoped<ILanguageDependencyExtractor, JavaScriptDependencyExtractor>();
builder.Services.AddScoped<ILanguageDependencyExtractor, PythonDependencyExtractor>();
builder.Services.AddScoped<ILanguageDependencyExtractor, GoDependencyExtractor>();
builder.Services.AddScoped<ILanguageDependencyExtractor, JavaDependencyExtractor>();
builder.Services.AddScoped<ILanguageDependencyExtractor, CFamilyDependencyExtractor>();
builder.Services.AddScoped<IEndpointDetector, NodeEndpointDetector>();
builder.Services.AddScoped<IEndpointDetector, PythonEndpointDetector>();
builder.Services.AddScoped<IEndpointDetector, SpringEndpointDetector>();
builder.Services.AddScoped<IEndpointDetector, GoEndpointDetector>();
builder.Services.AddScoped<IEndpointDetector, RailsEndpointDetector>();
builder.Services.AddScoped<ICodebaseAnalyzer, GenericCodebaseAnalyzer>();
builder.Services.AddScoped<ICodebaseAnalysisService, CodebaseAnalysisService>();
builder.Services.AddSingleton<SampleCatalog>();
builder.Services.AddScoped<ICodeGraphSimplifier, CodeGraphSimplifier>();
builder.Services.AddScoped<IProjectPersistenceService, ProjectPersistenceService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtOptions.GetSigningKeyBytes()),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        NameClaimType = ClaimTypes.Email,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
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

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CodeSightDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseCors("ClientPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Lightweight health endpoint for the host's health check (Swagger is dev-only,
// so it must not be used as the health path in Production).
app.MapGet("/health", () => Results.Ok("healthy")).AllowAnonymous();

var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (railwayPort is not null)
    app.Run($"http://0.0.0.0:{railwayPort}");
else
    app.Run();

static string GetRequiredConnectionString(IConfiguration configuration)
{
    return configuration.GetConnectionString("CodeSightDatabase")
           ?? throw new InvalidOperationException(
               "Missing connection string 'CodeSightDatabase'. Add it under ConnectionStrings in appsettings.Development.json, user secrets, or an environment variable.");
}

static JwtOptions GetRequiredJwtOptions(IConfiguration configuration)
{
    var jwtOptions = configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
        ?? throw new InvalidOperationException("Missing Jwt configuration section.");

    if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
    {
        throw new InvalidOperationException("Missing Jwt:Issuer configuration value.");
    }

    if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
    {
        throw new InvalidOperationException("Missing Jwt:Audience configuration value.");
    }

    _ = jwtOptions.GetSigningKeyBytes();

    return jwtOptions;
}
