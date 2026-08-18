# 0088 — Answer in the visitor's language, and resolve it at the door

- **Status:** Accepted — amended by [0089](0089-localize-every-surface-and-translate-the-refusal-at-its-funnel.md): the problem funnel now presents each cataloged refusal in the resolved culture, so `domainErrors[].errorMessage` is the domain's sentence only where the catalog has no entry
- **Date:** 2026-08-18

## Context

TrainingHub speaks English, everywhere, to everyone: `<html lang="en">` was hardcoded, no host
resolved a request culture, the WebAssembly boot set none, and every label on every screen is an
English literal. Opening the catalog to visitors (ADR 0062, ADR 0074) opened it to visitors who
read French or Russian, and nothing in the architecture had a place to put their language.

The constraint that shapes everything else is the first paint. The catalog's routes prerender
(ADR 0072): the server sends a complete, painted page, and nothing can correct it until the
WebAssembly runtime has booted. The theme faced the same constraint and accepted a corrected paint
for the non-prerendered routes, because its choice lives in `localStorage` — deliberately outside
anything the server knows (ADR 0077). A language cannot accept that trade. A page in the wrong
theme is briefly ugly; a page in the wrong language is a page the visitor cannot read, for the
whole time-to-interactive. So the server must know the language **before the first byte**, which
rules the browser's own storage out and decides most of what follows.

Three surfaces have to agree. The browser talks to the BFF; the BFF prerenders pages and forwards
API calls through YARP; the API hosts answer the sentences that end up on screens — a validation
refusal, a domain error's message. A design in which those legs resolve the language independently
is a design in which they eventually disagree, and a visitor reads Russian pages around French
error sentences.

One boundary must not move: the domain authors **codes** (`Training.DuplicateTitle`), each owned
by its aggregate (ADR 0015), and `ErrorCode` already documents the split that matters — the
message may be reworded freely, the code may not. The ubiquitous language of this repository is
singular and written in American English (ADR 0064); translations are presentations of it, not
additions to it. A domain that resolved `CurrentUICulture` inside its pure static factories would
produce facts — outbox events, stored messages — whose wording varied with whoever happened to
call, and its three hundred culture-free tests would be asserting on an accident. If the business
itself ever needs multilingual *data* — a training described in two languages — that is a domain
concept deserving its own record, and deliberately not this one.

Two facts about the platform bound the design. On .NET 10, a WebAssembly application loads
globalization data for `DefaultThreadCurrentUICulture` as well as `DefaultThreadCurrentCulture`,
and `BlazorWebAssemblyLoadAllGlobalizationData` ships the ICU data whole; the framework's
built-in "persist the server's culture into component state for the WebAssembly boot"
(`UseCultureFromServer`) exists only from .NET 11. So the handoff between the server's resolution
and the client's boot is this record's to design, and sheds a step on the next upgrade.

## Decision

**One resource assembly reachable from every surface and referencing nothing; one supported list
with an explicit English default; the culture resolved once at the BFF's door and restated to the
API in the standard header; the resolved answer stamped on the document, where the WebAssembly
boot reads it back. The domain keeps authoring codes and never learns the words.**

- **`TrainingHub.Translations` is the words, and nothing else.** Marker types
  (`CommonResources`, `ValidationResources`, `DomainErrorResources`) sit beside their `.resx`
  families — neutral English plus `fr` and `ru`, compiled into satellite assemblies. The project
  declares zero references in either direction of concern: the WebAssembly client cannot reach
  `Shared.Api` (only that layer may carry the web framework), so the words live in an assembly
  everything may load — which also means anything *it* referenced would ride into every surface,
  so it references nothing. `SupportedLanguages` lives there too: one list (`en`, `fr`, `ru`) and
  one default (`en`), read by the hosts' options, the culture endpoint, the boot and the selector,
  because two lists would drift and a language one leg offers while another refuses is the
  disagreement this record exists to prevent.
