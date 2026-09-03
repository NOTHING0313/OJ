using System.Security.Cryptography;
using System.Text;

namespace OnlineJudge.Infrastructure.Auth;

public sealed class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int CurrentIterations = 600_000;
    private const int MinimumAcceptedIterations = 10_000;
    private const int MaximumAcceptedIterations = 2_000_000;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var normalizedPassword = password.Normalize(NormalizationForm.FormC);
        var hash = Rfc2898DeriveBytes.Pbkdf2(normalizedPassword, salt, CurrentIterations, HashAlgorithmName.SHA256, HashSize);

        return $"v2.{CurrentIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            var parts = passwordHash.Split('.');
            if (parts.Length != 4
                || (parts[0] != "v1" && parts[0] != "v2")
                || !int.TryParse(parts[1], out var iterations)
                || iterations is < MinimumAcceptedIterations or > MaximumAcceptedIterations)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expectedHash.Length != HashSize)
            {
                return false;
            }

            var candidate = parts[0] == "v2" ? password.Normalize(NormalizationForm.FormC) : password;
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(candidate, salt, iterations, HashAlgorithmName.SHA256, HashSize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or CryptographicException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string passwordHash)
    {
        var parts = passwordHash.Split('.');
        return parts.Length != 4
            || parts[0] != "v2"
            || !int.TryParse(parts[1], out var iterations)
            || iterations != CurrentIterations;
    }
}
