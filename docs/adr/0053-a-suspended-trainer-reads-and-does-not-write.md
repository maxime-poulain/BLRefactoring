# 0053 — A suspended trainer reads, and does not write

- **Status:** Accepted — amended by [0057](0057-the-trainers-own-surface-says-where-they-stand.md): the write controls a suspension forbids are shown disabled rather than removed
- **Amends:** [0050](0050-retire-a-training-rather-than-delete-it.md)
- **Date:** 2026-08-07

**This record was `Proposed` until the commit that built it**, which is also the commit in which it
amends ADR 0050 — an amendment declared while nothing had changed would have been a claim about code
that still said the opposite.

## Context

ADR 0050 gave the trainer a standing and said precisely what a suspension costs them:

> **A suspended trainer may not increase their public footprint.** Refused: create, publish,
> transfer — giving and receiving alike. **Permitted: edit, unpublish.** One sentence to remember,
> and it leaves the trainer able to repair what earned them the sanction.

That sentence is implemented and correct as written: `Training.Unpublish` takes no port at all, and
`EditAsync` takes no standing port, so neither can be made to ask.

The administration surface makes the sentence's *justification* checkable for the first time, and it
does not hold. **"Repair what earned them the sanction" presumes a review loop.** Repairing means
something only if repairing leads somewhere — if some path exists by which a corrected training gets
looked at again and the suspension lifted. No such path exists, and none is being built: an appeal
is a workflow with a lifecycle of its own, which is the point at which moderation becomes a context
rather than an authority, and that boundary is deliberately not being drawn yet (ADR 0051, ADR 0052).

So the trainer edits, and nothing happens. Nobody is told, nothing is re-examined, the suspension
stands. The permission does not empower them; it misleads them about their situation. That is a
defect of experience, and it is worse than a refusal because it looks like a remedy.

There is a second, smaller thing the epic surfaces. The suspension has to be explained to the person
it lands on, with its reason. If the trainer cannot sign in, the only channel is the email, and the
product itself never accounts for its own decision.

## Decision

**A suspended trainer keeps every read and loses every write.**

| Surface | Suspended trainer |
|---|---|
| Sign in | ✅ |
| Read their profile, their trainings, their photo | ✅ |
| Read why they are suspended | ✅ — shown, not buried |
| Create, edit, publish, unpublish, transfer, change their photo | ❌ `403` |

**Keeping the reads is not a softening of the sanction; it is what makes the sanction accountable.**
A decision the product refuses to explain to the person it affects is a decision that exists only in
an email they may never have received.

**Losing the writes includes `unpublish`, and that is the amendment.** ADR 0050 permitted it on the
ground that a suspended trainer should be able to shrink their own footprint. The ground is sound and
the consequence is empty: a suspended trainer's catalogue is already invisible, composed away by
their standing, so unpublishing changes nothing anybody can observe. The permission buys nothing and
costs the confusion above.

**The refusal is at the boundary, and the domain does not move.** `Training.Unpublish` keeps its
empty signature; `EditAsync` keeps its ports. What changes is that the API refuses the request
before a use case runs. The domain's existing standing rules — create, publish, transfer — stay
exactly where ADR 0050 put them, as defence in depth: they answer any caller that reaches a
dispatcher, which is the same argument ADR 0046 makes for validating an identifier the boundary
already checked.

**The trainer is told twice: once by email, once by the product.** The email carries the fact and
the reason; the trainer space carries the same reason, permanently, wherever they look. And it names
the only recourse there is — an address to write to. The product says plainly that there is no
appeal flow rather than implying one.

## Consequences

- **One sentence of ADR 0050 stops being true**, and this record is what says so. The rest of that
  record is untouched: the states, the composition, the quota, the refusals on create, publish and
  transfer all stand.
- **The lockout is a boundary concern**, so it is a policy and its tests are integration tests. The
  domain suite does not change.
- **A trainer who was mid-edit loses their work on suspension.** Accepted: a suspension is not
  scheduled around a draft, and there is no draft to lose — this repository has no `Draft`.
- **The one thing a suspended trainer can still do is read and write an email.** That is thin, and
  it is written down here rather than discovered later. The first person who asks for an appeal form
  is asking for a bounded context, and the answer will be a new record, not a field.
- **`Trainer.Suspend` and `Reinstate` are still the only writers of the standing.** Nothing about
  this record touches the trainings of a suspended trainer, which is ADR 0050's central claim and
  the reason a reinstatement costs nothing.

## Alternatives considered

**Keep ADR 0050 as written.** Edit and unpublish stay permitted. The defensible position, and the one
to beat: it is strictly more generous, and generosity toward someone under sanction is rarely wrong.
Rejected because the generosity is illusory — nothing observes the repair — and because an interface
that accepts an edit implies the edit matters.

**Refuse the sign-in outright.** The strongest reading of "suspended", and the one first asked for.
Rejected on one concrete consequence: the trainer can then only learn why from an email, and a
product that sanctions someone should be able to tell them so itself. It also makes the sanction
indistinguishable from an outage from the user's side.

**Lock the writes and hide the trainings.** Show the suspended trainer an empty catalogue, since the
public sees one. Rejected as a second lie: their trainings exist, they are theirs, and hiding them
from their owner serves nobody. The public's view is composed; the owner's view is not.

**Build the appeal now, and keep edit permitted.** The version in which ADR 0050's justification
becomes true. It is the honest alternative and it is a much larger piece of work: an appeal has
states, actors and a queue, which is the boundary signal that turns moderation into a context. Left
for a record of its own, if and when somebody asks.

## Verification

- Shared facts in `tests/TrainingHub.Api.TestKit/`, so both hosts answer them: a suspended trainer
  receives `403` on every write of the trainer surface, and `200` on every read.
- The one that would otherwise rot: **`unpublish` is refused too**. It is the endpoint ADR 0050
  explicitly permitted, so it is the one a future reader will assume still works.
- A `bUnit` fact that the trainer space shows the reason, and that the write controls are absent
  rather than present-and-failing.
