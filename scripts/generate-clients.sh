#!/usr/bin/env bash
#
# Regenerates the typed HTTP client in src/BLRefactoring.GeneratedClients from the API's own
# OpenAPI document.
#
# One entry point, used identically on a developer machine and in CI — that is the whole reason
# this is a script rather than an MSBuild target. An ordinary `dotnet build` regenerates nothing,
# so nobody pays for generation on every compile; the client is refreshed when someone decides to.
#
# Two steps, in order:
#   1. Build the layered API host with document emission turned on. The document lands next to the
#      project as BLRefactoring.DDD.Api.json. OPENAPI_GENERATION stops that build from applying
#      migrations — producing the document loads the host, so everything before app.Run() runs.
#   2. Run the pinned NSwag CLI over generator.nswag, which reads that document.
#
# CI runs this on every push and every pull request, and commits the result when it differs: the
# API is the source of truth, so a controller change carries its client with it rather than waiting
# for someone to notice. Only a pull request from a fork — where the token cannot write — turns the
# same difference into a failure instead.
#
# See ADR 0008.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

api_project="src/DDD/Api/BLRefactoring.DDD.Api.csproj"
document="src/DDD/Api/BLRefactoring.DDD.Api.json"
client="src/BLRefactoring.GeneratedClients/Clients.Generated.cs"

echo "==> Restoring the pinned NSwag CLI"
dotnet tool restore

echo "==> Building $api_project and emitting its OpenAPI document"
OPENAPI_GENERATION=1 ASPNETCORE_ENVIRONMENT=Development \
    dotnet build "$api_project" \
    --configuration Debug \
    -p:OpenApiGenerateDocuments=true

if [[ ! -f "$document" ]]; then
    echo "The build did not produce $document." >&2
    echo "Microsoft.Extensions.ApiDescription.Server writes it from the API project; check that" >&2
    echo "the package is referenced and that OpenApiDocumentsDirectory still points at the" >&2
    echo "project folder." >&2
    exit 1
fi

echo "==> Generating $client"
dotnet nswag run generator.nswag

# NSwag exits 0 whether it wrote a client, left an identical one in place, or selected nothing at
# all — the last of which `set -e` cannot see. Checking that the file exists and holds something
# separates "no change" from "no output": the first is the normal case, the second means the
# generator's configuration stopped naming an output and every consumer is about to fail to
# compile for a reason this script could have named.
if [[ ! -s "$client" ]]; then
    echo "The generator produced no client at $client." >&2
    echo "Check that generator.nswag still names an output path and an input document." >&2
    exit 1
fi

echo "==> Done. Review the diff before committing:"
echo "    git diff -- $client"
