using System.Security.Claims;

namespace AiCodeAssistant.API.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var userIdValue =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated user id claim is missing or invalid.");
    }
}
