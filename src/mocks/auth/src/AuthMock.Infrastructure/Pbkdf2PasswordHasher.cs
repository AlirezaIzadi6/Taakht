using System.Globalization;
using System.Security.Cryptography;

namespace AuthMock.Infrastructure;

public static class Pbkdf2PasswordHasher
{
    private const string _prefix = "pbkdf2-sha256";
    private const int _saltSize = 16;
    private const int _keySize = 32;
    private const int _iterations = 100_000;

    private static readonly HashAlgorithmName _algorithm = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(_saltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, _algorithm, _keySize);
        return $"{_prefix}${_iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}
