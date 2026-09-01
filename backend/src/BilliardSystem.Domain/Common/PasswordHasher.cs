using System.Security.Cryptography;
using System.Text;

namespace BilliardSystem.Domain.Common;

public static class PasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "v2.";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"{Prefix}{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[1]);
            var expectedHash = Convert.FromBase64String(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        // Legacy SHA-256 (unsalted) — for migration only
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(legacyHash));
    }

    public static bool IsLegacyHash(string hash) => !hash.StartsWith(Prefix, StringComparison.Ordinal);
}
