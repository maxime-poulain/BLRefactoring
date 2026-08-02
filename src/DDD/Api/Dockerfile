FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore.
#
# The two Directory.*.props come first and are not optional. Central package management lives in
# Directory.Packages.props, so without it every PackageReference below has no version and restore
# fails; Directory.Build.props carries Nullable and ImplicitUsings, which would silently switch off
# rather than fail. `COPY src/ src/` further down does not reach either — they sit at the root.
COPY Directory.Build.props Directory.Packages.props ./
COPY BLRefactoring.slnx .
COPY src/BLRefactoring.Shared/BLRefactoring.Shared.csproj src/BLRefactoring.Shared/
COPY src/BLRefactoring.Shared.Domain/BLRefactoring.Shared.Domain.csproj src/BLRefactoring.Shared.Domain/
COPY src/BLRefactoring.Shared.Application/BLRefactoring.Shared.Application.csproj src/BLRefactoring.Shared.Application/
COPY src/BLRefactoring.Shared.Infrastructure/BLRefactoring.Shared.Infrastructure.csproj src/BLRefactoring.Shared.Infrastructure/
COPY src/BLRefactoring.Shared.Api/BLRefactoring.Shared.Api.csproj src/BLRefactoring.Shared.Api/
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

# The aspnet images ship a non-root account for exactly this; running as root is the default only
# because changing it would break images that write where they should not.
USER $APP_UID

ENTRYPOINT ["dotnet", "BLRefactoring.DDD.Api.dll"]
