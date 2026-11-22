using System.Globalization;
using AuthMock.Api;
using AuthMock.Application;
using AuthMock.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

var authOptions = ReadAuthOptions(builder.Configuration);
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUserRepository>(_ => new InMemoryUserRepository(SeedUsers.Create()));
builder.Services.AddSingleton<IAccessTokenIssuer, RsaAccessTokenIssuer>();
builder.Services.AddSingleton<LoginHandler>();
builder.Services.AddGrpc();

var grpcPort = ReadPort(builder.Configuration["GRPC_ADDR"], 9204);
var httpPort = ReadPort(builder.Configuration["HTTP_ADDR"], 8204);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
});

var app = builder.Build();

app.MapGrpcService<AuthGrpcService>();
app.MapGet("/healthz", () => Results.Ok());

app.Run();

static int ReadPort(string? address, int fallback)
{
    var value = address?.Split(':')[^1];
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
        ? port
        : fallback;
}

static AuthOptions ReadAuthOptions(IConfiguration configuration)
{
    var privateKeyPath = configuration["JWT_PRIVATE_KEY_PATH"];
    if (string.IsNullOrWhiteSpace(privateKeyPath))
    {
        throw new InvalidOperationException(
            "JWT_PRIVATE_KEY_PATH is required and must point to the RS256 private key PEM file.");
    }

    var tokenLifetime = TimeSpan.FromHours(1);
    var configuredLifetime = configuration["TOKEN_TTL"];
    if (!string.IsNullOrWhiteSpace(configuredLifetime)
        && !TimeSpan.TryParse(configuredLifetime, CultureInfo.InvariantCulture, out tokenLifetime))
    {
        throw new InvalidOperationException(
            $"TOKEN_TTL '{configuredLifetime}' is not a valid TimeSpan (for example 01:00:00).");
    }

    return new AuthOptions
    {
        PrivateKeyPath = privateKeyPath,
        KeyId = configuration["JWT_KEY_ID"] ?? "dev-1",
        Issuer = configuration["JWT_ISSUER"] ?? "taakht-auth-mock",
        Audience = configuration["JWT_AUDIENCE"] ?? "taakht-api",
        TokenLifetime = tokenLifetime,
    };
}

public partial class Program;
