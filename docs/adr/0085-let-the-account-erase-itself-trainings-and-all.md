# 0085 — Let the account erase itself, trainings and all

- **Status:** Accepted
- **Amends:** [0053](0053-a-suspended-trainer-reads-and-does-not-write.md)
- **Date:** 2026-08-16

**What this changes in 0053.** That record removed every write from a suspended trainer, on the
argument that repairing means something only if repairing leads somewhere. Erasure is the one
write this record gives back, because it is not a repair and does not lead anywhere: it is the
person leaving, and the right to leave belongs to the account rather than to the standing the
administration gave it. Every other write 0053 refuses stays refused.

## Context

The event-storming board has carried one hotspot since the administration arrived: *nothing
removes a trainer*. The rule itself has existed the whole time — `Trainer.MarkForDeletion`
raises `TrainerDeletedDomainEvent`, a policy deletes the departing trainer's trainings inside
the same unit of work, and `ITrainerRepository.Delete` sits implemented with no caller — but no
use case reaches it, and the two documents that mention the gap disagree about who should. The
aggregate's own remark, written before the administration existed, says removal is an
administrative decision waiting for a role. The strategic design, rewritten when that role
arrived and got its four commands, says the opposite: erasing a trainer is a right the account
holds, not a sanction the administration applies, and removal was deliberately not among the
administration's commands. This record settles the disagreement on the strategic design's side
and closes the hotspot.

The door it opens through was built by the two records before it. Registration is the system's
only cross-context write — one ambient `TransactionScope` around the Identity account and the
`Trainer`, on the shared `AuthControllerBase`, with each host supplying the trainer half through
one abstract method (ADR 0040). Recovery put account capabilities beside it and set the posture
for proving who is asking (ADR 0084). Erasure is registration's mirror, and it walks through the
same door.

## Decision

- **Erasure is the account's act, on the account's surface.** One authenticated action,
  `POST /Auth/erase-account`, on `AuthControllerBase` — published identically by both hosts,
  like registration, login and recovery. It is guarded by the trainer claim, not by the active
  standing: **a suspended trainer may erase their account**, which is the one write this record
  hands back to them. A sanction is about what a trainer may publish, not about whether the
  person may stop being one — and a platform that held a person's data hostage to its own
  sanction would have the relationship backwards. The administration keeps what it had:
  sanctions, not removal. An administrator's own account carries no trainer claim and cannot use
  this door; it was provisioned by hand and leaves by hand (ADR 0051).

- **The caller proves intent with their password.** The request carries the account's current
  password, checked before anything else (`CheckPasswordAsync`). A session is not enough: an
  access token lives for up to sixty minutes after a device is left unlocked or a token is
  stolen, and erasure is the one action in this API that cannot be undone by anyone. A wrong
  password answers a field-keyed `400` — the caller is authenticated as the account's holder, so
  there is nothing to enumerate and nothing to hide.

- **Erasure is immediate and final.** No soft delete, no grace period, no tombstone. The house
  already argued this shape for the reset credential — a deleted row is a stronger statement
  than a flag, because there is no flagged row to mishandle — and the deletion half of the
  lifecycle record exists precisely for "the trainer exercising a right to have their data
  removed, which a system that only ever hides things cannot honor" (ADR 0050's own words on
  `Training.MarkForDeletion`). Withheld trainings die with the account: the administration's
  hold governs what may be *offered*, not whether the person's data may *exist*, and a
  withholding that survived its owner would be a lock on an empty room.

- **One transaction, registration's mirror.** The action opens the same ambient
  `TransactionScope` registration opens, and inside it: the host-supplied
  `EraseTrainerAsync` stages the trainer — `MarkForDeletion`, then the repository delete — and
  saves; the cascade removes the trainings in that same save, announcing each one; then
  `userManager.DeleteAsync` removes the account, and the reset credential's row follows it by
  foreign key (ADR 0084). Either everything is gone or nothing is. The two contexts share one
  connection string, which is what keeps the transaction local (ADR 0040) — this becomes the
  second place both write sides commit together, and the context map stops calling registration
  the only one.

