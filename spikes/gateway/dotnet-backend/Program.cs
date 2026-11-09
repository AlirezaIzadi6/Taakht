using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Taakht.Hello.V1;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(9090, l => l.Protocols = HttpProtocols.Http2));
builder.Services.AddGrpc();
var app = builder.Build();
app.MapGrpcService<HelloImpl>();
app.Run();

class HelloImpl : HelloService.HelloServiceBase
{
    public override Task<GetGreetingResponse> GetGreeting(GetGreetingRequest request, ServerCallContext context)
    {
        var uid = context.RequestHeaders.GetValue("x-user-id");
        Console.WriteLine($"SEEN dotnet x-user-id=[{uid}]");
        return Task.FromResult(new GetGreetingResponse { Message = "Hello from .NET " + request.Name });
    }
}
