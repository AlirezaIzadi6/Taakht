# Evidence: API contract in Protobuf

Date: 2026-10-06. Backs the ADR "Define the API contract in Protobuf".

## Setup

One `.proto` with `google.api.http` annotations, `buf` (lint, breaking, generate), a Go gRPC service, a .NET gRPC service built with `Grpc.Tools`, both behind the same Envoy (see [edge-gateway.md](edge-gateway.md)). The .NET service used .NET 9 images.

## Results

| Check | Result |
|---|---|
| Go service from the proto | Works |
| .NET service from the same proto (`Grpc.Tools`) | Works after the fix below; answers REST calls through Envoy with the same JWT and `x-user-id` behaviour as the Go service |
| `buf breaking`: add a field | Accepted |
| `buf breaking`: change a field type, renumber a field, rename an RPC | Each rejected with a precise message and line |
| `buf lint` | Passed on the sample |

## .NET gotcha

The C# generated for an annotated proto references `Google.Api`, so the build fails with `CS0234` unless the Google API protos are compiled too. Copy them with `buf export buf.build/googleapis/googleapis -o third_party` and add:

```xml
<Protobuf Include="api/proto/taakht/hello/v1/hello.proto" ProtoRoot="api/proto"
          AdditionalImportDirs="third_party" GrpcServices="Server" />
<Protobuf Include="third_party/google/api/annotations.proto" ProtoRoot="third_party" GrpcServices="None" />
<Protobuf Include="third_party/google/api/http.proto" ProtoRoot="third_party" GrpcServices="None" />
```

## Not verified

- .NET 10: the spike targets .NET 9. The same service builds and starts on .NET 10 (checked 2026-11-12); the REST calls through Envoy were not re-run on it.

- The alternatives (OpenAPI-first, code-first per language) were reasoned about, not tried.