- **Neutral cultures, and the default is English.** A Belgian browser sends `fr-BE`; the
  framework's parent fallback lands it on `fr` for free, and a regional variant is added the day
  its prose diverges, not before. Formatting culture and language travel together — one list for
  both — because a French sentence around an English-formatted date is two languages on one line.
  English is the explicit default everywhere: the repository's own language, and what a crawler
  with no cookie receives.
- **The BFF resolves: cookie, then `Accept-Language`, then English.** The standard
  `.AspNetCore.Culture` cookie **is** the persistence — written by `POST /bff/culture` (validated
  against the supported list), a year long, `HttpOnly`, and `SameSite=Lax` where the session
  cookie is `Strict`: a preference must arrive on a navigation from elsewhere or the first paint
  after following a link would be in the wrong language, and it authenticates nobody. No account
  preference in this phase — the cookie survives sign-out, and cross-device continuity is a
  decision to take the day somebody asks for it.
- **The API hosts read `Accept-Language` alone**, through a shared
  `AddApiLocalization`/`UseApiLocalization` pair in `Shared.Api` (the logging and health
  precedent), seated before authentication because the culture is a fact about the request, not
  the caller. Stateless and standard — the API parses no cookie that is not its own. The BFF
  closes the two channels between them: a YARP request transform rewrites `Accept-Language` on
  forwarded traffic from the culture it resolved, and a `CultureForwardingHandler` does the same
  on the named client its own endpoints and the prerendering read clients use. Three legs, one
  resolution.
- **No language flash: the document carries the answer, and the boot reads it back.** App.razor
  stamps `<html lang="...">` from the resolved culture on every route — the honest label and the
  handoff. The prerendered pass renders localized words server-side (the host calls
  `AddLocalization` over the same satellites the browser ships). The WebAssembly boot reads the
  attribute with one interop call, sets both thread cultures, and `RunAsync` starts an
  application already speaking the page's language: same source on both passes, so there is
  nothing to correct. Changing language sets the cookie and reloads (`forceLoad`), because
  satellites and ICU data load per boot — a re-render would translate whatever happened to
  re-render.
- **Consumption is the framework's own:** `@inject IStringLocalizer<CommonResources> L`,
  `@L["Catalog"]`. No custom translation service, no dictionary, no switch. The first consumed
  keys — the bar's catalog label and the selector's own name — are the living proof the chain
  works end to end; **localizing every surface is explicitly not this record's scope.**
- **The domain error catalog is keyed by the domain's codes.** `DomainErrorResources` maps
  `Training.DuplicateTitle` to a sentence per language, the neutral entry restating the domain's
  own English verbatim. The wire shape of ADR 0004/0012 does not move: `errors` and
  `domainErrors` keep their semantics, and `domainErrors[].errorMessage` remains the domain's
  sentence. The next phase's hook is the one funnel that already exists —
  `ProblemResultExtensions.Problem` looks `Detail` up by code and falls back to the domain's
  message when the catalog has no entry, always for the codes whose sentences interpolate runtime
  values, until a format-argument mechanism is decided.
- **Validation localizes by consequence, not by work.** FluentValidation's default templates
  resolve against the request culture natively and ship French and Russian, so wiring the
  middleware makes them answer in the visitor's language immediately — named plainly under
  *Consequences* as the accepted intermediate state. DataAnnotations stay English this phase; the
  path is `AddDataAnnotationsLocalization` over `ValidationResources`, whose founding key already
  carries the `{0}` template shape the house attributes use.
- **Addresses stay culture-independent.** Localized URLs (`/fr/catalog`) demand hreflang,
  per-culture sitemaps, canonical rewrites and per-culture prerendering — a routing redesign that
  is its own record the day multilingual SEO is wanted. Until then a crawler carries no cookie,
  receives English, and `<html lang>` says so honestly.

### What was turned down

