using AuthMock.Application;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Taakht.Auth.V1;

namespace AuthMock.Api;

public sealed class AuthGrpcService : AuthService.AuthServiceBase
{
    private readonly LoginHandler _loginHandler;

    public AuthGrpcService(LoginHandler loginHandler)
    {
        _loginHandler = loginHandler;
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var command = new LoginCommand(request.Username, request.Password);
        var result = await _loginHandler.HandleAsync(command, context.CancellationToken);

        return result.Status switch
        {
            LoginStatus.Success => new LoginResponse
            {
                AccessToken = result.AccessToken!,
                ExpiresAt = Timestamp.FromDateTimeOffset(result.ExpiresAt),
            },
            LoginStatus.InvalidRequest => throw GrpcErrors.InvalidArgument(result.InvalidFields ?? []),
            _ => throw new RpcException(
                new Status(StatusCode.Unauthenticated, "invalid username or password")),
        };
    }
}
