# 0043 — Validate once, where the rule lives

- **Status:** Accepted
- **Date:** 2026-08-06
- **Amends:** [0016](0016-let-a-rejected-command-fail-like-every-other-command.md)

## Context

ADR 0016 changed what a rejection *is* — a failed `Result` rather than an exception — and closed by
naming what it had deliberately not done:

> **The validators stay as they are.** Deleting the duplicated rules is still the better idea about
> where validation belongs, and it is still not this record's subject — this one is about what a
> rejection *is*. Keeping them means the CQRS host still rejects some addresses the layered host
> accepts; that is recorded below as a cost, not resolved.

This record resolves it. The cost is real and measurable:
`RuleFor(command => command.ContactEmail).EmailAddress()` sits in `CreateTrainerCommandValidator`
and `EditTrainerCommandValidator` and has no counterpart on the layered side, so the two hosts
refuse different addresses for the same request — against ADR 0008, which promises one surface.

ADR 0016 also recorded why `EditTrainerRequestHttp` deliberately carries no `[EmailAddress]`:

> .NET's `[EmailAddress]` and the domain's validator disagree, notably on a quoted local part
> containing an `@`, and an API that refuses what the domain accepts would be worse than one that
> asks later.

Measured on this repository's own dependencies, the disagreement runs the other way too:
`EmailAddressAttribute` accepts `a@b` and `user@localhost`; the domain's `EmailValidation` refuses
both. So `.EmailAddress()` one layer down does exactly what the contract refused to do, and does it
on only one of the two hosts.

**The asymmetry ADR 0016 did not see.** It showed the `NotEmpty()` rules were "already unreachable"
because `EditTrainerRequestHttp` carries `[Required]` and `[StringLength(50, MinimumLength = 2)]`.
That is true of the *edit* path. It is false of registration: `RegisterRequest` bounded nothing at
all, so `RuleFor(command => command.Firstname).NotEmpty()` was the only gate before the domain.
Deleting the duplicated rules without bounding the contract would have loosened the API — which is
why this record could not be written before ADR 0042 gave the auth request a contract to be bounded
in.

## Decision

**Validation happens once, in the layer that owns the rule.**

- **The contract declares shape and presence.** Required, length, count, and the format the
  boundary is willing to commit to. `RegisterRequestHttp` receives the annotations
  `EditTrainerRequestHttp` already carries, so the two write paths into `Trainer` are gated
  identically.
- **The domain judges meaning.** Whether an address is an address, whether a title is available,
  whether a catalogue has room. It answers with its own codes and its own messages, and it is the
  only layer allowed to.
- **The pipeline validator guards what neither can.** Exactly one thing today: an empty identifier
  that would reach `EntityId.Create` and throw, which is a 500 rather than a 400.
  `DeleteTrainingCommandValidator` and `TransferTrainingCommandValidator` keep their `NotEmpty()`
  on `Id`, `TrainingId` and `RecipientTrainerId` for that reason. Every other shape rule in a
  command validator is deleted.

**The boundary stays deliberately looser than the domain on one field, and that is the point.**
The contract still declines to judge the shape of an address, for the reason ADR 0016 gave. The
consequence is a window in which a request passes every pre-domain gate and the domain still
refuses it — and that window is not a defect, it is where the domain does its job. It is also the
only place a test can prove that a refused trainer takes its Identity account down with it (ADR
0040), which is a second reason not to close it: a contract that bounded everything would leave the
domain nothing to refuse, and the rollback nothing to demonstrate.

## Consequences

- **The two hosts agree.** A malformed address is refused by the domain on both, with
  `Trainer.InvalidEmail`, rather than by FluentValidation on one with `Validation`. ADR 0016's
  recorded cost is paid off; its statement that "the codes still differ" stops being true.
- **Registration gets stricter, and edition does not change.** `RegisterRequestHttp` now refuses a
  one-character firstname at model binding, as `PUT /Trainer/me` always did. That is the surface
  becoming consistent, not a new restriction invented here.
- **The rollback fact moves to the address.** ADR 0040's
  `Register_WhenTheTrainerHalfIsRefused_LeavesNoAccountBehind` used a one-character firstname, which
  this record's annotation now stops at the boundary. It is re-based on `a@b`: accepted by
  `EmailAddressAttribute`, therefore accepted by Identity — `RequireUniqueEmail` is on, so Identity
  validates the format with that very attribute — and refused by the domain two statements later.
  The window it needs is the one named above.
- **Four validators lose every rule and keep existing.** `CreateTrainerCommandValidator`,
  `EditTrainerCommandValidator` and `CreateTrainingCommandValidator` are left empty or nearly so.
  `EveryCommand_HasExactlyOneValidator` still requires them, and that stays: a validator declared
  and empty says "nothing here needs guarding at this layer", which is a statement; a missing one
  says nothing at all.
- **Query validators are untouched.** ADR 0016 decided they keep throwing, and they still guard the
  same empty identifier.

## Alternatives considered

**Copy `.EmailAddress()` onto the layered side.** The other way to make the hosts agree. Rejected
for the reason `EditTrainerRequestHttp` gives in its own remark: it would make both hosts refuse
addresses the domain accepts, which is a worse API on two hosts instead of one.

**Put `[EmailAddress]` on the contracts and delete the domain's check.** One judge, at the
boundary. Rejected: the shape of an address is a business rule here — `Trainer.InvalidEmail` is a
code a client branches on — and moving it to an annotation moves it out of the layer that owns it
and out of reach of the domain's tests.

**Delete the validators entirely.** With the shape rules gone, three of them hold nothing.
Rejected: `EveryCommand_HasExactlyOneValidator` is a structural rule about where a use case's parts
live, and an empty validator is a cheap, visible seam for the first rule that genuinely belongs
there.

**Leave it, as ADR 0016 did.** Defensible once. Twice is a convention, and the divergence has now
been recorded as a known cost through several merges without anybody paying it.

## Verification

`NoCommandValidator_JudgesShape` scans the command validators and was red first on **eleven** rules
— three more than the audit had counted by reading `RuleFor` lines rather than the chains they open:
the three in each trainer validator, two each in `CreateTrainingCommandValidator` and
`EditTrainingCommandValidator` restating `[StringLength(100, MinimumLength = 5)]` and
`[MinLength(1)]`, and `SetTrainerPhotoCommandValidator`'s `NotNull()` on `Content` — dead twice
over, since the contract requires the file and the property is a non-nullable `byte[]` with a
default. The identifier exception was proven by watching the rule stay green on
`TransferTrainingCommandValidator`, then fail when its `NotEmpty()` was moved onto a non-identifier
field. The behavioural half is `AuthTest` and the two application suites.
