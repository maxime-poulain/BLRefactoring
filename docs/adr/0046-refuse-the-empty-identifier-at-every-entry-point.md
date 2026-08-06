# 0046 — Refuse the empty identifier at every entry point

- **Status:** Accepted
- **Amends:** [0043](0043-validate-once-where-the-rule-lives.md)
- **Date:** 2026-08-06

## Context

`GET /Training/00000000-0000-0000-0000-000000000000` answers **400** on the CQRS host and **500** on
the layered one. The gap has been recorded twice and paid off neither time.
`ValidationPipelineTests.EmptyIdentifier_OnAQuery_IsStillAnswered400` states it in a comment —
*"The layered host, which has no such validator, answers exactly that 500 today"* — and ADR 0043
declined to close it, because closing it means deciding **where the layered stack validates**, which
it named as a record of its own. This is that record.

The mechanism is not in dispute. `EntityId` refuses `Guid.Empty` by throwing, deliberately: an
identifier that cannot be empty is the guarantee the type exists to give. The CQRS host never
reaches the throw because a FluentValidation rule stops the request first; the layered host has no
such pipeline, so `TrainingApplicationService` calls `TrainingId.Create(id)` and the caller receives
a 500 for a request that is merely malformed. Against ADR 0004 and ADR 0012, which hold every
failure to Problem Details a client can act on, and against ADR 0008, which promises one surface
across two hosts.

**Where the rule was, and why it could not only stay there.** ADR 0043 kept exactly one shape rule
in the pipeline and gave its reason:

> The pipeline validator guards what neither can. Exactly one thing today: an empty identifier that
> would reach `EntityId.Create` and throw, which is a 500 rather than a 400.

True of the host that has a pipeline, and of nothing else. The layered host has no pipeline, and
giving it one would answer "where does the layered stack validate?" by copying a mechanism onto a
stack deliberately built without it. So the guard the pipeline holds cannot be the *only* guard: it
is unreachable from one of the two hosts. What follows is that the boundary needs one too — not that
the pipeline should lose the one it has.

**Two things measured while writing this.**

`TransferTrainingRequestHttp.RecipientTrainerId` carries `[Required]`, and its remark claims *"the
attribute only refuses a message with no recipient at all"*. It does not. A non-nullable `Guid`
always has a value, so `Guid.Empty` satisfies `[Required]` and travels on. What actually refused an
empty recipient was `TransferTrainingCommandValidator`, on one host of the two — the contract has
been announcing a guard it never had.

And of the seven FluentValidation rules left in the entire solution — every one a `NotEmpty()` on an
identifier — six guard a value that arrives **in the request**, from a route or a body. The seventh,
`GetTrainerByIdQueryValidator`, guards one that arrives from the **token**:
`currentUserService.TrainerId`, which appears in no contract a caller can see.

## Decision

**An identifier is refused empty where it arrives, and again where it is used.**

- **`NotEmptyIdentifier` does the refusing, because `[Required]` cannot.** A dedicated
  `ValidationAttribute` in `Shared.Api`, applied to every identifier a caller supplies — five route
  identifiers per host, and the transfer's recipient on the body contract. `[ApiController]` answers
  it at model binding with the `ValidationProblemDetails` this API already standardised on, naming
  the offending field. Identical on both hosts, because both carry the annotation and the body
  contract is shared.
- **It sits on the parameter, not on a route contract of its own, and that was measured.** The
  first shape built here gave each route identifier a `*RequestHttp` bound with `[FromRoute]`, on
  the model of `PaginationRequestHttp` — the more conventional answer, and the one this repository
  would otherwise reach for. It was abandoned on evidence: with a complex `[FromRoute]` model
  alongside a `[FromBody]` one, the framework's OpenAPI generator writes the route model's
  description onto the operation's **request body**. `PUT /Training/{trainingId}` published
  *"The training being addressed"* as the description of the edit body, and reordering the
  parameters did not move it. A published document that mislabels the payload is a worse defect
  than the one being fixed, so the annotation stays where it can be read and cannot lie.
