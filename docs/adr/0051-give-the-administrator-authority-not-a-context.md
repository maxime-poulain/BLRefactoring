# 0051 — Give the administrator authority, not a context

- **Status:** Proposed
- **Date:** 2026-08-07

**Why `Proposed`.** Nothing in this repository answers to this record yet. It becomes `Accepted` in
the commit that builds it, together with the rule that defends it — the treatment ADR 0050 used, and
for the same reason: writing the decision down before the code is the point, and claiming the code
already obeys it would be the lie this repository refuses everywhere else.

## Context

Three domain methods exist that no endpoint reaches, and the strategic design says so in as many
words. `Trainer.MarkForDeletion` has been in that state since the deletion endpoint was withdrawn;
`Trainer.Suspend` and `Trainer.Reinstate` joined it with ADR 0050. The actors table carries a row
for it:

| **Administrator** | Remove a trainer | **Named, not implemented.** no endpoint reaches it, because no role is entitled to it yet |

The event storming boards carry the matching hotspot — *"Nothing removes a trainer. The rule exists,
the event exists, the policy exists — and no actor can trigger any of it."* Building the
administration surface is what closes it.

What is genuinely open is **what kind of thing the administration is**. The word invites a bounded
context: there is an `/admin` prefix waiting to be written, a different audience, different screens.
Every one of those is a surface concern, and none of them is a model concern.

Two facts about the existing code decide the rest:

| Fact | Where it is visible |
|---|---|
| The token already carries role claims | `TokenService` emits one `ClaimTypes.Role` per role; `AuthControllerBase` already calls `GetRolesAsync` |
| **No role is ever granted, and none is seeded** | nothing in the solution names a role or writes one |
| A token cannot be issued to an account with no trainer | `TokenService` throws: *"No trainer is attached to identity account…"* |
| `ICurrentUserService` exposes `UserId` and `TrainerId`, nothing else | its whole surface |

So the pipe is built end to end and has never carried anything, and the one thing standing in the
way of an administrator who is not a trainer is a guard clause.

## Decision

**The administration is a second authority over Training Catalogue, not a second context.**

A bounded context bounds a *model* — the region where a word means one thing. Does `Trainer` mean
something else to an administrator? No: same aggregate, same invariants, same lifecycle. The
administrator reads more of it and may trigger transitions its owner may not, and that is
**authority**, not **vocabulary**. A difference of authority is authorization.

The comparison that makes the line visible is Catalogue Discovery, where `Training` becomes a
*search result*: another shape, another lifecycle, another consistency, another language — facet,
listing, relevance. That is a second model, and that is why it is a second context.

```
Trainer Portal ─┐
                ├──► Training Catalogue    same aggregates, different authorities
Admin Portal   ─┘
Public Website ────► Catalogue Discovery   another model, another consistency
```

Consequently: **no new project, no new context on the map, no new aggregate.** New use cases, new
endpoints behind a policy, a new screen.

**An administrator is an account, not a trainer.** `TokenService` stops requiring a trainer and
omits the `trainer_id` claim when there is none. `ICurrentUserService` gains a way to say "this
caller is nobody's trainer" rather than answering a `Guid` that names nothing. The alternative —
every administrator is also a trainer, carrying a publishable profile nobody will ever publish — is
a lie in the data model, and the kind this repository has removed elsewhere rather than added.

**Authorization is decided at the API and nowhere else.** An `AdministratorPolicy` beside
`TrainingOwnerPolicy`, registered in the same call so a host cannot hold half the pair. The
application layer never names a role: it does not depend on the API, and a use case that asked *who*
is calling would have to. The domain never hears of an administrator at all — `Trainer.Suspend` is a
domain method, and who may call it is not the domain's business, exactly as `Training.IsOwnedBy`
answers a question and leaves the caller to decide what refusing means.

**The first administrator is seeded in Development, and granted by hand anywhere else.** A startup
seeder reads a username from configuration — therefore from the git-ignored local overrides file of
ADR 0035 — and grants the role if the account exists. It runs in Development only, which is the
shape ADR 0003 already chose for applying migrations, and for the same reason: a convenience that
would be a hole in production. Elsewhere, the grant is a documented database operation. There is no
self-service path to becoming an administrator, and there is deliberately no endpoint that grants a
role.

## Consequences

- **The hotspot the boards have carried since they were written closes**, and the actors table's
  third row stops saying "not implemented".
- **`ICurrentUserService` changes shape, and it is consumed everywhere.** That is the real cost of
  this record and the reason it lands on its own: every caller that reads `TrainerId` has to say
  what it does when there is none.
- **`Trainer.Suspend` and `Reinstate` become reachable.** Their domain events stop being facts that
  only a unit test ever sees.
- **The latent defect ADR 0050 left, and #95 fixed, becomes live.** The trainer-deletion cascade now
  has a trigger; it announces every training it takes, and it did not until that commit. The order
  matters and it is the right way round.
- **Nothing about the context map moves.** No arrow, no box, no published language.
- **Two records follow this one** and are not folded into it: what an administrative decision does
  to a training is one decision (0052), and what a suspended trainer may still do is another (0053).

## Alternatives considered

**An `Administration` bounded context.** The obvious reading of the word, and the one to refuse.
It would duplicate `Trainer` and `Training` — or, worse, share them and call the sharing a context
boundary. Every candidate boundary signal fails: the vocabulary is identical, the invariants are
identical, the lifecycle is identical, and the consistency requirement is identical. What differs is
who is allowed to act, which authorization already expresses.

**A claim rather than a role.** `is_admin: true` on the token, no Identity role, no seeding. Cheaper
by a wide margin. Rejected because the framework's role machinery is already wired end to end and
already unused — the cost of using it is a seeder, and the cost of not using it is a second,
parallel notion of who somebody is. ADR 0023's Identity & Access section says the context is bought
rather than modelled; buying it and then declining to use it is the worst of both.

**Administrators are trainers with an extra role.** Zero code change: the token guard already
passes, `TrainerId` is always present, nothing downstream moves. Rejected because it makes every
administrator a publishable trainer with a contact address, a bio and a photo, which is a data model
that says something untrue. It would also put administrators in whatever listing the public
catalogue eventually grows.

**An endpoint that grants the role.** A `POST /Admin/administrators`. Rejected as the first thing an
attacker would look for, and as a use case with no user: administrators are made once, by whoever
runs the system, and a screen for it is a screen nobody opens.

## Verification *(when this is built)*

- A rule holding that **no inner layer names a role**: the application, domain and infrastructure
  assemblies never mention `Administrator` nor `ClaimTypes.Role`. Watched failing by naming the role
  in a use case.
- Shared facts in `tests/TrainingHub.Api.TestKit/`, so both hosts answer them: a trainer's token is
  refused `403` on an administrative endpoint, an anonymous caller `401`, an administrator's token
  passes.
- A fact that an account with no trainer receives a token, and that its `trainer_id` claim is absent
  rather than empty.
