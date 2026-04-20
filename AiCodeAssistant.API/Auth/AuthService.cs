using System.Net.Mail;
using System.Security.Claims;
using AiCodeAssistant.Domain.Contracts.Auth;
using AiCodeAssistant.Domain.Persistence;
using AiCodeAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AiCodeAssistant.API.Auth;

public class AuthService
{
    private readonly CodeSightDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public AuthService(CodeSightDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        var emailAlreadyExists = await _dbContext.Users
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (emailAlreadyExists)
        {
            throw new AuthException("An account with that email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(existingUser => existingUser.Email == email, cancellationToken);

        if (user is null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new AuthException("Email or password is incorrect.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(_jwtOptions.ExpirationMinutes, 1));

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            ExpiresAt = expiresAt,
            Token = CreateToken(user, expiresAt)
        };
    }

    private string CreateToken(User user, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),
            new("sub", user.Id.ToString())
        };
        var securityKey = new SymmetricSecurityKey(_jwtOptions.GetSigningKeyBytes());
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Expires = expiresAt,
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string NormalizeEmail(string email)
    {
        var trimmedEmail = (email ?? string.Empty).Trim();
        try
        {
            var address = new MailAddress(trimmedEmail);
            if (!string.Equals(address.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthException("Enter a valid email address.");
            }
        }
        catch (FormatException)
        {
            throw new AuthException("Enter a valid email address.");
        }

        return trimmedEmail.ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new AuthException("Password must be at least 8 characters.");
        }
    }
}
