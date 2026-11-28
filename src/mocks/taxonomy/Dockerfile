FROM bufbuild/buf:latest AS codegen
WORKDIR /src
COPY buf.yaml buf.gen.yaml buf.lock ./
COPY api/ api/
COPY gen/go/go.mod gen/go/go.sum gen/go/
RUN buf generate

FROM golang:1.27-alpine AS build
WORKDIR /src
COPY --from=codegen /src/gen/go/ gen/go/
COPY src/mocks/taxonomy/ src/mocks/taxonomy/
WORKDIR /src/src/mocks/taxonomy
RUN CGO_ENABLED=0 go build -o /out/taxonomy .

FROM alpine:3 AS final
WORKDIR /app
COPY --from=build /out/taxonomy /app/taxonomy
COPY src/mocks/taxonomy/data/ /app/data/
ENV GRPC_ADDR=:9201
ENV HTTP_ADDR=:8201
EXPOSE 9201 8201
ENTRYPOINT ["/app/taxonomy"]
