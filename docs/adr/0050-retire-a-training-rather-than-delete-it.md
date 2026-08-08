# 0050 — Retire a training rather than delete it

- **Status:** Accepted — amended by [0056](0056-announce-the-sanction-and-let-the-index-compose-visibility.md): the suspension has a surface and consumers now, so it leaves the context as a fact, and the index composes a trainer's standing rather than forgetting their catalogue; amended by [0053](0053-a-suspended-trainer-reads-and-does-not-write.md): a suspended trainer loses every write, editing and unpublishing included
- **Date:** 2026-08-07

**This record was `Proposed` for exactly one commit.** It was written before any code answered to
it, which is what that status means here, and it became `Accepted` in the commit that built it —
together with `EveryStatusTransition_AnnouncesItself`, the rule the Verification section below
promised. The two paragraphs that explained the wait are gone with the wait; what they said is
recorded in ADR 0039 and ADR 0040, which own the question of what a status means.

## Context

A trainer publishes trainings and may remove one. Removing is a `DELETE`, and five facts about the
current model — each read off the code rather than assumed — say that arrangement has stopped
being tenable.

| Fact | Where it is visible |
|---|---|
| Deleting a training raises **no domain event at all** | `Training.cs` — no `AddDomainEvent` on that path |
| So nothing ever removes it from the search index | already recorded as a hotspot in `event-storming.md`: *"a real one would serve a training that no longer exists"* |
| `ITrainingSearchIndexer` declares **only** `IndexAsync` | a retrait is not expressible through the port |
| `CountForTrainerAsync` counts every row a trainer owns | `TrainingRepository.cs` |
| Every read is already scoped to its owner | `GetByIdAsync` answers 404 for somebody else's training |

The first three compose into a defect that is harmless only while the index is a fake: a training
created, indexed, then deleted stays in the index for ever, and `Catalogue Discovery` — announced,
and the reason the indexer exists — would serve it.

**The occasion is the ban.** Suspending a trainer must not destroy their catalogue, and the current
cascade does exactly that: `DeleteTrainingWhenTrainerDeletedEventHandler` removes every training the
departing trainer owned. A sanction that is indistinguishable from an erasure cannot be lifted.

So the question is not *should a row survive a delete* — that is a storage question. It is *does a
training have a life beyond existing*, and the answer decides whether what follows is a lifecycle or
a soft delete wearing an enum.

## Decision

**A training is `Published` or `Unpublished`. A trainer is `Active` or `Suspended`. Public
visibility is composed from the two and stored nowhere.**

```
Training :  Published ⇄ Unpublished          born Published
Trainer  :  Active    ⇄ Suspended

publicly visible  ⟺  training is Published  AND  trainer is Active
```

- **Two states, not three.** There is no `Draft`, because there is no drafting: `POST /Training`
  takes five required fields and produces a complete training in one call. A state nothing can
  remain in is not a state. The word is also taken — `Training.CreateDraft` already means "an empty
  instance before the edition is applied", and one word cannot mean two things in one file.
- **`Unpublished`, not `Disabled` or `Archived`.** The verb is already in the ubiquitous language:
  *"a trainer keeps a professional profile and **publishes** the trainings they teach"*. The inverse
  of `publish` is `unpublish`. `Disabled` is a word about systems, `Archived` promises a retention
  policy that does not exist, `Retired` implies the door is shut.
- **Suspending a trainer changes nothing on their trainings.** One field, one aggregate, no cascade.
  Their catalogue disappears from public view because its owner did, and reappears when the
  suspension is lifted — with no record of *which* trainings the sanction touched, because it
  touched none.
- **A suspended trainer may not increase their public footprint.** Refused: create, publish,
  transfer — giving and receiving alike. Permitted: edit, unpublish. One sentence to remember, and
  it leaves the trainer able to repair what earned them the sanction.
- **Deleting survives, and changes role.** `UnpublishTraining` becomes the everyday act;
  `DeleteTraining` remains for the training created by mistake, and for erasure — a trainer has a
  right to have their data removed, and a system that never deletes anything cannot honour it. The
  deletion cascade is untouched.
- **An unpublished training does not consume the quota; it does keep its title.** Only published
  trainings count toward `MaximumPerTrainer`, so withdrawing ten does not end a trainer's catalogue
  for ever. The title stays taken — but by something its owner can see in their own listing and can
  republish, rename or delete, so the refusal names something actionable rather than something
  invisible.

## Consequences

- **Five business facts appear**: a training published, unpublished, deleted; a trainer suspended,
  reinstated. The third closes the hotspot the boards have carried since they were written.
- **The indexing port grows a second operation.** `RemoveAsync` is what makes an unpublished or
  deleted training leave the index; without it this record changes nothing a visitor could observe.
- **Only three of the five become integration events.** Publishing, unpublishing and deleting have a
  trigger and a consumer. Suspension has neither yet — no surface raises it, no context consumes it —
  and building outbox plumbing for a fact nobody produces is the anticipation this repository
  refuses. The domain events exist and are tested on the aggregate, exactly as
  `Trainer.MarkForDeletion` is today.
- **`CountForTrainerAsync` stops meaning what it says.** It counts rows; it must count published
  ones. That is the single change with the widest blast radius, and the reason this record exists
  rather than a commit message.
