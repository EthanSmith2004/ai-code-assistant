using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace AiCodeAssistant.Client.Services;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "codesight.auth";
    private readonly ProtectedLocalStorage _protectedLocalStorage;
    private AuthSession? _cachedSession;

    public JwtAuthenticationStateProvider(ProtectedLocalStorage protectedLocalStorage)
    {
        _protectedLocalStorage = protectedLocalStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await GetSessionAsync();
        return CreateAuthenticationState(session);
    }

    public async Task<AuthSession?> GetSessionAsync()
    {
        if (IsValidSession(_cachedSession))
        {
            return _cachedSession;
        }

        try
        {
            var result = await _protectedLocalStorage.GetAsync<AuthSession>(StorageKey);
            if (!result.Success || !IsValidSession(result.Value))
            {
                _cachedSession = null;
                return null;
            }

            _cachedSession = result.Value;
            return _cachedSession;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is JSException ||
            exception is CryptographicException)
        {
            _cachedSession = null;
            return null;
        }
    }

    public async Task SaveSessionAsync(AuthSession session)
    {
        _cachedSession = session;
        await _protectedLocalStorage.SetAsync(StorageKey, session);
        NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState(session)));
    }

    public async Task ClearSessionAsync()
    {
        _cachedSession = null;

        try
        {
            await _protectedLocalStorage.DeleteAsync(StorageKey);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is JSException ||
            exception is CryptographicException)
        {
        }

        NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState(null)));
    }

    private static AuthenticationState CreateAuthenticationState(AuthSession? session)
    {
        if (!IsValidSession(session) || session is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Email, session.Email),
            new(ClaimTypes.Name, session.Email)
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "CodeSightJwt");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static bool IsValidSession(AuthSession? session)
    {
        return session is not null &&
               !string.IsNullOrWhiteSpace(session.Token) &&
               session.ExpiresAt.ToUniversalTime() > DateTime.UtcNow.AddSeconds(30);
    }
}
