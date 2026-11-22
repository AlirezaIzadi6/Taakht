using AuthMock.Domain;

namespace AuthMock.Application;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
}
