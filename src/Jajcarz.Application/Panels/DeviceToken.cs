using System.Security.Cryptography;
using System.Text;

namespace Jajcarz.Application.Panels;

public static class DeviceToken
{
    private const int TokenBytes = 32;

    /// <summary>Znaki bezpieczne w nagłówku HTTP i w pliku secrets.h.</summary>
    public static string Create() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>Token jest długim losowym ciągiem, więc szybki skrót wystarcza. Baza nie zna samego tokenu.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