- **The application layer keeps its own guard, and gains the ones it was missing.** Every command
  and every query refuses an empty identifier on its own properties, whatever dispatched it. The
  annotation closes the HTTP path; a message reaching `ICommandDispatcher` from an integration event
  consumer, a background service or a scheduler passes no controller, and the guard that would have
  caught it has to live on the message. Only controllers dispatch today — which is precisely why the
  absence would be invisible the day that changes.
- **This is not the duplication ADR 0043 removed.** That record deleted `.EmailAddress()` because it
  made **two hosts disagree about one request**: one rule, two answers, one caller. These two guards
  answer two different callers, and neither can stand in for the other — the contract cannot see a
  message that never crossed HTTP, and the validator cannot answer at model binding with a field
  name. "Validate once, where the rule lives" is honoured by asking where the rule lives: an
  identifier that must name something is a precondition of the *message*, not of the transport that
  carried it.
- **The layer does not assume the layer above it checked.** Stated plainly because it is the
  principle, not a consequence of it: the application layer is agnostic of the API layer, and an
  entry point it cannot see is one it must not depend on.

**Bounding the route is a boundary decision, not a domain one.** The domain still owns what an
identifier *means* — `EntityId` keeps refusing `Guid.Empty`, and keeps throwing when a caller inside
the solution hands it one, because that is a programming error rather than a bad request. What
changes is that the malformed request no longer gets that far. This is the same division ADR 0043
drew for the address: the contract declares shape, the domain judges meaning.

## Consequences

- **The layered host stops answering 500 on a malformed identifier**, and the two hosts answer the
  same document. That is the defect this record exists to remove.
- **The contract stops announcing a guard it did not have.** `RecipientTrainerId` is refused when
  empty, by the annotation its remark always claimed. A rule pins the trap that produced it: a
  non-nullable `Guid` never carries `[Required]` in a contract, because there it reads as a guard
  and is not one.
- **The published surface does not move.** Regenerating the client leaves exactly one line
  different: the `[Required(AllowEmptyStrings = true)]` that `RecipientTrainerId` no longer carries,
  because the annotation that refused nothing is gone. Every signature, every path and every schema
  is unchanged. That was read off the diff rather than assumed — and the abandoned variant above is
  what makes the point worth stating.
- **Three identifiers that were guarded by nothing are now guarded.** Writing the decision as a rule
  rather than as prose found them within a minute: `CreateTrainingCommand.TrainingId`,
  `CreateTrainerCommand.TrainerId` and `CreateTrainerCommand.UserId`. All three reach an
  `EntityId.Create` in their handler. The first two default to a fresh `Guid` but are `init`, so a
  caller can set them back to empty; `UserId` has no default at all. ADR 0043 emptied both those
  validators on the sentence *"this command carries none"*, which was simply false — and a merged
  record's body is never rewritten, so this record is where the correction lives.
- **No pipeline rejection is reachable over HTTP any more, and that is the point rather than a
  regression.** Once the contract refuses the empty identifier, every rule the validators hold
  guards a value this boundary cannot deliver: an identifier from a route or a body the annotation
  already refused, one from the token, or one the command mints itself. The validators exist for
  callers that never cross HTTP — and a caller that never crosses it cannot be simulated by
  crossing it. What they refuse is asserted per validator in the CQRS unit suite; what the pipeline
  behaviour returns is asserted in `ValidationPipelineBehaviorTests`.
- **`ValidationPipelineTests` loses the assertion it was written for.** It proved that a pipeline
  rejection leaves as a `domainErrors` document rather than a field map (ADR 0016), using
  `PUT /Training/{Guid.Empty}` — the one request the pipeline refused and the boundary did not.
  That request is now refused at model binding, so the assertion asserts the opposite of what it
  says. It is removed rather than adjusted: the shape it guarded is exercised by every domain
  failure this API answers, and pretending otherwise would leave a test whose name outlived its
  subject. What survives is the property that never depended on which layer answered — a refused
  write leaves the aggregate, and the caller's version, untouched.