- **The suspension is modelled and unreachable.** No role grants it, so `Suspend` and `Reinstate`
  will be domain methods no endpoint calls — the treatment the strategic design already documents
  for deleting a trainer, and states as deliberate: *"the rule about deleting a trainer is modelled
  and tested, while the permission to trigger it is deliberately absent"*.
- **The observable gain is small on the day it ships.** Without a public site and without an
  administration surface, a reader sees a trainer withdrawing a training instead of destroying it.
  The gain is that `Catalogue Discovery` becomes buildable against a model that can express what it
  must not show.

## Alternatives considered

**Keep deleting, and fix the index another way.** The narrowest repair: add a `TrainingDeleted` fact
and a `RemoveAsync`, and change nothing else. It fixes the recorded hotspot and leaves the ban
unrepresentable — a suspension would still have to destroy a catalogue to hide it. Rejected because
the ban is the occasion, not a side quest.

**An `IsDeleted` flag.** One boolean, filtered in the repository. Cheapest by a wide margin, and it
is the shape this record exists to refuse: nothing is ever un-deleted, so the flag has one direction,
raises no fact anybody reacts to, and changes what is *shown* without changing what is *allowed*.
A lifecycle has states a business act moves between; this has a tombstone.

**`Draft` / `Active` / `Disabled`, the shape first proposed.** Rejected on both halves. `Draft` has
no behaviour, since creation is atomic and complete. And `Disabled` merges two situations the ban
itself pulls apart: a trainer withdraws T1, is then suspended, which hides T2 and T3 — and when the
suspension is lifted, T2 and T3 must return while T1 must not. One state cannot answer that. It was
not a hypothetical objection; it was the primary scenario failing on its own terms.

**One state plus a reason.** `Unpublished(reason: Owner | Moderation)`. Keeps a single state and
records why. Rejected because the two differ in what is *permitted*, not merely in provenance — an
owner may republish what they withdrew and must not republish what was taken from them — and a rule
about what is permitted belongs in a state, not in a field beside one. It is also the first step
toward a moderation record, which is a boundary this repository has deliberately not drawn.

**Cascade the suspension onto each training.** The shape first proposed: banning sets every training
to `Unpublished`. Rejected for three reasons that compound. It writes N rows to record one fact. It
loses the information needed to lift the sanction — nothing distinguishes a training the ban hid
from one the trainer had withdrawn. And it duplicates onto the training a fact that already lives on
the trainer, so the two can disagree. Derived visibility has none of those costs and needs no
handler on the write side at all.

## Verification

- **`LifecycleRules.EveryDeletion_AnnouncesItself`** — added after the fact, and the record is
  honest about why. The rule below watches members that *move* a status; a deletion moves none, it
  removes the row the status was written on. `DeleteTrainingWhenTrainerDeletedEventHandler` fell
  straight through that gap: it deleted every training a departing trainer owned and announced not
  one of them, so the search index went on serving all of them — the very defect this record was
  written to close, surviving on the bulk path. Two halves, two rules. Watched red on the cascade,
  then red again on a path that was already correct, to prove it is not aimed at one file.
- **`LifecycleRules.EveryStatusTransition_AnnouncesItself`** — the rule this record turns on. It
  reads the domain's source, splits each file into members, and refuses any member that moves a
  `Status` without calling `AddDomainEvent`. Watched failing first: deleting the
  `TrainingUnpublishedDomainEvent` line from `Training.Unpublish` names the member and the file.
  It deliberately does not check that the fact *matches* the state — that is the unit tests' job —
  because what a rule can hold and a test would not miss is the transition nobody wrote a test for
  at all.
- **The transitions and the refused transitions**, on both aggregates:
  `TrainingLifecycleTests` and `TrainerStandingTests`. Each state is reached, each no-op transition
  is refused by name, and each aggregate is walked in both directions — the assertion that this is
  a lifecycle and not a tombstone.
- **The three invariants this record changes.** The quota and the title are asserted in the domain
  *and* end to end: `Unpublish_AtTheCatalogueLimit_FreesAPlace` withdraws one of ten, creates an
  eleventh, and then watches the withdrawn one refused on the way back — the hole a quota on
  published trainings would otherwise leave open. What a suspended trainer may do is asserted on
  `CreateAsync`, `PublishAsync` and both sides of the transfer; that they may still *unpublish* is
  asserted on the signature rather than on a call, because a method with no port cannot be made to
  ask.
- **Shared facts in `tests/TrainingHub.Api.TestKit/`**, so both hosts answer them: nine additions to
  `TrainingLifecycleTest`, run twice. `DomainEventPipelineTest` gained a tenth,
  `DeletingATrainer_AnnouncesEveryTrainingItTakesWithIt`: the fact beside it proves the rows leave,
  and proved only that, which is how the cascade stayed silent through the commit that shipped this
  record. It seeds two trainings on purpose — "one fact per training" is the claim, and one training
  cannot distinguish it from "one fact per cascade".

One rule was widened rather than added. `EveryValueObject_IsBuiltThroughAFactoryThatCanRefuse` named
`Topic` as its single recorded exception, which left the next closed set a choice between editing a
list of names by hand and writing a `TryFrom` that no caller would use. It now exempts closed
enumerations by shape — no public constructor, and instances that are its own static fields — which
is a stronger answer than a factory rather than a weaker one: a factory can refuse, whereas here
there is nothing to refuse.
