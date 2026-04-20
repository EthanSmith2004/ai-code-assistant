using System.Text;

namespace AiCodeAssistant.API.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 120;

    public byte[] GetSigningKeyBytes()
    {
        var keyBytes = Encoding.UTF8.GetBytes(SigningKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
        }

        return keyBytes;
    }
}
