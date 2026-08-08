# 0056 — Announce the sanction, and let the index compose visibility

- **Status:** Accepted
- **Amends:** [0050](0050-retire-a-training-rather-than-delete-it.md)
- **Date:** 2026-08-08

**This record was `Proposed` until the commit that built it**, the treatment 0050, 0051, 0052 and
0055 all had, and for the same reason: writing the decision down before the code is the point, and
claiming the code already obeys it would be the lie this repository refuses everywhere else.

## Context

ADR 0050 gave the suspension a state, ADR 0052 gave the withholding one, ADR 0051 gave both an
authority and ADR 0054 gave them endpoints. Three of those states now have everything except a
consequence anyone outside this context can observe. Suspending a trainer writes one column, logs
one audit line, and changes nothing a trainer, a visitor or a search index would notice.

ADR 0050 said as much, and said why:

> Only three of the five become integration events. Publishing, unpublishing and deleting have a
> trigger and a consumer. Suspension has neither yet — no surface raises it, no context consumes it
> — and building outbox plumbing for a fact nobody produces is the anticipation this repository
> refuses.

Both halves of that excuse are gone. ADR 0051 built the surface — *"`Trainer.Suspend` and
`Reinstate` become reachable. Their domain events stop being facts that only a unit test ever
sees"* — and ADR 0053 named the consumer that was missing: a trainer who cannot be told why they
were sanctioned has been sanctioned in secret. So the record that declined to build the plumbing is
the record this one amends.

Two things had to be decided before any of it could be written, and neither is obvious.

**Who is told.** A trainer has two addresses and this repository had never had to tell them apart.
**What the index does.** ADR 0050 composes public visibility from two aggregates and stores it
nowhere, which is elegant on the write side and leaves the read side with a question: what does an
index do about a state that was never written to any of the rows it holds?

## Decision

**Three of the four administrative decisions leave the context as facts. Releasing still leaves
none.**

| Fact | Carries | Consumers |
|---|---|---|
| `TrainerSuspendedIntegrationEvent` | trainer, reason | the notice, the index |
| `TrainerReinstatedIntegrationEvent` | trainer | the notice, the index |
| `TrainingWithheldIntegrationEvent` | training, trainer, title, reason | the notice, the index |

ADR 0052's test is applied again rather than forgotten: **a release lands the training on
`Unpublished`, where it was not indexed and where nobody was waiting**, so it produces nothing.
That is the one thing in this change with no code to point at, which is why it is the one thing
asserted by watching a mailbox stay empty for the whole delivery budget.

- **A notice goes to the account's address, never to the published contact address.** They are
  different things and this is where the difference first matters. `Trainer.ContactEmail` is
  published on a profile and a trainer may legitimately point it at a secretariat, a shared inbox
  or a colleague; the account address belongs to the person who signs in. A sanction is addressed
  to that person. The two existing notices keep the contact address, and consistently so: the
  welcome message answers a registration that supplied it, and the change warning is *about* that
  address.
- **The recipient is resolved when the notice is sent, not when the decision commits**, through a
  new port, `ITrainerAccountQuery`. An address is not part of a sanction — it is where the sanction
  has to be delivered, and behind an outbox those are separated by an unbounded delay. It joins
  `ITrainingOwnerQuery` and `ITrainerIdentityQuery` in `Shared.Application/Queries/` rather than
  starting a naming family of its own: same shape, same folder, same reason — a handful of columns
  that used to cost a whole aggregate. Its adapter opens two stores because it is the only thing
  that knows both: the trainer is a row of `TrainingContext`, the address a column of
  `TrainingIdentityDbContext`, and neither has ever heard of the other.
- **A consumer reads through a port that cannot write.** `NoIntegrationEventHandler_Commits`
  refuses `IUnitOfWork` and `TrainingContext` and stops nothing about `ITrainerRepository`, whose
  surface is `Add`, `Update`, `Delete` and a handful of reads: handing that to something running
  after the commit hands back every write it was just refused, under another name. Rule 166 moves
  the line out by one interface.
- **The index learns a trainer's standing; it does not delete and rebuild.** The port grows a
  symmetric pair, `HideTrainerCatalogueAsync` and `ShowTrainerCatalogueAsync`, and the adapter
  decides how — a real engine flips a flag by query. The index composes public visibility exactly as
  the domain does, so a sanction is **one call about a trainer** rather than one per training, and a
  lifting costs one call rather than a rebuild. The verbs are neutral on purpose: an index knows
  what it shows, not why.
