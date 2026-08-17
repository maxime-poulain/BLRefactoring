# 0086 — Say Current when the caller is the criterion

- **Status:** Accepted
- **Amends:** [0081](0081-name-a-query-for-what-it-retrieves-and-what-scopes-it.md)
- **Date:** 2026-08-17

## Context

ADR 0081 gave the query half of the CQRS vocabulary its grammar — a retrieval verb, what is
retrieved, the criterion as `ByX` — and opened with a judgment about the other half: *"the commands
read well already, because a command has nowhere to hide: `TransferTrainingCommand`,
`SuspendTrainerCommand`, `RemoveTrainerPhotoCommand`."* A verb, the thing acted on, done.

One of those three examples was hiding something. `RemoveTrainerPhotoCommand` declares nothing at
all, and its handler resolves the trainer through `ICurrentUserService` — it removes the *calling*
trainer's photo, and nothing in the name says so. It is not alone:

| Message | Carries | Acts on |
|---|---|---|
| `EditTrainerCommand` | the profile fields, a version | the calling trainer |
| `SetTrainerPhotoCommand` | bytes, a media type | the calling trainer |
| `RemoveTrainerPhotoCommand` | nothing | the calling trainer |
| `EraseTrainerCommand` | nothing | the calling trainer, account and all (ADR 0085) |

Beside them sit messages that read identically and mean the opposite: `SuspendTrainerCommand` acts
on an *arbitrary* trainer, named by the `TrainerId` it carries. So the same name shape —
verb-Trainer-Command — covers both "whoever is calling" and "whoever is named", and a reader has to
open the file to learn which. That is precisely the defect ADR 0081 fixed for queries, arrived at
from the other side: there the criterion was carried and unnamed, here the criterion is the caller
and unnamed. ADR 0081 already settled what to call this case when it named
`GetTrainingsByCurrentTrainerQuery` — *"the criterion is named even when the message does not
carry it"*, and it is `CurrentTrainer` rather than `CurrentUser` because `ICurrentUserService`
distinguishes the account from the trainer deliberately.

The distinction matters most on the newest of the four. `EraseTrainerCommand` destroys the calling
trainer irreversibly; a future reader who takes it for an administrative command acting on a
carried identifier — its neighbors `SuspendTrainerCommand` and `ReinstateTrainerCommand` read
exactly that way — has misread the blast radius of the most destructive message in the system.

## Decision

**A message whose criterion is its caller says `Current`.** Precisely: a command or query whose
handler resolves the acting trainer through `ICurrentUserService`, and which carries no identifier
of its own, names the caller as its scope — `Current` standing before the noun it scopes:

- `EraseCurrentTrainerCommand`
- `EditCurrentTrainerCommand`
- `SetCurrentTrainerPhotoCommand`
- `RemoveCurrentTrainerPhotoCommand`

**And a message that carries an explicit identifier never says it.** `GetTrainerByIdQuery` acts on
whoever it is given and only its callers choose the current trainer; `SuspendTrainerCommand`
carries its target; `CreateTrainingCommand` carries the identifier of what it creates. Adding
`Current` to any of them would move the claim from the message to one of its call sites — the
name must describe the message, not its most frequent caller.

The two conventions are one grammar in two spellings. A query names its caller-criterion the way
ADR 0081 spells every criterion, as `ByCurrentTrainer`; a command has no `By` clause, so `Current`
qualifies the noun directly. Both say the same thing: the one value this message acts on that no
call site can supply — and therefore no call site can get wrong — is the caller.

**`ICurrentUserService` is the abstraction, and remains the only one.** The convention names what
the existing seam already does; it introduces no new notion of caller, no new interface, and no
change to how any handler resolves anybody. This is a rename, bounded to the four types above and
their handlers and validators — behavior, authorization and routing are untouched by construction.

## Consequences

- **Four messages are renamed**, each with its handler and validator
  (`EveryMessage_HasAHandlerNamedAfterIt` makes the handler move with the message rather than a
  separate decision). Use-case folders keep their names: `Erase/`, `Edit/`, `SetPhoto/`,
  `RemovePhoto/` name the verb and carry no criterion, the same grammar that leaves
  `GetByCurrentTrainer/` carrying its own.
- **Nothing on the HTTP surface moves.** The published operations stay `Trainer_EditCurrent`,
  `Trainer_SetPhoto`, `Trainer_DeletePhoto` and `Auth_EraseAccount`; no route, schema or line of
  `Clients.Generated.cs` changes, so the regeneration is run and the empty diff is the proof
  (ADR 0008).
- **The layered stack is untouched.** Its use cases are methods read with their receiver —
  `trainerApplicationService.EraseAsync()` — and ADR 0081 already drew that line: a method is read
  with its receiver, a message is read alone.
- **`CLAUDE.md` gains the convention**, beside ADR 0081's, so a future session writes
  `Current` into the next caller-scoped message without reading this record first.
- **Records merged before this one keep the names they were written with.** ADR 0021 and its
  contemporaries name `SetTrainerPhotoCommand` as it was called on the day they were accepted; a
  merged record is never rewritten, so the rule that defends this decision reads code and never
  `docs/adr/`. ADR 0085, which ships in the same pull request as this record and is not yet
  merged, is updated to the new name rather than left contradicting the tree it describes.

## Alternatives considered

**`EraseCurrentAccountCommand`, keeping the word the erasure's own record uses.** ADR 0085 is
titled "Let the account erase itself" and the account is the door the caller knocks on — but the
command does not touch the account. It stages the Trainer aggregate's deletion; the Identity half
belongs to the shared endpoint around it. A name should describe what the type does, and this
layer's vocabulary is the trainer's: `EditTrainerCommand`, `ICurrentUserService.TrainerId`.
Rejected.

**Rename the layered service methods to match** — `EraseAsync` to `EraseCurrentAsync` and so on.
Rejected for the reason ADR 0081 left `GetByIdAsync` alone: the receiver says the first half, and
`ITrainerApplicationService` documents its own scoping. The two stacks still name one use case
compatibly — the README's use-case table pairs them row by row.

**Say `Current` at the HTTP boundary too** — `Auth_EraseCurrentAccount`. The operation identifier
is a published contract (ADR 0008); renaming it moves the generated client, the BFF and every
anchored rule for a distinction the boundary already draws with `/Trainer/me` routes and bearer
authentication. The boundary names the resource; the message names the use case. Rejected.

**A convention without a rule.** Rejected the way ADR 0081 rejected it: a convention kept by
reading decays at exactly the commits that are hardest to review — and this one's population is
defined sharply enough to check mechanically, which is rare for a semantic claim and too cheap to
decline.

## Verification

`EveryMessageActingForItsCaller_SaysCurrent` takes every CQRS message by reflection, finds its
handler through the closed `ICommandHandler<,>`/`IQueryHandler<,>` interface, and asserts both
directions:

- a message whose handler's constructor takes `ICurrentUserService` and which declares no `Guid`
  property has `Current` in its name — the caller is its criterion, and the name says so;
- a message with `Current` in its name has such a handler — nobody borrows the word for a message
  whose scope a call site supplies.

Born red against exactly the four commands above — with `GetTrainingsByCurrentTrainerQuery`
already green, `SuspendTrainerCommand` excluded by the identifier it carries, and
`CreateTrainingCommand` excluded by the identifier of what it creates — and green once they were
renamed. The rename itself is behavior-preserving, so the proof that nothing but names moved is
that every other suite passes unchanged apart from its own renames.
