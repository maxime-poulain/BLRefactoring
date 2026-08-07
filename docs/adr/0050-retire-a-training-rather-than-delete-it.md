# 0050 — Retire a training rather than delete it

- **Status:** Proposed
- **Date:** 2026-08-07

**Why `Proposed` and not `Accepted`.** The design below is settled — it came out of a review that
argued it against the model and rejected three earlier shapes. What is not settled is the code:
nothing in this repository answers to this record yet. `AdrRecord.IsInForce` reads that status and
excludes the record from `EveryRecordInForce_IsDefendedByARule_OrSaysWhyItCannotBe`, whose comment
says why — *"a proposed record is not a decision yet"*. This becomes `Accepted` in the commit that
implements it, together with the rules that defend it. Writing it down first is the point; claiming
the code already obeys it would be the lie this repository refuses everywhere else.

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

## Verification *(when this is built)*

Nothing here is defended by a rule today, and that is what the `Proposed` status says. The commit
that implements it carries, at minimum:

- the transitions and the refused transitions, on the aggregates;
- the three invariants this record changes — the quota, the title, and what a suspended trainer may
  do — each watched failing before it is made to pass;
- shared facts in `tests/TrainingHub.Api.TestKit/`, so both hosts answer them;
- a rule holding that every state transition raises a fact, which is the claim that separates this
  from a soft delete and the one a reader is entitled to see enforced.