- **The withholding fact carries the training's title**, which is the exception to announcing
  identifiers only, made deliberately. The consumer has to name the training to the person it was
  taken from, and an owner with a dozen trainings told that "a training" was withheld has been told
  nothing. It is the title as it stood when the decision was taken, which is also the title the
  administrator saw.
- **A notice states the fact and the reason and stops.** Explaining how a decision was reached
  would publish a moderation policy to the person most motivated to work around it (ADR 0052), and
  claiming an appeal process there is none of would be worse than saying nothing.

## Consequences

- **What ADR 0050 refused to anticipate is now built, so its sentence changes.** *"Suspension has
  neither yet"* was true of a repository with no administrative surface. Its status line names this
  record, which is what ADR 0039 requires of an amendment and what stops a reader meeting an
  outbox chain the record beside it says does not exist.
- **The index is asked to hold something the write model does not.** Public visibility is composed
  and stored nowhere on the write side (ADR 0050) and the read side now stores that composition,
  which is what a read model is for. The cost is a third and fourth operation on a port that had
  two, and a fake adapter that logs four things instead of two.
- **A trainer's standing is now readable outside the aggregate** — by the index, from a fact. That
  is the only copy, it is a read model, and nothing composes a decision from it.
- **Three consumers depend on the identity store.** The notice cannot be composed from the message
  alone, unlike the eight that came before it, and a trainer whose account is gone gets no notice:
  the consumer logs and returns rather than throwing, because throwing is the protocol for *try
  again* (ADR 0034) and an absence will still be an absence on the next attempt (ADR 0033).
- **Fourteen post-commit consumers, eleven publishers, seventeen domain event handlers.** Every
  counted claim about them moves in the same commit, which is what ADR 0038 and ADR 0041 exist to
  force.
- **ADR 0053's email half is built here; its read-only half is not.** A suspended trainer is told,
  with the reason, and can still write everything they could write yesterday. That record stays
  `Proposed` and this one does not pretend otherwise — the notice is worded so that it claims
  nothing the code does not do.

## Alternatives considered

**Carry the address on the fact, like `TrainerCreatedIntegrationEvent` does.** No new port, and a
consumer that works from the message alone. It cannot answer the withholding case at all: a
`Training` knows its owner's identifier and has never known their address, so the publisher would
have to reach across aggregates during `SavingChanges` to fetch one. Two shapes for three notices,
where the second shape is needed anyway.

**Let the consumer take `ITrainerRepository`.** Zero new types, and a precedent in the same project.
It hands a post-commit consumer `Add`, `Update` and `Delete`, and the only thing standing between
that and a consumer writing is that nobody has done it yet. Rule 166 is the argument.

**Remove the entries on suspension and re-index them on reinstatement.** The obvious reading of
"the catalogue leaves public view", and it needs something the index no longer has: the list of the
trainer's published trainings, read back from the write model by a consumer, one call per training,
for a sanction that wrote to none of them. Hiding costs one call each way and keeps the read model's
shape identical before and after.

**Give releasing a fact too, for symmetry.** Four endpoints, four facts, a table that looks
finished. It would announce something to nobody, and the next reader would spend an afternoon
finding the consumer that does not exist. ADR 0052 decided this; repeating the decision here would
be the anticipation ADR 0050 refuses.

**Tell the trainer at the published contact address.** One less port, and the address the profile
already offers. It is the address a trainer gives out so that *strangers* can reach them, and it is
the one they are most likely to have pointed elsewhere. A sanction sent there can arrive at a
mailbox its subject never reads.

## Verification

- **`NoIntegrationEventConsumer_ReadsThroughARepository`**, watched failing first: the suspension
  notice was given `ITrainerRepository` and a line that used it, and the rule named both the class
  and the interface before the port went back.
- **The address, end to end, in `AdministrativeNoticeTest` so both hosts answer it.** Every trainer
  in that suite moves their published contact address somewhere else before the sanction lands, so
  an implementation reading `Trainer.ContactEmail` sends to a mailbox nobody is watching. The notice
  is read back out of a real mail server, addressed to the account.
- **The release notifying nobody**, by watching the mailbox for the same budget a delivered message
  is given and finding nothing. It is the only shape that distinguishes a message that will never
  come from one that is merely slow, and it is spent once, on the asymmetry it proves.
- **The two index reactions asked for exactly one call each**, and asserted not to have made the
  other: a suspension that removed trainings one by one, or a lifting that re-indexed them, would
  pass an assertion that only checked the catalogue ended up right.
- **The withholding notice naming its training**, with a title minted per run, so a message that
  merely mentions "a training" fails.
- **The closed set held closed**: the serializer's and the dispatcher's instance lists both grew to
  eleven, and both are checked against `IntegrationEventTypes.All` rather than counted by hand.
