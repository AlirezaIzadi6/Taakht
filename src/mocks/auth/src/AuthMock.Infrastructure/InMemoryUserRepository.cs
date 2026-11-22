using AuthMock.Application;
using AuthMock.Domain;

namespace AuthMock.Infrastructure;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<string, User> _usersByUsername;

    public InMemoryUserRepository(IEnumerable<User> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        _usersByUsername = users.ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_usersByUsername.GetValueOrDefault(username));
    }
}
