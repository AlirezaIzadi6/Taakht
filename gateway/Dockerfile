FROM bufbuild/buf:latest AS descriptor
WORKDIR /src
COPY buf.yaml buf.lock ./
COPY api/ api/
RUN buf build -o /descriptor.binpb

FROM envoyproxy/envoy:v1.34-latest
COPY gateway/envoy.yaml /etc/envoy/envoy.yaml
COPY --from=descriptor /descriptor.binpb /etc/envoy/descriptor.binpb
