namespace AuthMock.Domain;

public sealed class User
{
    public required string Id { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Phone { get; init; }

    public required UserRole Role { get; init; }
}
