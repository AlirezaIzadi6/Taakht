using AuthMock.Domain;
using FluentAssertions;

namespace AuthMock.Infrastructure.Tests;

public sealed class InMemoryUserRepositoryTests
{
    private readonly InMemoryUserRepository _repository = new(SeedUsers.Create());

    [Fact]
    public void SeedsTenUsers()
    {
        // Arrange
        const int expectedCount = 10;

        // Act
        var users = SeedUsers.Create();

        // Assert
        users.Should().HaveCount(expectedCount);
        users.Should().ContainSingle(user => user.Role == UserRole.Admin);
    }

    [Fact]
    public async Task FindsUserByUsernameIgnoringCase()
    {
        // Arrange
        const string username = "ALI";

        // Act
        var user = await _repository.FindByUsernameAsync(username, CancellationToken.None);

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("ali");
        user.Id.Should().Be("ali");
    }

    [Fact]
    public async Task ReturnsNullForUnknownUsername()
    {
        // Arrange
        const string username = "nobody";

        // Act
        var user = await _repository.FindByUsernameAsync(username, CancellationToken.None);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task ObservesCancellation()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act
        Func<Task> act = () => _repository.FindByUsernameAsync("ali", source.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
