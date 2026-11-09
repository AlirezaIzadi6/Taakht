FROM golang:1.26-alpine AS build
WORKDIR /src
COPY . .
RUN go mod tidy && CGO_ENABLED=0 go build -o /out/server .

FROM alpine:3
COPY --from=build /out/server /server
CMD ["/server"]
