using System.Security.Cryptography;

namespace Jajcarz.Application.Accounts;

/// <summary>PBKDF2 z SHA-512. Liczba iteracji jest w zapisanym haśle, więc można ją później podnieść.</summary>
public static class PasswordHasher
{
    private const string Scheme = "pbkdf2-sha512";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int PartCount = 4;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);
        return string.Join('$', Scheme, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != PartCount || parts[0] != Scheme)
        {
            return false;
        }
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, Convert.FromBase64String(parts[2]), int.Parse(parts[1]), Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
