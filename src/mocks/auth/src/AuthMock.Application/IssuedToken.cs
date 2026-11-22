namespace AuthMock.Application;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