- **`ValidationExceptionHandler` keeps every feeder it had in principle and none in practice.** ADR
  0016 decided query validators keep throwing rather than failing into a `Result`, and they still
  do. Over HTTP the contract now refuses first in every case, so nothing reaches the handler by that
  route; it stays registered because the rules it serves stay, for the callers named above.
- **`EntityId.Create` still throws, and the 500 still exists in the type.** It is now unreachable
  over HTTP, which is not the same as gone. Making the factory refuse with a `Result<T>` — as this
  repository's own convention for a value object's factory would have it, and as `EntityId`'s
  docstring implies by calling itself a value object — remains the deeper correction. It was
  weighed and declined here on cost: seventy-seven call sites, forty of them in domain tests, for a
  path this record already closes. It is a record of its own, and saying so is better than leaving
  the inconsistency unmentioned.

## Alternatives considered

**A route contract per identifier, carrying the annotation.** Weighed first and built first, for
the reasons in the Decision above; it lost to a measurement rather than to an argument, and the
measurement is recorded there so the next reader does not repeat it.

**Give the layered stack a validation pipeline of its own.** The symmetric answer, and the one ADR
0043 seemed to point at. Rejected: it copies a mechanism onto a stack deliberately built without
one, and it would state the same rule in two pipelines — on the repository whose last record on the
subject is titled *Validate once, where the rule lives*.

**A route constraint, `{trainingId:guid:notempty}`.** Cheapest of all, and wrong on the wire: a
route that does not match answers 404, which tells a caller the resource is missing when the request
is malformed. It would also silently change what the CQRS host answers today, which is a 400.

**A global action filter rejecting any empty `Guid` argument.** One place, no annotations to forget.
Rejected for the reason this repository gives elsewhere about calls hidden inside a branch: a guard
that no signature mentions is invisible where it applies, and it would keep the identifier out of
the OpenAPI document. The architecture rule gives the same completeness while the annotation stays
readable at the point it guards.

**Remove the validators' rules, leaving the contract as the only guard.** This is what the first
version of this record decided, on the argument that the second entry point does not exist:
`ICommandDispatcher` is called from controllers and nowhere else, so two guards answer one caller.
The argument is true about today and wrong about what it was being asked. It reasoned from the
current call graph rather than from where the rule belongs, and the errors are asymmetric — a guard
kept for a caller that never arrives costs one line with its reason attached, while a guard removed
before that caller arrives costs an exception nobody predicted, out of `EntityId.Create`, three
layers from the dispatch. Rejected on that asymmetry.

## Verification

`EveryIdentifierAnActionTakes_IsRefusedEmpty` scans both hosts' controllers by reflection and fails
on any action parameter typed `Guid` that does not carry the annotation — ten of them before this
record, five per host. It was watched red on all ten, and again after the change with one parameter
stripped back, which is the shape a future endpoint would arrive in.

`EveryIdentifierAMessageCarries_IsRefusedEmptyByItsValidator` asks FluentValidation's own descriptor
whether each `Guid` on a command or a query has a `NotEmpty` rule, so a rule folded over several
lines answers like one written inline. It was watched red on the three unguarded identifiers named
above — which is how they were found — and again afterwards with a restored rule deleted, the exact
regression it exists to catch, since that deletion is one this record's first version had already
made once.

`NoContract_MarksAGuidRequired` fails on a non-nullable `Guid` property carrying `[Required]` in the
shared contracts. It was watched red on `TransferTrainingRequestHttp` before the annotation was
replaced — the one occurrence that existed, and the reason the rule exists.

`EmptyIdentifierTest` in the shared TestKit — so both API suites run it — sends `Guid.Empty` on each
of the five identifier-bearing routes and asserts 400 with a `ValidationProblemDetails` naming the
field. It is the first of these facts the layered host has ever passed. `ValidationPipelineTests`
keeps its query fact and loses the comment describing the layered 500 as the state of the day.
