namespace AuthMock.Application;

public enum LoginStatus
{
    Success,
    InvalidRequest,
    InvalidCredentials,
}

public sealed record LoginResult(
    LoginStatus Status,
    string? AccessToken = null,
    DateTimeOffset ExpiresAt = default,
    IReadOnlyList<string>? InvalidFields = null)
{
    public static LoginResult Succeeded(string accessToken, DateTimeOffset expiresAt) =>
        new(LoginStatus.Success, accessToken, expiresAt);

    public static LoginResult InvalidRequest(IReadOnlyList<string> invalidFields) =>
        new(LoginStatus.InvalidRequest, InvalidFields: invalidFields);

    public static LoginResult InvalidCredentials() => new(LoginStatus.InvalidCredentials);
}
