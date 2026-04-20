using System.Security.Claims;
using AiCodeAssistant.API.Auth;
using AiCodeAssistant.Application.Services;
using AiCodeAssistant.Application.Interfaces;
using AiCodeAssistant.Infrastructure.Analyzers;
using AiCodeAssistant.Infrastructure.Detection;
using AiCodeAssistant.Infrastructure.Persistence;
using AiCodeAssistant.Infrastructure.Scanning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySql.EntityFrameworkCore.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var codeSightConnectionString = GetRequiredConnectionString(builder.Configuration);
var jwtOptions = GetRequiredJwtOptions(builder.Configuration);

builder.Services.AddDbContext<CodeSightDbContext>(options =>
{
    options.UseMySQL(codeSightConnectionString);
});
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IGraphDataService, MockDataService>();
builder.Services.AddScoped<INodeExplanationService, NodeExplanationService>();
builder.Services.AddScoped<IProjectScanner, FileSystemProjectScanner>();
builder.Services.AddScoped<IFrameworkDetector, DotNetFrameworkDetector>();
builder.Services.AddScoped<ICodebaseAnalyzer, GenericCodebaseAnalyzer>();
builder.Services.AddScoped<ICodebaseAnalysisService, CodebaseAnalysisService>();
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
