using System.Globalization;
using AuthMock.Domain;
using FluentAssertions;

namespace AuthMock.Infrastructure.Tests;

public sealed class SeedUsersTests
{
    [Fact]
    public void LoadsAllSeededUsers()
    {
        // Arrange
        var expectedNamed = new (string Id, string Email, string Phone, UserRole Role)[]
        {
            ("ali", "ali@example.com", "+989120000001", UserRole.User),
            ("sara", "sara@example.com", "+989120000002", UserRole.User),
            ("reza", "reza@example.com", "+989120000003", UserRole.User),
            ("mina", "mina@example.com", "+989120000004", UserRole.User),
            ("hossein", "hossein@example.com", "+989120000005", UserRole.User),
            ("zahra", "zahra@example.com", "+989120000006", UserRole.User),
            ("amir", "amir@example.com", "+989120000007", UserRole.User),
            ("neda", "neda@example.com", "+989120000008", UserRole.User),
            ("kian", "kian@example.com", "+989120000009", UserRole.User),
            ("admin", "admin@example.com", "+989120000010", UserRole.Admin),
        };

        // Act
        var users = SeedUsers.Load(TestSeedFile.FilePath);

        // Assert
        users.Should().HaveCount(2010);
        users.Take(10).Select(user => (user.Id, user.Email, user.Phone, user.Role)).Should().Equal(expectedNamed);
        users.Skip(10).Select(user => user.Username).Should().Equal(
            Enumerable.Range(1, 2000).Select(index => "u" + index.ToString("0000", CultureInfo.InvariantCulture)));
        users.Should().OnlyContain(user => user.Username == user.Id);
        users.Select(user => user.Username).Should().OnlyHaveUniqueItems();
        users.Select(user => user.Email).Should().OnlyHaveUniqueItems();
        users.Select(user => user.Phone).Should().OnlyHaveUniqueItems();
        users.Should().ContainSingle(user => user.Role == UserRole.Admin);
        users.Count(user => user.Role == UserRole.User).Should().Be(2009);
    }

    [Fact]
    public void MissingFileThrows()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"taakht-users-{Guid.NewGuid():N}.json");

        // Act
        Action act = () => SeedUsers.Load(path);

        // Assert
        act.Should().Throw<FileNotFoundException>().WithMessage($"*{path}*");
    }

    [Fact]
    public void InvalidJsonThrows()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"taakht-users-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");

        try
        {
            // Act
            Action act = () => SeedUsers.Load(path);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*invalid*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DuplicateUsernameThrows()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"taakht-users-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            """
            [
              {"id":"a","username":"ali","email":"a@example.com","phone":"1","role":"user"},
              {"id":"b","username":"ALI","email":"b@example.com","phone":"2","role":"user"}
            ]
            """);

        try
        {
            // Act
            Action act = () => SeedUsers.Load(path);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate username*");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
