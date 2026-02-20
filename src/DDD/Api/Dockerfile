FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY BLRefactoring.slnx .
COPY src/BLRefactoring.Shared/BLRefactoring.Shared.csproj src/BLRefactoring.Shared/
COPY src/BLRefactoring.Shared.Domain/BLRefactoring.Shared.Domain.csproj src/BLRefactoring.Shared.Domain/
COPY src/BLRefactoring.Shared.Application/BLRefactoring.Shared.Application.csproj src/BLRefactoring.Shared.Application/
COPY src/BLRefactoring.Shared.Infrastructure/BLRefactoring.Shared.Infrastructure.csproj src/BLRefactoring.Shared.Infrastructure/
COPY src/DDD/Domain/BLRefactoring.DDD.Domain.csproj src/DDD/Domain/
COPY src/DDD/Application/BLRefactoring.DDD.Application.csproj src/DDD/Application/
COPY src/DDD/Infrastructure/BLRefactoring.DDD.Infrastructure.csproj src/DDD/Infrastructure/
COPY src/DDD/Api/BLRefactoring.DDD.Api.csproj src/DDD/Api/

RUN dotnet restore src/DDD/Api/BLRefactoring.DDD.Api.csproj

# Copy source code and publish
COPY src/ src/
RUN dotnet publish src/DDD/Api/BLRefactoring.DDD.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BLRefactoring.DDD.Api.dll"]
