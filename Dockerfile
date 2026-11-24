# Both images are pinned by digest as well as tag, so a build is reproducible
# and a new base arrives as a Dependabot pull request rather than silently (#83).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
WORKDIR /source

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY UserManagementAPI.sln ./
COPY src/UserManagementAPI.Domain/*.csproj                    src/UserManagementAPI.Domain/
COPY src/UserManagementAPI.Application/*.csproj               src/UserManagementAPI.Application/
COPY src/UserManagementAPI.Infrastructure/*.csproj            src/UserManagementAPI.Infrastructure/
COPY src/UserManagementAPI.Api/*.csproj                       src/UserManagementAPI.Api/
COPY tests/UserManagementAPI.Domain.UnitTests/*.csproj        tests/UserManagementAPI.Domain.UnitTests/
COPY tests/UserManagementAPI.Application.UnitTests/*.csproj   tests/UserManagementAPI.Application.UnitTests/
COPY tests/UserManagementAPI.IntegrationTests/*.csproj        tests/UserManagementAPI.IntegrationTests/
RUN dotnet restore UserManagementAPI.sln

COPY . .
RUN dotnet publish src/UserManagementAPI.Api/UserManagementAPI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21 AS runtime
WORKDIR /app

USER $APP_UID

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UserManagementAPI.Api.dll"]
