# 0051 — Give the administrator authority, not a context

- **Status:** Accepted
- **Date:** 2026-08-07

**This record was `Proposed` for exactly one commit**, the treatment ADR 0050 used and for the same
reason: writing the decision down before the code is the point, and claiming the code already obeys
it would be the lie this repository refuses everywhere else. It became `Accepted` in the commit that
built it, together with `NoInnerLayer_NamesARole`.

**Two paragraphs of the decision changed while it was being built, and they are named here rather
than quietly rewritten.**

The first: the draft said `ICurrentUserService` would gain a way to say *"this caller is nobody's
trainer"*, and called that the record's real cost. Building it showed the accessor to be the wrong
place: all eleven readers of `TrainerId` sit behind the trainer surface, no administrative use case
wants the caller's identity at all, and a member nothing would ever call is not a defence — it is
dead code wearing one. The refusal moved to where this record says every authorization decision
belongs, the boundary, as `TrainerPolicy`.

The second: the draft had the seeder *grant the role to an account that already exists*, named in
the git-ignored overrides file. That leaves a reader of this repository three manual steps from
seeing the administration at all — register, edit a file they have to create, restart — and this
project exists to be read. The seeder now **creates** the account as well, from credentials the
Development configuration carries. The paragraphs below state what was built.

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

**An administrator is an account, not a trainer.** `TokenService` stops requiring a trainer: the
`trainer_id`, `firstname` and `lastname` claims are simply absent when the account has none — absent
rather than empty, since an empty identifier satisfies every check that only asks whether the claim
is there and then fails inside a `Guid.Parse` two layers away. The alternative — every administrator
is also a trainer, carrying a publishable profile nobody will ever publish — is a lie in the data
model, and the kind this repository has removed elsewhere rather than added.

**Authorization is decided at the API and nowhere else**, and there are now three policies,
registered by one call so a host cannot hold two of the three:

| Policy | Demands | Carried by |
|---|---|---|
| `TrainingOwnerPolicy` | the caller owns the training the route names | five write actions |
| `TrainerPolicy` | the caller is somebody's trainer | `ApiControllerBase`, so every trainer action |
| `AdministratorPolicy` | the `Administrator` role | the administrative endpoints, when they exist |

`TrainerPolicy` is what makes an administrator's token safe to receive. Without it, the first thing
such a caller meets on the trainer surface is `CurrentUserService.TrainerId` raising on the absent
claim, which is a `500` where a `403` belongs. Refusing once at the boundary is also what keeps the
eleven readers of `TrainerId` free of the question: none of them can be reached without the claim,
so none of them has anything to say about its absence. Only `TrainingOwnerPolicy` needs a handler of
its own — ownership is a question only the database can answer, while a role and a claim are already
in the token the caller presented.

The application layer never names a role: it does not depend on the API, and a use case that asked
*who* is calling would have to. The domain never hears of an administrator at all —
`Trainer.Suspend` is a domain method, and who may call it is not the domain's business, exactly as
`Training.IsOwnedBy` answers a question and leaves the caller to decide what refusing means.

**The first administrator exists by default in Development, and is made by hand anywhere else.** A
start-up seeder reads a username and a password from configuration, creates the account if it is
missing, and grants it the role. The committed Development configuration names **`admin` / `admin`**,
so a clone of this repository has a working administrator after `docker compose up` and
`dotnet run`, with nothing to set up. Naming another username in the git-ignored overrides file of
ADR 0035 overrides it; an account that already exists keeps its password, because this is a seeder
and not a reset.

**A known credential is safe only because of the gate above it, so the gate is the decision.** The
seeder runs in Development alone — the shape ADR 0003 already chose for applying migrations, and for
the same reason — and two independent things must both hold before it creates anything: the host
runs as Development, and a configuration section that exists in no other committed file names a
password. `admin`/`admin` is a **fixture, not a secret**: writing it in the open is what makes the
gate the only thing anyone has to check, where a password hidden in a committed file would invite
the belief that hiding it was the protection. Elsewhere the grant is a documented database
operation, there is no self-service path to becoming an administrator, and there is deliberately no
endpoint that grants a role.

**The seeded account is nobody's trainer**, which is this record's central claim made concrete
rather than merely asserted: the default administrator is the first account in the system whose
token carries no `trainer_id`, and the trainer endpoints refuse it with the `403` that
`TrainerPolicy` exists to produce.

## Consequences

- **Half of the hotspot the boards have carried since they were written closes**, and the actors
  table's third row stops saying the permission is absent. The actor exists; the commands it would
  issue do not, and the boards now say which half is which rather than one sentence covering both.
- **The trainer surface answers `403` where it used to answer nothing at all**, because the caller it
  refuses could not exist before. Every action of both hosts declares it, so the published document
  and the generated client changed with them. This was expected to be the record's real cost, in the
  shape of `ICurrentUserService` changing and eleven callers with it; one policy on the shared
  controller base turned out to be the whole of it.
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

**No default account: seed the role onto an account somebody registered.** What this record said
first, and it is the more cautious option. Rejected on what it costs a reader: three manual steps —
register, create an overrides file, restart — before the administration can be seen at all, in a
repository whose purpose is to be read. The generosity was also illusory, since the account it
promotes is a *trainer* with the role added, which is the data model this record calls a lie two
paragraphs above.

**A random password, printed to the log at start-up.** Strictly better security, and the shape a
real product would ship. Rejected here because it is not reproducible: the credential changes on
every database reset, README instructions cannot name it, and a reader who missed the line has to go
looking through logs. The trade is deliberate and it is a showcase's trade, not a product's — the
day this repository is deployed anywhere, this is the paragraph to come back to.

## Verification

- **`NoInnerLayer_NamesARole`**, which reads the source of everything under `src/` that is not a
  boundary and refuses the vocabulary of authorization there. Stated by exclusion, so a project
  added tomorrow is watched without anybody remembering to add it. Watched failing by naming the
  role in a CQRS command, and it named the file and the term.
- **Shared facts in `tests/TrainingHub.Api.TestKit/`**, so both hosts answer them: an account with
  no trainer signs in and receives a token whose `trainer_id` is absent rather than empty and whose
  role claim is present; that token is refused `403` on a read, a write and a creation of the
  trainer surface; an anonymous caller still receives `401` rather than `403`; and a trainer still
  reaches their own profile — the last one because a requirement nobody satisfies refuses everybody,
  which passes every test that only checks for refusals.
- **The seeder, branch by branch**, against a real `UserManager` and `RoleManager` over SQLite in
  memory. Two of its facts carry the weight of the paragraph above: outside Development nothing is
  created even with a password configured — *"a known credential in production is the hole this gate
  exists to close"* — and the credential the repository actually ships, `admin`/`admin`, is created
  and can sign in, so the README cannot go on promising a sign-in the password policy would refuse.
  A third holds that an account already there keeps its password, which is what stops the
  configured one being a way to take over an existing account.
- **`PolicyRegistrationTests`**, on the three policies and the handler. A missing policy is not
  reported at start-up: ASP.NET Core discovers it when a request reaches the action that names it,
  and answers `500`. It is also the only way to assert `AdministratorPolicy` at all until the first
  administrative endpoint exists — a policy nothing carries is never evaluated. That endpoint-level
  proof, a trainer refused `403` and an administrator passing, arrives with the use cases.
