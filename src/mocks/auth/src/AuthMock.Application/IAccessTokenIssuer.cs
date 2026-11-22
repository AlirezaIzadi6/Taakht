using AuthMock.Domain;

namespace AuthMock.Application;

public interface IAccessTokenIssuer
{
    IssuedToken Issue(User user);
}
