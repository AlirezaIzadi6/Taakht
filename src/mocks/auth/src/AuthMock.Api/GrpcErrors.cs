using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;

namespace AuthMock.Api;

internal static class GrpcErrors
{
    public static RpcException InvalidArgument(IEnumerable<string> fields)
    {
        var badRequest = new BadRequest();
        foreach (var field in fields)
        {
            badRequest.FieldViolations.Add(new BadRequest.Types.FieldViolation
            {
                Field = field,
                Description = $"{field} is required.",
            });
        }

        var status = new Google.Rpc.Status
        {
            Code = (int)StatusCode.InvalidArgument,
            Message = "Request validation failed.",
        };
        status.Details.Add(Any.Pack(badRequest));

        var trailers = new Metadata
        {
            { "grpc-status-details-bin", status.ToByteArray() },
        };

        return new RpcException(
            new Grpc.Core.Status(StatusCode.InvalidArgument, "Request validation failed."),
            trailers);
    }
}
