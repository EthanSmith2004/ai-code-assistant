using System.Net;
using System.Net.Http.Json;
using AiCodeAssistant.Domain.Contracts.Auth;

namespace AiCodeAssistant.Client.Services;

public class AuthService
{
    private const string ApiBaseUrl = "http://localhost:5217";
    private readonly HttpClient _httpClient;
    private readonly JwtAuthenticationStateProvider _authenticationStateProvider;

    public AuthService(
        HttpClient httpClient,
        JwtAuthenticationStateProvider authenticationStateProvider)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/register", request);
        return await HandleAuthResponseAsync(response, "register");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/login", request);
        return await HandleAuthResponseAsync(response, "login");
    }

    public async Task LogoutAsync()
    {
        await _authenticationStateProvider.ClearSessionAsync();
    }

    public async Task<string?> GetTokenAsync()
    {
        var session = await _authenticationStateProvider.GetSessionAsync();
        return session?.Token;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        return await _authenticationStateProvider.GetSessionAsync() is not null;
    }

    private async Task<AuthResponse> HandleAuthResponseAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await CreateAuthErrorMessageAsync(response, operation);
            throw new InvalidOperationException(errorMessage);
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
                           ?? throw new InvalidOperationException("The API returned an empty auth response.");

        await _authenticationStateProvider.SaveSessionAsync(new AuthSession
        {
            UserId = authResponse.UserId,
            Email = authResponse.Email,
            Token = authResponse.Token,
            ExpiresAt = authResponse.ExpiresAt
        });

        return authResponse;
    }

    private static async Task<string> CreateAuthErrorMessageAsync(HttpResponseMessage response, string operation)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(errorBody))
        {
            return errorBody.Trim('"');
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Email or password is incorrect.",
            HttpStatusCode.Conflict => "An account with that email already exists.",
            _ => $"Could not {operation}. The API returned {(int)response.StatusCode} {response.StatusCode}."
        };
    }
}
