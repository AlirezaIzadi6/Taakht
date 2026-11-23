using AuthMock.Domain;

namespace AuthMock.Infrastructure;

public static class SeedUsers
{
    public static IReadOnlyList<User> Create() =>
    [
        CreateUser("ali", "ali@example.com", "+989120000001", UserRole.User),
        CreateUser("sara", "sara@example.com", "+989120000002", UserRole.User),
        CreateUser("reza", "reza@example.com", "+989120000003", UserRole.User),
        CreateUser("mina", "mina@example.com", "+989120000004", UserRole.User),
        CreateUser("hossein", "hossein@example.com", "+989120000005", UserRole.User),
        CreateUser("zahra", "zahra@example.com", "+989120000006", UserRole.User),
        CreateUser("amir", "amir@example.com", "+989120000007", UserRole.User),
        CreateUser("neda", "neda@example.com", "+989120000008", UserRole.User),
        CreateUser("kian", "kian@example.com", "+989120000009", UserRole.User),
        CreateUser("admin", "admin@example.com", "+989120000010", UserRole.Admin),
    ];

    private static User CreateUser(string username, string email, string phone, UserRole role) =>
        new()
        {
            Id = username,
            Username = username,
            Email = email,
            Phone = phone,
            Role = role,
        };
}
