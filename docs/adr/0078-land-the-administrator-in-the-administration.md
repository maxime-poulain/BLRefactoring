# 0078 — Land the administrator in the administration

- **Status:** Accepted
- **Amends:** [0074](0074-make-the-catalog-the-front-door.md)
- **Date:** 2026-08-13

## Context

ADR 0051 decided that an administrator is an account and not a trainer, and the API has held that
line ever since: `TokenService` mints `trainer_id`, `firstname` and `lastname` only when the account
has a trainer — absent rather than empty — and `TrainerPolicy`, carried by `ApiControllerBase`,
refuses the whole trainer surface to a caller without that claim. `AdministratorPolicy` guards the
administration from the other side. The seeded administrator is nobody's trainer, deliberately, and
a shared integration test pins the `403`s on both hosts.

The browser never asked. Signing in navigated to `/trainings` for everybody, `Login.razor` reading
neither the role nor the claim; `/trainings`, `/trainings/create`, `/trainings/edit/{id}` and
`/profile` carried a bare `[Authorize]`, which asks only whether somebody is signed in, while the
three administration pages carried `[Authorize(Roles = …)]`. That asymmetry is the whole defect.
An administrator's first screen was the trainer's dashboard: a red banner over an empty panel,
saying "The trainings could not be loaded. Try again in a moment." — a permanent refusal dressed as
a passing outage — under an invitation to create a training their account can never own. The user
menu offered them "Profile" and "My trainings", two doors onto the same `403`, and the layout's
suspension banner spent one refused `GET /Trainer/me` per scope discovering what the identity in
the browser's own hands already said.

That identity did have the answer. The BFF copies every claim of the token into the cookie —
`trainer_id` passes through the canonicalization untouched, no inbound map shortens it — and
`GET /bff/user` hands the whole list to the WebAssembly client. Nothing read it.

ADR 0074 built the user menu and listed its doors as "the profile, the trainings, the
administration by role, sign out". The qualifier attached to the administration's three and to
nothing else, which is the gap this record closes: that record did not know it had left the
trainer's own two doors unqualified.

## Decision

**The browser asks the same question the API asks.** A policy named `Trainer`, character for
character the API's `TrainerPolicy.Name`, requiring the claim `trainer_id`, character for character
the API's `TrainerClaims.TrainerId` — registered in the WebAssembly client and again in the
prerendering host, because the same components render on both sides and a page granted by one and
refused by the other is two hosts disagreeing about who the caller is.

**The trainer's routes and doors depend on that policy.** The four routed pages of the trainer's
space carry `[Authorize(Policy = …)]` rather than a bare `[Authorize]`, and the menu's "Profile"
and "My trainings" sit inside an `AuthorizeView` on the same policy. The administration's pages
keep their role guard, now named rather than quoted.

**A refusal answers with a destination when there is one.** `RouteRefusal` — the component formerly
named `RedirectToLogin`, whose name would otherwise have become false — sends an anonymous visitor
to sign in, shows a signed-in caller the refusal they can do nothing about, and sends an
administrator who reached the trainer's surface to the administration. That third case is a
different kind of refusal: an account that is nobody's trainer is not one authority away from
owning trainings, so there is nothing for them to come back for.

**The administrator has an address of their own, and lands on it.** `/administration` shows what
the three doors hold — trainers and how many are suspended, trainings and how many are withheld,
messages waiting to be requeued — each count read from a list the administration already publishes,
asked for a single row. It adds no endpoint, no contract and no query. Signing in chooses by what
the caller is: the return address if there is one, then the trainer's space, then the
administration, then the catalog. A trainer who also holds the role goes to their own work, and
keeps the administration one click away in the corner.

**The call that is certain to be refused is never made.** `TrainerStandingSource` reads the
identity before reaching for `GET /Trainer/me`, and answers "active" and "no portrait" without
touching the network when the caller carries no `trainer_id`. The guard lives there rather than at
its five call sites, because a guard written five times is four chances to forget it.

None of this is enforcement. The API still decides, and still answers `403` whatever the browser
renders (ADR 0057). What the mirror buys is that the doors offered are the doors that open.

## Consequences

- The browser writes three of the API's names again — the claim, the policy, the role — because it
  references the generated clients and nothing else, and taking a project reference on the API's
  assembly to read three strings would invert the dependency the boundary exists to keep. The copy
  is safe only because a rule compares it: `TheBrowsersTrainerDoors_AskTheApisOwnQuestion` reads
  the three files and holds each against the constant the API registers with.
- The role literal `"Administrator"`, previously written out in four places, is now named once.
- bUnit's authorization double grants a policy by name and never evaluates a claim, so the menu and
  page facts prove the markup asks for the policy and nothing more. The registration itself is
  exercised separately, against the real `IAuthorizationService`, which is the only place a policy
  bound to the wrong claim would be caught.
- An administrator's session now makes no trainer-scoped call at all.
- A trainer who reaches for the administration still meets the written refusal. Only the reverse
  direction redirects, and the record says why.
- The redirect belongs to navigation inside the running application — a stale bookmark followed
  from an open tab, a link left in a note. A cold request for a guarded address never reaches the
  browser's router at all: the host maps each page endpoint with its own attribute, so it answers
  the request itself — `401` to a visitor, as it always has, and now `403` to an administrator.
  That is the host being right rather than a gap, and making it render an explanation instead is a
  separate decision about the anonymous case first, which predates this record.

## Alternatives considered

- **Read the role instead of the claim.** Rejected: "not an administrator" does not mean "is a
  trainer". The honest predicate is the one the API uses, and using anything else would create a
  second notion of trainer in the browser — the exact drift this repository writes rules against.
- **Land everybody on the catalog and let the corner take them where they want.** Rejected: it is
  the smallest change and the least useful one. An administrator signing in has come to administer,
  and a landing page that asks them to look for their own surface is a menu, not an answer.
- **Show an administrator the refusal screen on the trainer's routes.** Rejected for that
  direction. The screen exists for a caller who is missing an authority they could conceivably be
  granted; an administrator is missing a trainer, which is not an authority at all. It stays for a
  trainer reaching the administration, where the sentence is true.
- **Hide the doors and change nothing else.** Rejected as the version that looks fixed: the menu
  would be tidy and the first screen after signing in would still be a refused call. ADR 0057
  already says hiding a control is never the enforcement, and it is not the remedy either.
- **Give the administrator a trainer, so every account is one shape.** Rejected by ADR 0051 before
  this record existed, and for the same reason: it makes every administrator a publishable trainer
  with a bio, a contact address and a photo, which is a data model that says something untrue.

## Verification

- `TheBrowsersTrainerDoors_AskTheApisOwnQuestion` proved red three ways before being declared
  green: a trainer page stripped of its policy, the browser's claim name drifted from the API's,
  and — for the half no rule can see — the policy registered against the wrong claim, caught by
  `SessionPoliciesTests` against the real authorization service.
- bUnit facts: signing in as a trainer, as an administrator and as neither; the menu of an account
  with no `trainer_id`; the refusal that redirects; the standing source that asks the API nothing.
- Run end to end with `docker compose --profile full up`, signing in as the seeded administrator
  and as a trainer.
