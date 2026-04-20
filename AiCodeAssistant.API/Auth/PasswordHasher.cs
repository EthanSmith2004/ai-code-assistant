using System.Globalization;
using System.Security.Cryptography;

namespace AiCodeAssistant.API.Auth;

public static class PasswordHasher
{
    private const string Format = "PBKDF2-SHA256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            "$",
            Format,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 ||
            !string.Equals(parts[0], Format, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
