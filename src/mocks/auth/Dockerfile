FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY api/ api/
COPY src/mocks/auth/ src/mocks/auth/
RUN dotnet publish src/mocks/auth/src/AuthMock.Api/AuthMock.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV GRPC_ADDR=:9204
ENV HTTP_ADDR=:8204
USER $APP_UID
EXPOSE 9204 8204
ENTRYPOINT ["dotnet", "AuthMock.Api.dll"]
