FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props ./
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER $APP_UID

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UserManagementAPI.Api.dll"]