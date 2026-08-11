# 0072 — Prerender the catalog's routes

- **Status:** Accepted
- **Date:** 2026-08-10

## Context

`App.razor` has carried the question for a long time, as a comment that ended *"it is not made
here."* Prerendering used to be impossible — the JWT lived in `localStorage`, which a server
cannot read — and ADR 0009 removed that obstacle when identity became a cookie the host reads.
What remained were two reasons to keep it off: the pages are interactive MudBlazor controls that a
prerendered pass renders inert, and the application was entirely behind authentication, so there
was nothing for a crawler to read anyway.

ADR 0062 made the second reason half false, and each record since has widened the public half:
`/catalog` and `/catalog/{id}` (ADR 0062), the facets (ADR 0069), a person's page (ADR 0070). A
public catalog is exactly the kind of page a crawler is meant to read, and what a crawler reads
today is an empty shell — the words arrive only once WebAssembly boots, which is to say never, for
anything that does not run scripts. The first reason, meanwhile, is untouched for the signed-in
screens and simply does not apply to the catalog's: they are pages of content, and a first paint a
visitor cannot click for a moment costs nothing when there is nothing to click but links.

## Decision

**The catalog's routes prerender; nothing else does. One file decides, per request path.**

- **`App.razor` computes the render mode per request.** It renders statically, once per request,
  which makes it the one place that can decide per address: `prerender: IsCatalogRoute`, where the
  route test names `/catalog` and its children and nothing broader. `HeadOutlet` and `Routes`
  share the mode, or the head's content is written twice. The decision that was "not made here" is
  made here, and nowhere else — a page that declared a `@rendermode` of its own would take the
  decision out of the one place the rule reads.
- **The host answers the prerendered pages' questions itself.** The browser's registrations are
  not adopted — that collision is recorded and guarded — and each injected service gets the host's
  honest counterpart: identity is the cookie the BFF pipeline already authenticated, read in place
  through a host-side `AuthenticationStateProvider` rather than fetched from `/bff/user` by the
  host calling itself; the generated read clients ride the BFF's own named client to the API,
  under its name and never under `Api`; the layout's session client and standing source resolve
  because instantiation must, and behave honestly if reached. The layout itself stopped naming the
  browser's concrete provider — it announces sign-out through `IAuthenticationStateNotifier`,
  which each side answers in its own way, the host's with a no-op.
- **The answer travels once.** The three pages hand their prerendered state to the interactive
  pass through `PersistentComponentState`, so the first paint is the real page rather than a
  spinner, and the WebAssembly boot does not ask the API a question the server just answered. A
  failed load is deliberately not handed over: the interactive pass asks again rather than
  inheriting a snackbar it never showed.

## Consequences

- A crawler reading `/catalog`, a training's page or a trainer's profile gets the page's own words
  as HTML — the discoverability half of what ADR 0062 opened, delivered.
- A visitor's first paint of the catalog is the catalog, not a boot screen; the interactive pass
  takes over the same markup without refetching.
- The signed-in screens keep the behavior the old comment defended: no inert forms, no prerender
  of pages a crawler has no cookie for.
- The host now renders the client's components, so a service a catalog page grows must have a
  host-side answer — the failure is at runtime, which is why the BFF suite renders the page for
  real rather than trusting the registrations.

## Verification

- `ThePrerenderedRoutes_AreExactlyTheCatalogs` (PrerenderingRules) — the deciding file keys the
  mode to the catalog's routes and nothing broader, and no client page declares a render mode of
  its own; proved by mutation before it was trusted.
- `The_catalog_page_is_prerendered_for_a_visitor_with_no_session` (BffTests) — a plain GET of
  `/catalog` answers HTML carrying the page's own words, which is also the proof that the host
  resolves everything the page and the layout inject.
- `The_home_page_is_not_prerendered` (BffTests) — the other half of the closed set, observed from
  outside.
