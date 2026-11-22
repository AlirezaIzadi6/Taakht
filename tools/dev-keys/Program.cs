using System.Security.Cryptography;
using System.Text.Json;

const string KeyId = "dev-1";

var outputDirectory = ".dev-keys";
var force = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length:
            outputDirectory = args[++i];
            break;
        case "--force":
            force = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

var privateKeyPath = Path.Combine(outputDirectory, "jwt-private.pem");
var jwksPath = Path.Combine(outputDirectory, "jwks.json");

if (!force && File.Exists(privateKeyPath) && File.Exists(jwksPath))
{
    Console.WriteLine($"Dev keys already exist in {outputDirectory}; leaving them untouched (pass --force to regenerate).");
    return 0;
}

Directory.CreateDirectory(outputDirectory);

using var rsa = RSA.Create(2048);
File.WriteAllText(privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(privateKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}

var publicParameters = rsa.ExportParameters(includePrivateParameters: false);
var jwks = new
{
    keys = new[]
    {
        new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = KeyId,
            n = Base64UrlEncode(publicParameters.Modulus!),
            e = Base64UrlEncode(publicParameters.Exponent!),
        },
    },
};

var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(jwksPath, JsonSerializer.Serialize(jwks, options) + Environment.NewLine);

Console.WriteLine($"Wrote {privateKeyPath}");
Console.WriteLine($"Wrote {jwksPath}");
return 0;

static string Base64UrlEncode(byte[] value) =>
    Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
