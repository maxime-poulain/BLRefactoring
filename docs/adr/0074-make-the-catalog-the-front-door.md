# 0074 — Make the catalog the front door

- **Status:** Accepted
- **Amends:** [0071](0071-give-the-catalog-a-second-published-order.md),
  [0072](0072-prerender-the-catalogs-routes.md)
- **Date:** 2026-08-11

## Context

The root served a landing page that told visitors the catalog exists, one click away. Everything a
visitor could want was already on the other side of that click: the search, the topic facets, the
two published orders, the pagination, the prerendered head. The landing page itself was not
prerendered — deliberately, being outside ADR 0072's closed set — so the most-linked address of
the application answered a crawler with an empty shell, and answered a person with a detour.

The bare catalog, meanwhile, opened on the alphabet. A–Z was named the default in ADR 0071 as the
order a visitor scans — a defensible reading for a page reached on purpose, and the wrong one for
a front door: what a returning visitor wants from a landing is what is new, and the alphabet
serves only somebody looking up a title they already know, which the search serves better.

The corner of the bar, finally, was a row: two buttons for the trainer's space, three more for an
administrator, a username, a sign-out icon. It identified nobody at a glance and it grew with
every role.

## Decision

**The root serves the catalog itself; the bare address answers newest first; the corner becomes
the user menu. The landing page retires.**

- **One page, two addresses.** `Catalog.razor` answers `/` and `/catalog` — a route alias, not a
  redirect and not a copy. The root joins ADR 0072's closed set of prerendered routes, so the
  application's most-linked address serves the catalog's own words as HTML. The canonical is
  untouched: it keeps naming `/catalog` (ADR 0073), so the two addresses stay one page to an
  index, and the sitemap and robots change not at all.
- **Newest first is the default, everywhere the default is spoken.** The shared mapping falls
  back to `CatalogOrder.Newest`, the page's bare address means newest, and the alphabet becomes
  the order a caller asks for — `?sort=title` — while the default keeps traveling as no parameter
  at all. What "newest" means is exactly what ADR 0071 decided and is not touched here: the
  training's own age, replay-stable, unmoved by republication. The first paint of the front door
  is the twenty youngest trainings on offer, the kernel's own page of them.
- **The corner identifies its caller.** A visitor sees one action: sign in. A signed-in caller
  sees a compact identity — the portrait when there is one, initials when there is not, and the
  person's name from the token's own claims, which cost no call — opening onto the doors that
  used to sit in the bar: the profile, the trainings, the administration by role, sign out. The
  portrait's address comes from the same one read the suspension banner already makes, asked a
  second question rather than made twice; it is the authenticated route with the photo's identity
  as a cache buster, never the public one, which answers 404 for a suspended trainer who is still
  entitled to their own face. An account that is nobody's trainer — an administrator — gets its
  account name and initials, which is everything the token honestly knows about it.

## Consequences

- A visitor's first address is the shelf: search, facets, orders and pagination with no detour,
  and the newest trainings first. The catalog stops being a destination and becomes the ground.
- A crawler reading `/` gets prerendered content where it got an empty shell, and the canonical
  folds whatever link equity the root gathers into `/catalog`.
- `Home.razor` and its tests retire; the not-found page offers one door where it offered two,
  home and the catalog now being the same place.
- The prerendered pass of a signed-in caller renders initials rather than the portrait — the
  host's API client carries no token, and the interactive pass corrects it — which is the honest
  variant of ADR 0072's rule that every injected service gets the host's honest answer.
- The bar holds the brand, the catalog and the identity, whoever is signed in — the row that grew
  with every role is a menu now.

## Verification

- `TheFrontDoor_IsTheCatalog` (PrerenderingRules) — the catalog page answers both addresses, the
  deciding file keys the root into the prerendered set, and the shared mapping's fallback is the
  newest order; proved by mutation before it was trusted.
- `The_front_door_is_the_prerendered_catalog` and `The_login_page_is_not_prerendered` (BffTests)
  — the root serves the catalog's words as HTML with the canonical still naming `/catalog`, and
  the closed set still has an outside.
- `TheBareCatalog_AnswersTheYoungestTrainingFirst_ToACallerWithNoToken` (TestKit, both hosts) —
  the bare address walks youngest first over the wire, `?sort=title` walks the alphabet, and
  `?sort=newest` stays a valid spelling of the default.
- The user menu's facts (MainLayoutTests) — the doors by state, the name from the claims, the
  account-name fallback, the initials, the portrait, and the sign-out reaching the BFF.
