using AuthMock.Domain;
using FluentAssertions;
using NSubstitute;

namespace AuthMock.Application.Tests;

public sealed class LoginHandlerTests
{
    private static readonly DateTimeOffset _expiresAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAccessTokenIssuer _tokens = Substitute.For<IAccessTokenIssuer>();

    [Fact]
    public async Task ValidUsernameReturnsToken()
    {
        // Arrange
        var user = CreateUser();
        _users.FindByUsernameAsync("ali", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));
        _tokens.Issue(user).Returns(new IssuedToken("token-ali", _expiresAt));
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(new LoginCommand("ali", "ignored"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(LoginStatus.Success);
        result.AccessToken.Should().Be("token-ali");
        result.ExpiresAt.Should().Be(_expiresAt);
        await _users.Received(1).FindByUsernameAsync("ali", Arg.Any<CancellationToken>());
        _tokens.Received(1).Issue(user);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    public async Task PasswordIsNotChecked(string password)
    {
        // Arrange
        var user = CreateUser();
        _users.FindByUsernameAsync("ali", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));
        _tokens.Issue(user).Returns(new IssuedToken("token-ali", _expiresAt));
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(new LoginCommand("ali", password), CancellationToken.None);

        // Assert
        result.Status.Should().Be(LoginStatus.Success);
        _tokens.Received(1).Issue(user);
    }

    [Fact]
    public async Task UnknownUserReturnsInvalidCredentials()
    {
        // Arrange
        _users.FindByUsernameAsync("nobody", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(null));
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(new LoginCommand("nobody", "ignored"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(LoginStatus.InvalidCredentials);
        result.AccessToken.Should().BeNull();
        _tokens.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingUsernameReturnsInvalidRequest(string username)
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.HandleAsync(new LoginCommand(username, "ignored"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(LoginStatus.InvalidRequest);
        result.InvalidFields.Should().NotBeNull();
        result.InvalidFields!.Should().Equal("username");
        _users.ReceivedCalls().Should().BeEmpty();
        _tokens.ReceivedCalls().Should().BeEmpty();
    }

    private LoginHandler CreateHandler() => new(_users, _tokens);

    private static User CreateUser() =>
        new()
        {
            Id = "ali",
            Username = "ali",
            Email = "ali@example.com",
            Phone = "+989120000001",
            Role = UserRole.User,
        };
}
