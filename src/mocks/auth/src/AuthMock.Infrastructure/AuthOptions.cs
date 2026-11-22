namespace AuthMock.Infrastructure;

public sealed class AuthOptions
{
    public required string PrivateKeyPath { get; init; }

    public string KeyId { get; init; } = "dev-1";

    public string Issuer { get; init; } = "taakht-auth-mock";

    public string Audience { get; init; } = "taakht-api";

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(1);
}
