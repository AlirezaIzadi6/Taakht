using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthMock.Application;
using AuthMock.Domain;
using Microsoft.IdentityModel.Tokens;

namespace AuthMock.Infrastructure;

public sealed class RsaAccessTokenIssuer : IAccessTokenIssuer, IDisposable
{
    private readonly AuthOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly RSA _rsa;
    private readonly SigningCredentials _credentials;
    private readonly JwtSecurityTokenHandler _handler = new();

    public RsaAccessTokenIssuer(AuthOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _rsa = RSA.Create();
        _rsa.ImportFromPem(File.ReadAllText(options.PrivateKeyPath));
        _credentials = new SigningCredentials(
            new RsaSecurityKey(_rsa) { KeyId = options.KeyId },
            SecurityAlgorithms.RsaSha256);
    }

    public IssuedToken Issue(User user)
    {
        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(_options.TokenLifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim("role", RoleClaimValue(user.Role)),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _credentials);

        return new IssuedToken(_handler.WriteToken(token), expiresAt);
    }

    public void Dispose()
    {
        _rsa.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string RoleClaimValue(UserRole role) => role == UserRole.Admin ? "admin" : "user";
}
