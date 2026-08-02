# 0006 — Describe the API with the framework's OpenAPI generator

- **Status:** Accepted — one paragraph superseded by [0008](0008-generate-the-http-client-from-a-script-and-verify-it-in-ci.md)
- **Date:** 2026-08-01

> **Superseded in part.** The paragraph below that keeps `NSwag.MSBuild` "deliberately, named so
> nobody mistakes it for an oversight" no longer describes the repository: ADR 0008 removed the
> package, and with it the commented-out target this record points at. The reasoning for keeping it
> *until client generation had its own decision* stands; that decision has since been taken.

## Context

The two hosts serve the same REST API and described it with two different libraries.

| | `src/DDD` | `src/DDDWithCqrs` |
|---|---|---|
| Generator | NSwag — `AddOpenApiDocument()` / `UseOpenApi()` | Swashbuckle — `AddSwaggerGen()` / `UseSwagger()` |
| Security scheme published | yes, configured by hand | **no** — a bare `AddSwaggerGen()` |
| Documentation packages referenced | NSwag.AspNetCore, NSwag.MSBuild, Scalar.AspNetCore, Swashbuckle.AspNetCore | Swashbuckle.AspNetCore |

Three separate defects sat behind that table.

**Two implementations for one contract.** NSwag and Swashbuckle do not describe the same
application identically — nullability, enum representation, `oneOf`, how a `ProblemDetails` is
rendered. So the published contract depended on which host was asked, on an API whose selling point
is that both stacks serve it identically. A client generated from one was not guaranteed to fit the
other.

**The CQRS host published no security scheme.** `AddSwaggerGen()` with no arguments produces a
document that never mentions bearer authentication, so its UI had no way to offer a token and no
authenticated endpoint could be tried from it. The layered host had that configuration; the CQRS one
had lost it, and nothing said so.

**Packages referenced and never configured.** `Scalar.AspNetCore` and `Swashbuckle.AspNetCore` were
both on `DDD.Api` while only NSwag was wired. The mixture there was more fragile than it looked:
`UseOpenApi()` is NSwag's, while `UseSwaggerUI()` — capital `UI` — is Swashbuckle's spelling, NSwag
v14 exposing `UseSwaggerUi`. On that reading the layered host's UI worked only because both
libraries default to serving at `/swagger/v1/swagger.json`.

## Decision

**`Microsoft.AspNetCore.OpenApi`, the framework's own generator, on both hosts, with Scalar as the
reference UI.**

Both are configured once in `Shared.Api/Extensions/OpenApiExtensions.cs`, next to CORS, identity,
optimistic concurrency and problem details — the things the two hosts must not disagree on.
`AddApiOpenApi()` registers the document; `UseApiOpenApi()` serves it and the UI, from the
Development branch only, exactly as before.

**The bearer scheme is declared by a document transformer**, so neither host can be the one that
forgets it. The transformer declares the scheme and stops there: which endpoints require a token is
already visible from `[Authorize]` and the framework reflects that into the document. What a reader
could not do was supply the token.

Scalar was already a dependency of `DDD.Api`, referenced and never used — this decision is partly
the completion of a path someone had started.

**`NSwag.MSBuild` stays**, deliberately. It serves client generation only, which is a separate
undecided question. Its MSBuild target is commented out in `DDD.Api.csproj` and invokes a
`nswag.json` that does not exist through `$(NSwagExe_Net10)`, a property that does not exist either.
Removing it here would pre-empt that decision; it is named so nobody mistakes it for an oversight.

## Consequences

- One document generator, so the two hosts describe the same API the same way, and a generated
  client fits both.
- Both documents now declare how the API is authenticated, and both UIs can exercise a protected
  endpoint.
- Two third-party documentation libraries leave the solution. What remains is the framework plus a
  UI.
- The document moves to `/openapi/v1.json` and the UI to Scalar's route, so anyone with `/swagger`
  bookmarked has to change it. Called out because a route is a small thing that wastes real time.

Against that:

- **The framework's generator is younger** than either library it replaces and has fewer knobs.
  Nothing in this API needed those knobs, but a future requirement might, and the honest position is
  that this is the bet — a smaller surface maintained by the platform against a larger one
  maintained elsewhere.
- Microsoft.OpenApi v2, which .NET 10 carries, changed types against v1. The transformer is written
  against v2 and was authored in an environment with no .NET SDK, so CI is the first thing to
  compile it.

## Alternatives considered

**NSwag on both hosts.** The most defensible alternative: the layered host already had the working
configuration, and NSwag also generates the clients, so producer and consumer would come from one
family. Rejected because an OpenAPI document is a standard artefact — NSwag's client generator
consumes one whatever produced it — so the supposed coherence buys nothing, and it keeps a
third-party dependency for work the framework now does.

**Swashbuckle on both hosts.** The most familiar option, and the CQRS host already used it.
Rejected because Swashbuckle was dropped from the ASP.NET Core project templates in .NET 9: choosing
it would be the one option of the three that moves against the platform, which in a repository whose
purpose is to show current practice is itself the message.

**Keep both and document the divergence.** Rejected on the same grounds as every other "document the
defect" option in this series: it does not make a client's job smaller, it only makes it explicit
that it will not get smaller.
