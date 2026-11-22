using AuthMock.Domain;

namespace AuthMock.Application;

public sealed class LoginHandler
{
    private readonly IUserRepository _users;
    private readonly IAccessTokenIssuer _tokenIssuer;

    public LoginHandler(IUserRepository users, IAccessTokenIssuer tokenIssuer)
    {
        _users = users;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return LoginResult.InvalidRequest(["username"]);
        }

        var user = await _users.FindByUsernameAsync(command.Username, cancellationToken);
        if (user is null)
        {
            return LoginResult.InvalidCredentials();
        }

        var token = _tokenIssuer.Issue(user);
        return LoginResult.Succeeded(token.AccessToken, token.ExpiresAt);
    }
}
