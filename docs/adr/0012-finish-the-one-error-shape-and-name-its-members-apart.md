# 0012 — Finish the one error shape, and name its members apart

- **Status:** Accepted
- **Date:** 2026-08-01
- **Amends:** [0004](0004-publish-every-error-as-rfc-7807-problem-details.md)

## Context

ADR 0004 opens with an absolute: *"Every error body is an RFC 7807 `ProblemDetails`. One shape,
whatever failed and wherever."* An audit of the whole solution against its own records found four
ways in which that was not true, and none of them were visible from the record.

**`POST /Auth/login` answered a bare JSON string.** `Unauthorized("Invalid username or password.")`
serialises to `"Invalid username or password."` — quotes included, not an object, no `status`, no
member to read. ADR 0004 lists that exact shape in its table of four and calls it *"the worst of
them"*. It then left it standing on the endpoint every client calls first.

**`POST /Auth/register` answered a bare `IdentityError` array**, at three return sites: the shape
the same record removed from forty other places. The generated client shows the cost — one
operation throwing `ApiException<List<IdentityError>>` while every other throws
`ApiException<ProblemDetails>`, and one page in the front end carrying a catch clause no other page
needs.

**Two different shapes arrived under one member name.** `ProblemResultExtensions` published domain
codes as `errors`; `ValidationProblemDetails` — which `[ApiController]` and
`ValidationExceptionHandler` both produce — publishes its field map under the same name. So
`PUT /Trainer/me` with a malformed email answered `errors: {"ContactEmail": [...]}` on the CQRS host,
where FluentValidation catches it, and `errors: [{errorCode, errorMessage}]` on the layered host,
where the value object does. A client that deserialised `errors` worked against one host and threw
against the other, and both suites asserted only the status code.

**Business failures answered `application/json`.** `ControllerBase.StatusCode(int, object)` produces
an `ObjectResult` with no content type, so negotiation lands on the JSON formatter's first media
type. Everything the framework produces — a bare `NotFound()`, model-state validation, both
`IExceptionHandler`s — answers `application/problem+json`. ADR 0004's consequences claim that is
what a client sees; for the failures this API writes itself, it was not.

A fifth, smaller: `404` was built two ways. Nine actions returned a bare `NotFound()`; six built a
problem document carrying a `NotFound` code.

## Decision

**The two `/Auth/*` endpoints join the shape.**

- Sign-in answers a `ProblemDetails` with the same sentence it always did. Both call sites — unknown
  username, wrong password — go through one method, so they cannot drift apart and start telling a
  caller which accounts exist. No `domainErrors`: a code naming which half failed would give away
  exactly what the shared sentence withholds.
- Registration answers a `ValidationProblemDetails` **keyed by the field at fault** —
  `Username`, `Email`, `Password`, `ConfirmPassword` — which is what a data annotation failure and a
  FluentValidation failure already answer. Identity's codes are names with no numeric value, so a
  field is the honest translation of `DuplicateEmail`; forcing them into `ErrorCode`, a closed smart
  enum with no member for `PasswordTooShort`, would mean inventing numbers to fill a column.
- Trainer creation fails on the domain's own rules, which do carry this API's codes, so it leaves
  through `ProblemResultExtensions` like every other business failure.

**The domain-code member is renamed `domainErrors`.** `errors` is the standard's name for the field
map, and the standard's meaning wins. This is the cheaper of the two available fixes and the more
honest one: the alternative — dropping the FluentValidation rules that duplicate a domain rule so
only one layer ever judges — is a better idea about *where validation belongs* and a worse one about
*what to do this week*, because it moves messages as well as shapes.

**Business failures state their media type.** `ObjectResult` with
`ContentTypes = { "application/problem+json" }` rather than `StatusCode(int, object)`.

**`404` is a bare `NotFound()`, everywhere.** The only code it carried was `NotFound`, which repeats
the status and gives a caller nothing to act on. Fifteen sites, one shape, and ADR 0004's promise
that *"`errors` is present wherever domain codes exist"* now reads as being about the failures that
have something to say.

## Consequences

- One error shape, without an exception to remember. `status` is always present; `errors` always
  means the field map; `domainErrors` always means this API's codes.
- **Breaking for callers of `/Auth/*`.** A client reading the 401 as a string, or the registration
  failure as an array, must follow. The generated client and the front end are updated in the same
  commit; CI regenerates the client (ADR 0008).
- **Breaking for anyone reading `errors` for domain codes.** One name to change, and now
  unambiguous.
- `ErrorFormatTest` moves into the test kit and runs on both hosts, which it never did — it covered
  the CQRS suite only while testing handlers that live in `Shared.Api`. It now also pins the media
  type, the two member names, and the two `/Auth/*` bodies.

Against that:

- **The API publishes two problem shapes, not one.** `ProblemDetails` and its subtype
  `ValidationProblemDetails`. That is RFC 7807 working as designed — the second is the first plus a
  member — and it is what the framework produces unprompted, but a client that wants the field map
  still has to know which endpoints can send one.
- **Identity's vocabulary is now flattened into field names.** `DuplicateEmail` and
  `InvalidEmail` both land under `Email`, distinguishable only by their message. A client that
  wanted to branch on the code cannot. No client does, and inventing a parallel code list to keep
  the distinction would be the tail wagging the dog.

## Known trade-off, recorded rather than fixed

`IDomainEvent` inherits `Mediator.INotification`, and `ICommand<T>` / `IQuery<T>` inherit
`IRequest<T>`, so `TrainingHub.Shared` — the kernel that `Shared.Domain` depends on — carries
`Mediator.Abstractions`. Every domain event in this solution therefore implements a third-party
messaging contract, which is the one place "the domain knows nothing about messaging" is not true.

It stays. Decoupling costs an internal `DomainEventNotification<T>` wrapper, a generic adapter
handler, and moving `Shared/CQS` out of the kernel — for a benefit that materialises only on the day
the library is replaced. What does not stay is the silence: the domain project's own csproj argues
at length that it removed `Microsoft.EntityFrameworkCore.Abstractions` for a single `[Owned]`
attribute, and a reader who notices this interface deserves to find the trade written down rather
than assume nobody looked.

## Alternatives considered

**Leave `/Auth/*` alone and amend ADR 0004 with a known-exceptions section.** Honest, cheap, and it
was the standing state — ADR 0011 already recorded the divergence rather than fixing it. Rejected on
the second reading: an absolute with two exceptions is not a contract, it is a convention plus a
list, and the endpoints that break it are the first two any client meets.

**Drop the FluentValidation rules that duplicate a domain rule** — `EmailAddress()`, `NotEmpty()` on
names and titles — so the domain judges on both hosts and the two answers converge without a rename.
The better idea, and out of scope here: it changes which layer reports a failure and what it says,
which is a decision about validation rather than about error format. Left to its own record.

**Give Identity's failures numeric codes in `ErrorCode`.** Would keep `domainErrors` as the single
error vocabulary. Rejected: `ErrorCode` is a closed set describing *this domain*, and
`PasswordRequiresNonAlphanumeric` is not part of it. A vocabulary that grows to cover a library's
error list stops being a vocabulary.

**Keep `errors` for domain codes and rename the field map.** Impossible without fighting the
framework: `ValidationProblemDetails` writes `errors`, and suppressing it means writing our own
subtype and giving up what `[ApiController]` produces for free — the exact mistake ADR 0004's first
alternative was rejected for.
