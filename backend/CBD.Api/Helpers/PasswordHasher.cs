using System.Security.Cryptography;
using System.Text;

namespace CBD.Api.Helpers;

public static class PasswordHasher
{
    public static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
