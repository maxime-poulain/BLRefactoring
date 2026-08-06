# 0016 — Let a rejected command fail like every other command

- **Status:** Accepted — the validation cost it recorded and deferred is paid off by [0043](0043-validate-once-where-the-rule-lives.md)
- **Date:** 2026-08-02
- **Amends:** [0012](0012-finish-the-one-error-shape-and-name-its-members-apart.md)

## Context

ADR 0012 renamed the domain-code member to `domainErrors` so that it would stop colliding with the
field map `ValidationProblemDetails` publishes as `errors`. It closed by naming the thing it had
deliberately not done:

> **Drop the FluentValidation rules that duplicate a domain rule** — `EmailAddress()`, `NotEmpty()`
> on names and titles — so the domain judges on both hosts and the two answers converge without a
> rename. The better idea, and out of scope here: it changes which layer reports a failure and what
> it says, which is a decision about validation rather than about error format. Left to its own
> record.

This is that record, and the audit that prompted it found the divergence was worse than 0012
described. It is not only between the two hosts. It is inside the CQRS host, on a single endpoint.

**`PUT /Trainer/me` published two error vocabularies, chosen by which rule the caller broke.** A
malformed `ContactEmail` was caught by `EditTrainerCommandValidator`, which threw a
`ValidationException` that `ValidationExceptionHandler` answered as
`errors: {"ContactEmail": [...]}` — a field map, carrying no error code. A `Bio` of six hundred
characters passed that validator untouched, reached `TrainerProfileFactory`, and came back as
`domainErrors: [{errorCode: "Trainer.BioExceeds500Characters", …}]`. Same endpoint, same request
body, two shapes and two vocabularies, decided by which field was wrong.

**Most of the duplicated rules were already unreachable.** `EditTrainerRequestHttp` lives in
`Shared.Api` and is bound by both hosts. It carries `[Required]` and
`[StringLength(50, MinimumLength = 2)]` on `Firstname` and `Lastname`, and `[ApiController]` applies
them at model binding — before the pipeline runs. `RuleFor(c => c.Firstname).NotEmpty()` therefore
never fired on a request that had not already been rejected, and was weaker than the annotation that
preceded it.

**The one duplicated rule that did fire is one the contract deliberately refuses to make.**
`EditTrainerRequestHttp` records why it does not check the shape of an address:

> what is deliberately absent is any check on the shape of the address — .NET's `[EmailAddress]` and
> the domain's validator disagree, notably on a quoted local part containing an `@`, and an API that
> refuses what the domain accepts would be worse than one that asks later.

`RuleFor(c => c.ContactEmail).EmailAddress()` is that check, reintroduced one layer down. The CQRS
host rejects addresses the domain accepts and the layered host accepts.

## Decision

**A command's rejection travels as a failed `Result`, not as an exception.**
`ValidationPipelineBehavior` returns `Result.Failure(errors)` where it used to
`throw new ValidationException(failures)`. Nothing else about the behaviour changes: it still returns
before calling `next`, so the handler is not entered and no aggregate is touched.

A rejected command therefore leaves the API through `ProblemResultExtensions.Problem`, the single
place a business failure becomes a body, and is published under `domainErrors` like every other
failure of either host. The shape a caller reads no longer depends on which layer said no.

**The validators stay as they are.** Deleting the duplicated rules is still the better idea about
where validation belongs, and it is still not this record's subject — this one is about what a
rejection *is*. Keeping them means the CQRS host still rejects some addresses the layered host
accepts; that is recorded below as a cost, not resolved.

**The rejection carries `ErrorCodes.Validation`, a fourth kernel code.** It belongs to the kernel
rather than to an aggregate because it is raised before any aggregate is involved — that is the point
of validating there — so there is no owner to name, and ADR 0015's rule that a kernel code carries no
owner prefix is satisfied by construction.