- **`localStorage`, the theme's home (ADR 0077).** Unreadable before the first byte, so it
  guarantees the flash on exactly the pages that prerender. The theme accepts a corrected paint;
  a language cannot.
- **Resources inside `Shared.Api`.** Unreachable from the WebAssembly client without dragging the
  web framework into the browser.
- **JSON dictionaries and a custom translation service.** Rebuilding `IStringLocalizer`,
  satellite loading and fallback by hand, to arrive where the framework already stands.
- **A per-account preference.** Premature: devices are personal, the cookie survives sign-out,
  and an anonymous visitor — the catalog's main audience — has no account to store one in.
- **Letting the domain know the translations.** The challenge was made in ubiquitous-language
  terms and answered above: the language is singular, translations are presentations, and
  `TheDomain_ReferencesTheKernelAndNothingElse` already said most of this — the new rule extends
  it to every inner layer.
- **A custom culture header.** `Accept-Language` is the standard that already flows through every
  proxy and client on the path.

## Consequences

- **A French request now gets French FluentValidation sentences while annotations, Identity's
  own errors and the domain's sentences stay English until their phases land.** Named rather than
  hidden: the intermediate state is visible on the wire, and every English-pinned fact in the
  suites stays green because no test sends `Accept-Language` and the default is English.
- **Every culture file carries exactly the neutral file's keys.** Fallback means a missing
  translation never shows a raw key — which is precisely why it would hide forever, so in a
  showcase the drift is a red build (`EveryCultureResource_CarriesExactlyTheDefaultsKeys`), in
  both directions, for every language the list offers and no language it does not.
- **The census learns the resx family.** The neutral files are English this repository writes, so
  the spelling rule reads them; `.fr.resx` and `.ru.resx` are declared unread by their compound
  extension — their words are not English at all — and their keys are governed through the
  key-set rule instead.
- **The WebAssembly payload grows** by the full ICU data and the satellite assemblies — the price
  of changing language without shipping a build per culture, paid once per cache. Changing
  language costs a full reload, stated on the selector itself.
- **The three-legs guarantee is executable.** The BFF suite proves the prerendered page carries
  the resolved `lang` and the localized words with no script having run; that the cookie beats
  the header; that `fr-BE` lands on `fr`; and that both channels to the API — the proxy and the
  named client — restate the resolution in `Accept-Language`.
- **Phase 2 has named seams instead of a rewrite:** the target families
  (`CatalogResources`, `TrainingResources`, `TrainerResources`, `AuthenticationResources`,
  `AdministrationResources`), the `Problem` lookup by code, and
  `AddDataAnnotationsLocalization`. On .NET 11, `UseCultureFromServer` replaces the boot's
  interop read; the cookie and the resolution chain stay as they are.

## Verification

- `NoInnerLayer_ReferencesTheTranslations` — red with a `ProjectReference` from
  `Shared.Infrastructure` to the translations, green on revert.
- `TheTranslations_DependOnNothing` — red with a `PackageReference` added to the project.
- `EveryCultureResource_CarriesExactlyTheDefaultsKeys` — red three ways at once: a key renamed in
  the French file (reported missing *and* left over), and a `de` file no supported language
  claims.
- `BothApiHosts_ResolveTheSameCulture` — red with `UseApiLocalization()` commented out of one
  host.
- The census closed over the new kind of file: with `.resx` removed from the written extensions,
  `EveryFileThisRepositoryHolds_IsEitherReadOrDeclaredUnread` named the three neutral files; with
  `.fr.resx` no longer declared unread, `EveryWordThisRepositoryWrites_UsesAmericanSpelling` bit
  on the French file's own words.
- The BFF facts listed under *Consequences*, plus the cookie's shape (a year, `HttpOnly`,
  `Secure`, `Lax`) and the refusal of a language the list does not offer; the client facts prove
  the selector's offer in each language's own words, the recorded choice with its `forceLoad`
  reload, and the resource lookups per language through the real factory.