- **Two facts, because two contexts lose their rows.** Each fact carries what its consumers
  will no longer be able to ask, resolved on its own side of the seam — and neither side ever
  reads across it mid-transaction, which is what keeps the ambient scope on one connection at a
  time (ADR 0040's condition, held rather than tested by luck).
  `TrainerDeletedDomainEvent` gains what its policies need — the portrait's identity, which the
  aggregate knows at the moment it marks itself — and gains what the other trainer facts have: a
  policy translating it into `TrainerDeletedIntegrationEvent`, the trainer's identifier and
  photo, staged in the erasing save. The account's side is
  `AccountErasedIntegrationEvent` — the address and the username — committed by the endpoint
  itself, which is holding the `IdentityUser` it just checked a password against:
  `PasswordChanged`'s exact mold, making erasure the third flow whose endpoint commits its own
  fact (ADR 0082, ADR 0084). Unlike the sanction notices — which resolve the address at
  delivery, because the row will still be there (ADR 0056) — this fact **carries** it, because
  gone is the only state the fact is ever true in. The house precedent is the warning sent to a
  contact address the aggregate has already forgotten: when the fact outlives every row it
  speaks about, the fact is the record.

- **Two consumers finish what the transaction cannot.** `SendErasureNotice` mails the erased
  account's address a farewell — the confirmation for the owner, and the alarm bell for an owner
  whose session was stolen, for whom sixty minutes is the most the intruder's token survives.
  `RemoveErasedPortrait` deletes the portrait's bytes from the object store. The bytes cannot
  die inside the transaction — a scope that rolls back cannot put them back, which is ADR 0021's
  whole ordering — and they should not die on the request thread after it, where a crash between
  commit and cleanup orphans them silently. In the outbox they are removed at-least-once, with
  retries, and both consumers are idempotent under redelivery: a duplicate farewell is an email,
  and deleting absent bytes is a no-op. The photo store's delete learns to take the photo's
  identity rather than the whole value object — the shape its own read already argues for, now
  that a deleter, like a reader, may have no aggregate at all. The search index needs no
  trainer-level call — the cascade announces every training's deletion individually, and the
  index consumers it already has remove each entry (ADR 0050).

- **The browser's door is the BFF's, and it closes the session behind it.**
  `POST /bff/erase-account` is the first authenticated call the BFF makes to the API on a
  visitor's behalf outside the proxy: it reads the access token from the session cookie,
  forwards the password to `/Auth/erase-account`, and — only on success — signs the cookie out
  before answering, so the browser leaves the flow as anonymous as the account now is. The
  screen is a guarded section of the profile page: a consequence spelled out, the password asked
  for in place, and the catalog as the landing — the one surface an ex-trainer still has.

## Consequences

- **What survives an erasure is named, not hidden.** The audit journal keeps the administrative
  history it recorded — a sanction's record belongs to the administration that made it, and
  erasing the subject does not erase the decision. Outbox payloads carrying the trainer's facts
  survive until the retention sweep, fourteen days after delivery (ADR 0033), the farewell fact
  among them — address included, which is the price of a fact that must outlive its rows. Log
  lines age out with their files (ADR 0026).

- **Outstanding sessions survive for at most the access token's remaining lifetime.** The same
  sixty-minute residual as a password reset, with the same compensating control (ADR 0084). A
  deleted account's token still validates cryptographically; what it can reach answers `403` or
  `404`, because every trainer read resolves a trainer that no longer exists. The BFF session
  that performed the erasure is closed by the flow itself.

- **The freed username and address can register again, immediately.** Identity's uniqueness
  rules judge rows, and the rows are gone. That is the honest meaning of erasure — the platform
  keeps no shadow copy to refuse a returning person with — and the returning person is a new
  trainer with an empty catalog, not a restoration.

- The eventual half is visibly eventual: the catalog stops serving the erased trainings when the
  delivery worker reaches their facts, seconds after the commit, exactly as every other removal
  already behaves (ADR 0059).

## Alternatives considered

- **The administration's door** — removal as a fifth administrative command. Rejected by the
  strategic design's own sentence: a sanction is applied to a person, erasure is performed by
  one, and giving the administration the power to erase a person's account confuses moderation
  with ownership. The aggregate remark that pointed this way predates the administration and is
  corrected by this record.

- **Refusing a suspended trainer.** The consistent reading of 0053, and rejected with a
  narrowing amendment instead: 0053's writes are catalog writes, refused because repair leads
  nowhere without a review loop. Leaving is not repairing. A suspension that blocked erasure
  would turn a moderation state into custody of a person's data, and the audit journal already
  preserves what the administration needs of the history.

- **A soft delete, a grace period, an export-then-erase.** Each keeps rows so the erasure can be
  undone or delayed, and each therefore keeps every risk the rows carry, for a benefit nobody
  asked for. A person who erases in error re-registers in a minute; a platform that "erases"
  reversibly has renamed hiding.

- **Deleting the portrait bytes inline, after the commit** — the photo-replacement pattern.
  Right for a replacement, where the displaced object is an orphan nobody references and the
  next replacement writes a fresh key anyway; wrong here, where the cleanup is part of a promise
  of erasure and an unluckily-timed crash would break the promise silently. The outbox retries
  what an inline call would lose, and the consumer's idempotency makes the retry safe.

- **Resolving the farewell's address at delivery**, the sanction notices' shape. Impossible by
  construction: `ITrainerAccountQuery` answers `null` for a trainer who is gone, and gone is the
  only state this fact is ever true in. The fact carries the address or nobody is told.

- **One fact instead of two**, a `TrainerDeletedIntegrationEvent` carrying the address and the
  photo together. Rejected on where the address would have to come from: the fact would be
  staged by a policy running inside the trainer's own save, and the address lives in the other
  context's store — a cross-store read at exactly the moment the training connection is busy,
  which is the two-open-connections shape that promotes the ambient transaction to a
  coordinator this platform does not have. Two facts keep each context's data on its own side
  of the seam and the scope on one connection at a time.

- **A `TrainerErasure` aggregate, or a command in each stack's application layer for the
  account half.** Rejected for ADR 0084's reason: the account is the framework's, the domain
  holds no authentication vocabulary, and the cross-context orchestration already has a home —
  the shared controller both hosts inherit. The trainer half *is* in each stack, as the erase
  use case each application layer owns, because deleting the aggregate is domain work; the
  account half is not.

## Verification

- `TransactionRules` gains the erasure's half of what it holds for registration: the erasing
  member opens the ambient scope, completes it, and deletes the account inside it
  (`TheErasure_RunsInOneAmbientTransaction`), and demands the caller's own password before any
  of it (`TheErasure_DemandsTheCallersOwnPassword`).
- `IntegrationEventRules`' census extends to the two new facts, and
  `TheFarewell_CarriesItsAddress` pins the property this record's whole notice depends on: the
  account's fact declares the address it will be delivered to.
- `AccountErasureTest` in the shared TestKit proves the flow whole on both hosts, against real
  SQL Server and real SMTP: the round trip and the dead session, the wrong password that erases
  nothing, the catalog withdrawal, the farewell at the account's address, the freed identity
  registering again, the suspended trainer allowed to leave, and the portrait bytes gone.
- The cascade itself was already proven — `DeleteTrainingWhenTrainerDeletedDomainEventHandler` and the
  lifecycle rules predate this record — which is the measure of how much of this feature was
  built before it: what this record adds is the door, and the argument about whose it is.