One code, not one per rule. FluentValidation writes the field into its message
(`'Contact Email' is not a valid email address.`), which is what the field map used to carry;
inventing a code per rule would publish a second vocabulary for fields the domain already describes
in its own.

**Queries keep throwing.** A query answers with what it read — a DTO, a page — and has no failed
state to return, so `GetTrainerByIdQueryValidator` and its two siblings still raise a
`ValidationException` that `ValidationExceptionHandler` answers as a field map. They guard exactly
one thing: an empty identifier reaching `EntityId.Create`, whose constructor throws. Without them
that is a 500, which is what the layered host answers for `GET /Trainer/{Guid.Empty}` today.

## Consequences

- **`PUT /Trainer/me` with a malformed email answers the same shape on both hosts** — 400,
  `application/problem+json`, `domainErrors`. The codes still differ: `Validation` on the CQRS host,
  `Trainer.InvalidEmail` on the layered one, because the two layers judged it. The shape is the part
  a client deserialises; the code is the part it branches on, and a client that branches on
  `Trainer.InvalidEmail` still only gets it from one host.
- **Breaking for a CQRS client reading `errors` after a rejected command.** It moves to
  `domainErrors`, and gains an `errorCode` it did not have. `errors` still means the field map, now
  produced only by what the standard means by it: data annotations, Identity's rejections, and the
  query validators.
- **An exception stops being the normal outcome of a normal request.** Registration is the clearest
  case: a rejected `CreateTrainerCommand` used to unwind out of `CreateTrainerAsync` and roll the
  `TransactionScope` back by escaping it. It now returns a failure and rolls back by reaching the end
  of the scope without `Complete()` — the same outcome, arrived at deliberately.
- `ValidationExceptionHandler` survives with a narrower job. It is no longer on the path of any
  command.

Against that:

- **The CQRS host still refuses addresses the layered host accepts.** `EmailAddress()` is still
  there, and it still disagrees with `Email.Create`. What changes is that the refusal is now shaped
  and coded like every other failure, so it is legible rather than merely different. The rule itself
  is left for the record that decides where validation belongs.
- **Two ways to fail remain in one behaviour** — return for a command, throw for a query — and the
  reason is a real asymmetry in the read side rather than an oversight. Removing it means giving
  queries a `Result` to fail into, which changes every query handler and every call site on the read
  side.
- **`Validation` is coarser than the field map it replaces on the command path.** A client that wants
  to mark the offending input reads the message rather than a key. For the endpoints in question the
  data annotations still produce a real field map for everything they cover, which is most of it.

## Alternatives considered

**Drop the FluentValidation rules that duplicate a domain rule**, so the domain judges alone and the
two hosts converge on both shape *and* code. Still the better idea about where validation belongs,
and the one this record would have taken if it were only about correctness: it removes the
unreachable rules, the email disagreement, and a dependency in one move. Rejected here because it
deletes messages and rules rather than re-shaping an answer, which is a larger blast radius than the
divergence being fixed, and because the query validators would need a replacement guard against
`Guid.Empty` before they could go.

**Give queries a `Result<T>` so the behaviour has one exit.** The honest fix for the asymmetry above.
Rejected as out of proportion: it changes every query handler, every dispatcher call site and every
controller on the read side, to unify an exit that is taken only by three identifier guards.

**Invent an error code per validation rule** — `Validation.Email`, `Validation.Required` — so the
codes stay as precise as the field map. Rejected: it publishes a second vocabulary describing the
same fields the domain already describes in its own, and a client branching on `Validation.Email`
from one host and `Trainer.InvalidEmail` from the other is worse off than one reading the message.

**Keep throwing, and teach `ValidationExceptionHandler` to write `domainErrors`.** Same body, much
smaller diff. Rejected: it would answer a `ValidationProblemDetails`-shaped failure under the name
reserved for domain codes, re-creating under one member the collision ADR 0012 spent a rename
removing — and it leaves an exception as the normal outcome of a normal request.
