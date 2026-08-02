# 0004 — Publish every error as RFC 7807 Problem Details

- **Status:** Accepted — amended in part by [0012](0012-finish-the-one-error-shape-and-name-its-members-apart.md)
- **Date:** 2026-08-01

> **Amendment.** Three sentences below were true of the intent and not of the code, and ADR 0012
> makes them true rather than rewriting them here: the two `/Auth/*` endpoints never adopted the one
> shape; business failures answered `application/json` where this record's consequences claim
> `application/problem+json`; and the domain-code extension is now called `domainErrors`, because
> `errors` is what `ValidationProblemDetails` calls its field map and the two were colliding under
> one name. Read the paragraphs below as the decision they were, and 0012 as what it took to keep
> it.

## Context

The API answered failures in four different shapes, depending only on how far a request got before
something went wrong:

| Shape | Produced by |
|---|---|
| `[{ "errorMessage": …, "errorCode": { "name": …, "value": … } }]` — a bare array | business failures, at forty return sites across both hosts |
| `ValidationProblemDetails` (RFC 7807) | data annotations, produced by `[ApiController]` |
| `{ "errors": [{ "propertyName": …, "errorMessage": … }] }` | `FluentValidationMiddleware` |
| `"An unexpected error occurred…"` — a bare JSON **string** | `GlobalExceptionHandlerMiddleware` |

Nothing told a client which to expect, so a correct client had to parse all four. The fourth is the
worst of them: `WriteAsJsonAsync` applied to a `const string` serialises to a JSON string, quotes
included — not an object, no status, no field to read.

Two of the four were written by hand *next to* a framework that was already producing the standard
one on its own. And the layered host had no exception handling at all, so an unhandled exception
there left as whatever the host happened to produce.

## Decision

**Every error body is an RFC 7807 `ProblemDetails`.** One shape, whatever failed and wherever.

- **Business failures** go through `ProblemResultExtensions.Problem(statusCode, errors)`, the single
  place a failure becomes a body. The domain codes are not lost to the standard: the `errors`
  extension carries the same `ErrorResponseHttp` array as before, unchanged down to the nested code,
  so a client branching on `DuplicateTitle` keeps its logic and changes only where it reads it from.
- **FluentValidation failures** become a `ValidationProblemDetails` keyed by field name — the same
  body a data annotation failure already produced. Where a request failed no longer changes how a
  client reads why.
- **Anything unhandled** becomes a 500 `ProblemDetails` with a fixed sentence. The exception, the
  method and the path go to the log; none of them go to the caller.
- **Both hosts**, from `Shared.Api`, alongside CORS, identity and optimistic concurrency. The two
  hosts advertise the same REST API, and error handling had already drifted between them.

**The status codes do not move.** `DuplicateTitle` is still 409, `ConcurrencyConflict` still 412, a
missing `If-Match` still 428. Those are protocol decisions and they stay in the controllers; this
decision is only about the shape of what accompanies them.

**`IExceptionHandler`, not middleware.** The two hand-written middlewares had to be ordered relative
to each other — the validation one inside the global one, or a validation failure became a 500 — and
that ordering needed five lines of comment in the host to be safe. Handlers are tried in
registration order, so the ordering is the registration, expressed once in `AddApiProblemDetails`.

## Consequences

- A client parses one shape. `status` is always present, `errors` is present wherever domain codes
  exist.
- The generated OpenAPI document now says `ProblemDetails` on every `ProducesResponseType`
  attribute that previously advertised a bespoke array, so generated clients inherit the real
  contract.
- Two middlewares and their ordering comment are deleted; the layered host gains the exception
  handling it never had.
- Moving away from `ProblemDetails` later — or adding `traceId`, or a `type` URI per error code — is
  a change to one file.

Against that:

- **This is a breaking change to the published contract.** A business failure used to be a bare
  array at the root; it is now an object whose `errors` member holds that array. Existing clients
  must follow. The array itself was kept identical precisely to make that a one-line change rather
  than a rewrite.
- The `errors` extension is not part of RFC 7807. It is a legal extension member, and the
  alternative was to flatten domain codes into `detail` and lose them.
- `ProblemDetails` sets `Content-Type: application/problem+json`, which a client asserting
  `application/json` will notice.

## Alternatives considered

**Unify everything onto the existing `ErrorResponseHttp` array.** No breaking change for business
errors, which is a real advantage. Rejected because it means disagreeing with the framework inside
its own API: `[ApiController]` produces `ValidationProblemDetails` for a malformed body and
`ProblemDetails` for a bare `NotFound()` without being asked, so this option is not "keep one shape"
but "keep two and actively suppress the standard one".

**Keep the four shapes and document them.** The cheapest option and the reason the defect survived
this long. Documenting four shapes does not make a client's job smaller; it makes it explicit that
it will not get smaller.

**Unify the CQRS host only.** Half the work for less than half the benefit: it would leave the two
hosts differing on error format, which is the same defect one level up, on an API whose selling
point is that both stacks serve it identically.

**Make validation return a `Result` instead of throwing.** The philosophically consistent answer,
and the right one — but it changes the signature of every command handler and its tests, for a
question that is about error *mechanism* rather than error *format*. Deliberately left to its own
decision; this record only guarantees that whatever throws, the caller sees one shape.
