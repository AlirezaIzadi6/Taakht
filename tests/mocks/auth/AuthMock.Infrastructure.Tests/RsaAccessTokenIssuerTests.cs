using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using AuthMock.Domain;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace AuthMock.Infrastructure.Tests;

public sealed class RsaAccessTokenIssuerTests
{
    private const string _keyId = "test-key";
    private const string _issuer = "taakht-auth-mock";
    private const string _audience = "taakht-api";

    [Fact]
    public void IssuesTokenWithExpectedClaims()
    {
        // Arrange
        using var keyFile = new TempKeyFile();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        using var issuer = CreateIssuer(keyFile, now);
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = CreateValidationParameters(keyFile);

        // Act
        var token = issuer.Issue(CreateUser(UserRole.User));
        handler.ValidateToken(token.AccessToken, validationParameters, out var validatedToken);

        // Assert
        token.ExpiresAt.Should().Be(now.AddHours(1));
        validatedToken.Should().BeOfType<JwtSecurityToken>();

        var parts = token.AccessToken.Split('.');
        parts.Should().HaveCount(3);
        using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[0]));
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[1]));

        header.RootElement.GetProperty("kid").GetString().Should().Be(_keyId);
        header.RootElement.GetProperty("alg").GetString().Should().Be("RS256");
        payload.RootElement.GetProperty("sub").GetString().Should().Be("ali");
        payload.RootElement.GetProperty("role").GetString().Should().Be("user");
        payload.RootElement.GetProperty("iss").GetString().Should().Be(_issuer);
        payload.RootElement.GetProperty("aud").GetString().Should().Be(_audience);
        payload.RootElement.TryGetProperty("exp", out _).Should().BeTrue();
        payload.RootElement.TryGetProperty("iat", out _).Should().BeTrue();
    }

    [Fact]
    public void AdminUserGetsAdminRoleClaim()
    {
        // Arrange
        using var keyFile = new TempKeyFile();
        using var issuer = CreateIssuer(keyFile, DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var token = issuer.Issue(CreateUser(UserRole.Admin));

        // Assert
        var parts = token.AccessToken.Split('.');
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[1]));
        payload.RootElement.GetProperty("role").GetString().Should().Be("admin");
    }

    private static RsaAccessTokenIssuer CreateIssuer(TempKeyFile keyFile, DateTimeOffset now) =>
        new(
            new AuthOptions
            {
                PrivateKeyPath = keyFile.PrivateKeyPath,
                KeyId = _keyId,
                Issuer = _issuer,
                Audience = _audience,
                TokenLifetime = TimeSpan.FromHours(1),
            },
            new FixedTimeProvider(now));

    private static TokenValidationParameters CreateValidationParameters(TempKeyFile keyFile) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(keyFile.Rsa) { KeyId = _keyId },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

    private static User CreateUser(UserRole role) =>
        new()
        {
            Id = "ali",
            Username = "ali",
            Email = "ali@example.com",
            Phone = "+989120000001",
            PasswordHash = "unused",
            Role = role,
        };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class TempKeyFile : IDisposable
    {
        private readonly string _directory;
        private readonly RSA _rsa = RSA.Create(2048);

        public TempKeyFile()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"taakht-auth-keys-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            PrivateKeyPath = System.IO.Path.Combine(_directory, "jwt-private.pem");
            File.WriteAllText(PrivateKeyPath, _rsa.ExportPkcs8PrivateKeyPem());
        }

        public string PrivateKeyPath { get; }

        public RSA Rsa => _rsa;

        public void Dispose()
        {
            _rsa.Dispose();

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
