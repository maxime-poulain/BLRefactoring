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
COPY TrainingHub.slnx .
COPY src/TrainingHub.Shared/TrainingHub.Shared.csproj src/TrainingHub.Shared/
COPY src/TrainingHub.Shared.Domain/TrainingHub.Shared.Domain.csproj src/TrainingHub.Shared.Domain/
COPY src/TrainingHub.Shared.Application/TrainingHub.Shared.Application.csproj src/TrainingHub.Shared.Application/
COPY src/TrainingHub.Shared.Infrastructure/TrainingHub.Shared.Infrastructure.csproj src/TrainingHub.Shared.Infrastructure/
COPY src/TrainingHub.Shared.Api/TrainingHub.Shared.Api.csproj src/TrainingHub.Shared.Api/
COPY src/DDDWithCqrs/Domain/TrainingHub.DDDWithCqrs.Domain.csproj src/DDDWithCqrs/Domain/
COPY src/DDDWithCqrs/Application/TrainingHub.DDDWithCqrs.Application.csproj src/DDDWithCqrs/Application/
COPY src/DDDWithCqrs/Infrastructure/TrainingHub.DDDWithCqrs.Infrastructure.csproj src/DDDWithCqrs/Infrastructure/
COPY src/DDDWithCqrs/Api/TrainingHub.DDDWithCqrs.Api.csproj src/DDDWithCqrs/Api/

RUN dotnet restore src/DDDWithCqrs/Api/TrainingHub.DDDWithCqrs.Api.csproj

# Copy source code and publish
COPY src/ src/
RUN dotnet publish src/DDDWithCqrs/Api/TrainingHub.DDDWithCqrs.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

# For the compose healthcheck alone: the aspnet image ships neither curl nor wget, and a check
# that guesses at tooling is how the SeaweedFS one went wrong once. Installed as root, before the
# user drops. See ADR 0037.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The file sink writes under /app/logs, and /app belongs to root while the process does not run
# as root. The directory has to exist and be writable before the user drops, because Serilog
# reports a sink it cannot open through SelfLog — which nobody reads — and then logs to the
# console alone, looking healthy.
RUN mkdir logs && chown $APP_UID logs

# The aspnet images ship a non-root account for exactly this; running as root is the default only
# because changing it would break images that write where they should not.
USER $APP_UID

ENTRYPOINT ["dotnet", "TrainingHub.DDDWithCqrs.Api.dll"]
