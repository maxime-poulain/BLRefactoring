# 0052 — Make an administrative removal a state of its own

- **Status:** Proposed
- **Date:** 2026-08-07

**Why `Proposed`.** Nothing answers to it yet; it becomes `Accepted` in the commit that builds it,
with the rules that defend it. Same treatment as ADR 0050, for the same reason.

## Context

The administration must be able to take a training out of public view, and must give a reason for
it. The obvious implementation is the one already there: set `Status` to `Unpublished`, the state
ADR 0050 gave the owner's own withdrawal.

**It does not survive its first test.** `POST /Training/{trainingId}/publish` is open to the owner.
An administration that only sets `Unpublished` is undone by the trainer in one request, and the
moderation is worth nothing.

That is not a new discovery. ADR 0050 considered exactly this shape — a single state carrying a
reason — and rejected it in terms that decide the present question:

> **One state plus a reason.** `Unpublished(reason: Owner | Moderation)`. Keeps a single state and
> records why. Rejected because the two differ in what is *permitted*, not merely in provenance — an
> owner may republish what they withdrew and must not republish what was taken from them — and a
> rule about what is permitted belongs **in a state**, not in a field beside one. It is also the
> first step toward a moderation record, which is a boundary this repository has deliberately not
> drawn.

The boundary is still not being drawn. What has changed is that the case the record deferred has
arrived, and the record already said what shape it takes.

A second, smaller thing is open: the reason has to be readable by the trainer *after the fact*, and
the outbox is not a store. ADR 0033 sweeps delivered messages on a retention period; a fact that has
been delivered and swept cannot answer "why is my training unavailable".

## Decision

**A training is `Published`, `Unpublished`, or `Withheld`.**

```
       ┌─────────────┐                              owner:  publish, unpublish
  ────►│  Published  │◄──────┐                      admin:  withhold, release
       └──────┬──────┘       │ publish
    unpublish │              │
              ▼              │
       ┌─────────────┐───────┘
       │ Unpublished │◄──────────── release ────────┐
       └──────┬──────┘                              │
              │                                     │
              └──────── withhold ──►┌─────────────┐─┘
       Published ─────── withhold ─►│  Withheld   │
                                    └─────────────┘
```

- **`Withheld`, not `Suspended`.** `Suspended` is the trainer's word (ADR 0050); one word meaning two
  things across two aggregates is the confusion this model was built to avoid. Not `Disabled` — ADR
  0050 rejected it as "a word about systems". Not `Archived` — same record, "promises a retention
  policy that does not exist". Not `Moderated`, which names a process rather than an outcome:
  reviewing a training and clearing it is also moderating it. `Withheld` is the publishing world's
  own word for a work kept back from distribution, it sits beside `Published` and `Unpublished`
  because all three are about the act of publishing, and it implies an actor other than the owner
  without naming one.
- **This does not reopen `Draft`.** ADR 0050 refused `Draft` because *"a state nothing can remain in
  is not a state"*. A training can remain withheld indefinitely.
- **`Withhold` accepts either starting state.** Prohibited content is prohibited whether or not it
  is currently on offer, and withholding an already-withdrawn training is what stops its owner
  putting it back.
- **`Release` lands on `Unpublished`, never on `Published`.** No memory of the prior state is kept,
  deliberately: the administration lifts an interdiction, it does not decide to put a training back
  in the window. Publishing is the owner's call again. Zero extra field, and the more honest reading
  of what releasing means.
- **The owner cannot leave `Withheld`.** `PublishAsync` refuses it by name. That refusal is the
  whole reason the state exists.
- **A withheld training keeps its place in the quota.** ADR 0050 made the ten count published
  trainings, on the grounds that a trainer who withdraws ten should not lose their catalogue for
  ever. That argument is about a *voluntary* withdrawal. Freeing a slot by being moderated is a
  perverse incentive, so the count becomes "not withdrawn by its owner" rather than "published".
- **The reason lives on the aggregate, beside the state that gives it meaning**, as a value object
  with the shape `Bio` already has: non-empty, bounded. The invariant is stated in both directions —
  **the reason is present if and only if the state is the one it motivates** — which is what forbids
  an orphan reason and a mute state alike.

The same shape applies to the trainer: `Trainer.Suspend` takes a `SuspensionReason`, `Reinstate`
clears it.

## Consequences

- **Two new business facts**: a training withheld, a training released. Both announce themselves,
  which the rule ADR 0050 left behind already demands of any member that moves a status.
- **Only one of the two becomes an integration event.** Withholding has a consumer — the notifier
  and the index. Releasing has none: the training lands on `Unpublished`, where it was not indexed
  and nobody was waiting. That is ADR 0050's own test — *nothing produces it, nothing consumes it* —
  applied again rather than forgotten.
- **`Training.PublishAsync` grows a third refusal**, and it is the one a trainer will actually meet.
  Its error code has to say what happened without leaking a moderation policy.
- **The counting specification changes meaning for the second time.** It stops being
  "is published"; the rule and the criteria move together or the quota is briefly wrong, so they
  land in one commit.
- **The trainer's own listing shows a third status**, and must show the reason with it. A state the
  interface renders as merely "not published" would hide the one thing the trainer needs to know.
- **No moderation model appears.** There is a current reason for a current state, and nothing else:
  no decision entity, no history, no appeal. What that costs is written down in 0053 and in the
  strategic design rather than left to be discovered.

## Alternatives considered

**Reuse `Unpublished` and record the actor.** The cheapest option, and the one this record exists to
refuse — for the reason ADR 0050 already gave, which the `publish` endpoint makes concrete: a rule
about what is *permitted* cannot live in a field beside the state, because nothing consults it.

**Refuse the owner's `publish` while any moderation is open, without a third state.** Keeps two
states, moves the rule into the use case. Rejected on the same grounds: the rule then lives in one
caller, and the aggregate — asked directly, as the transfer domain service asks it — would happily
publish a training that the administration removed.

**Suspend the trainer instead.** A blunt instrument: one bad training costs the trainer their whole
catalogue. It also conflates two sanctions of very different weight, and leaves no way to act on a
single training at all.

**Keep the reason only on the event.** Attractive because the reason is genuinely a property of the
decision rather than of the training. Rejected because the trainer must be able to read it later,
and the outbox is swept (ADR 0033). Persisting it beside the state is the smallest thing that
answers the requirement.

**A `ModerationDecision` aggregate holding actor, timestamp, reason and target.** The complete
answer, and premature. It is justified when a decision needs an identity of its own, a lifecycle of
its own, or more than one actor — none of which is true today. Who acted and when is already
recoverable: ADR 0027's enricher stamps the caller on every log line, and the audit handlers run
inside the transaction.

**`Release` restores the prior state.** Symmetrical, and it requires remembering what that state
was: a second field, written on every withholding, read once. Rejected for the reason ADR 0050
rejected the cascade — a field that exists to remember something is the thing to avoid when the
behaviour can be had without it. Landing on `Unpublished` gives the decision back to the owner,
which is also what lifting an interdiction means.

## Verification *(when this is built)*

- Every transition and every refused transition on the aggregate, including the one that matters
  most: **the owner publishing a withheld training is refused by name**.
- The invariant in both directions — reason without the state, and the state without a reason — each
  watched failing.
- The quota counting a withheld training, end to end: a trainer at the limit whose training is
  withheld still cannot create an eleventh.
- `EveryStatusTransition_AnnouncesItself` covers the two new transitions without being touched,
  which is the point of having written it as a shape rather than a list.
- Shared facts in `tests/TrainingHub.Api.TestKit/`, so both hosts answer them.
