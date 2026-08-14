FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# TLS only, and the two API images are the ones that differ rather than this one. Those speak HTTP
# because nothing but this host calls them, inside the compose network; this host is the one a
# browser opens, and the browser is what needs TLS. Serving plain HTTP here as well would add an
# address that UseHttpsRedirection bounces and nothing else reaches.
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore.
#
# The three root files come first and none of them is optional. Central package management lives in
# Directory.Packages.props, so without it every PackageReference below has no version and restore
# fails; Directory.Build.props carries Nullable and ImplicitUsings, which would silently switch off
# rather than fail; .editorconfig carries every analyzer severity this repository chose, and without
# it the build stage compiles the same source under the analyzers' own defaults — which stays green
# for as long as those choices only tighten a rule, and goes red the first time one of them
# deliberately loosens one. `COPY src/ src/` further down reaches none of the three — they sit at
# the root.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY TrainingHub.slnx .
COPY src/TrainingHub.GeneratedClients/TrainingHub.GeneratedClients.csproj src/TrainingHub.GeneratedClients/
COPY src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/TrainingHub.Blazor.Client.csproj src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/
COPY src/Web/TrainingHub.Blazor/TrainingHub.Blazor/TrainingHub.Blazor.csproj src/Web/TrainingHub.Blazor/TrainingHub.Blazor/

RUN dotnet restore src/Web/TrainingHub.Blazor/TrainingHub.Blazor/TrainingHub.Blazor.csproj

# Copy source code and publish.
#
# One publish produces two applications: this host, and the WebAssembly client it serves as static
# assets. Neither AOT compilation nor trimming is switched on, so the `wasm-tools` workload is not
# needed here — which is the question anybody reading a Blazor Dockerfile asks first, and the
# answer would change the day a project turns either of them on.
COPY src/ src/
RUN dotnet publish src/Web/TrainingHub.Blazor/TrainingHub.Blazor/TrainingHub.Blazor.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# TLS inside the container, and it is not optional dressing. The session cookie is `__Host-bff`
# with Secure always on, so a browser refuses to store it over plain HTTP: this host served without
# TLS renders every page and signs nobody in, silently. The certificate is the developer's own,
# exported from `dotnet dev-certs https -ep` and mounted read-only by compose — the machine's
# browser already trusts that root, so there is nothing to accept by hand. The path and the
# password arrive as environment variables. See ADR 0065.
ENV ASPNETCORE_URLS=https://+:8443

# For the compose healthcheck alone: the aspnet image ships neither curl nor wget, and a check
# that guesses at tooling is how the SeaweedFS one went wrong once. Installed as root, before the
# user drops. See ADR 0037.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The aspnet images ship a non-root account for exactly this; running as root is the default only
# because changing it would break images that write where they should not. No logs directory is
# prepared here, unlike the two API images: this host logs to the console alone. Serilog is the two
# API hosts' dependency and not this one's — it owns no domain behavior and no persistence, and its
# diagnostics have not yet earned one (ADR 0026).
USER $APP_UID

ENTRYPOINT ["dotnet", "TrainingHub.Blazor.dll"]
