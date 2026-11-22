using AuthMock.Domain;
using FluentAssertions;

namespace AuthMock.Infrastructure.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void HashHasExpectedFormat()
    {
        // Arrange
        const string password = "dev-password";

        // Act
        var hash = Pbkdf2PasswordHasher.Hash(password);

        // Assert
        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("pbkdf2-sha256");
        parts[1].Should().Be("100000");
        parts[2].Should().NotBeNullOrEmpty();
        parts[3].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SamePasswordGetsDifferentSalts()
    {
        // Arrange
        const string password = "dev-password";

        // Act
        var first = Pbkdf2PasswordHasher.Hash(password);
        var second = Pbkdf2PasswordHasher.Hash(password);

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void SeedUsersHaveDistinctPasswordHashes()
    {
        // Arrange
        const int expectedCount = 10;

        // Act
        var users = SeedUsers.Create();

        // Assert
        users.Should().HaveCount(expectedCount);
        users.Should().OnlyContain(user => !string.IsNullOrWhiteSpace(user.PasswordHash));
        users.Select(user => user.PasswordHash).Should().OnlyHaveUniqueItems();
        users.Should().ContainSingle(user => user.Role == UserRole.Admin);
    }
}
