using AiCodeAssistant.Client.Components;
using AiCodeAssistant.Client.Services;
using AiCodeAssistant.Client.Services.Managers;
using AiCodeAssistant.Client.Services.Rest;
using Microsoft.AspNetCore.Components.Authorization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<GraphRestService>();
builder.Services.AddScoped<GraphServiceManager>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddHttpClient();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
//app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
