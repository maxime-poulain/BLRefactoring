# 0089 — Localize every surface, and translate the refusal at its funnel

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

ADR 0088 built the foundation and drew its own limit: the culture is resolved once and travels
every leg, the words live in `TrainingHub.Translations`, and exactly two keys were consumed — the
bar's catalog label and the selector's own name. Everything else still spoke English to a visitor
whose page already said `lang="fr"`: every screen's labels, prompts and snackbars; every
`domainErrors[].errorMessage` on the wire; every data annotation template. The record named the
seams this phase would land on — five per-surface families, the `Problem` lookup by code,
`AddDataAnnotationsLocalization` — so the work here is filling named seams, not designing new
ones.

One invariant makes the fill safe, and it is worth stating before the decisions because every one
of them bends to it: **the English output must not move, byte for byte.** Dozens of facts across
the suites pin English sentences — bUnit markup, TestKit problem documents, application-layer
messages — and none of them sends a culture, so English is what they read. Every neutral resource
entry therefore restates today's sentence verbatim, and every mechanism is chosen so that a
lookup that hits and a lookup that never ran answer the same English. A localization whose
English side is a diff is a localization nobody can review.

Filling the seams surfaced facts the plan could not see from above:

- **A code is only translatable when it names one sentence.** Extracting every
  `Failure(code, "sentence")` site found codes raised with *different* sentences on different
  paths — `Training.DuplicateTitle` says one thing on publish and another on transfer,
  `Training.TrainerSuspended` three things, `Trainer.InvalidEmail` and `Training.Withheld` two
  each. A catalog entry for such a code would overwrite the path's own sentence with the other
  path's translation. And many sentences interpolate runtime values — a byte count, an
  identifier — which no static resource entry can restate.
- **English grammar was load-bearing.** Sentences were assembled from fragments in ways only
  English survives: a past participle spliced into `$"The {subject} could not be {verb}."`, a
  plural built by appending `s`, a name concatenated after a phrase. Fragments cannot be
  translated; only sentences can.
- **Some words on a screen are not the interface's words.** A suspension reason and a
  withholding reason are an administrator's own sentence, shown as written (ADR 0052,
  ADR 0057). A topic name is the domain's canonical spelling, stored in the index and carried in
  addresses (ADR 0069). Translating either would be this product paraphrasing somebody else's
  words — or renaming its own data.

## Decision

**Every surface reads its words from a per-surface family, in whole sentences; the problem
funnel translates each refusal it answers, by code, entry by entry; the data annotation
templates resolve against the validation family by their own English text. Wherever the catalog
does not know a code, the domain's authored sentence passes through untouched — which is also
what keeps English byte-identical everywhere.**

- **Five families, one per surface, plus the shared shell.** `CatalogResources`,
  `TrainingResources`, `TrainerResources`, `AuthenticationResources`,
  `AdministrationResources` — the families ADR 0088 named — each a marker beside its
  `en`/`fr`/`ru` trio, mirroring the surfaces the README describes. `CommonResources` holds what
  crosses surfaces: the shell (menu entries, the theme and language controls, the lost-visitor
  pages, the suspension banner), the shared verbs (`Cancel`), the shared form labels, and the
  sentences every surface's error handling shares (`RequestRejected`, `NoLongerAllowed`,
  `SuspendedCannotChange`). A string appearing on one surface lives in that surface's family
  even when another family carries the same English — the families are independent vocabularies,
  and a shared entry would couple two surfaces' wording forever.
- **The funnel translates per entry, and the code never moves.** The sink overload of
  `ProblemResultExtensions.Problem` — the one place every business refusal already passes
  (ADR 0004) — looks each `domainErrors[]` entry's `errorCode` up in `DomainErrorResources`,
  replaces the `errorMessage` when the catalog answers, and keeps the authored sentence when it
  does not. `Detail` follows the first entry, translated or not. This deliberately extends what
  ADR 0088 pinned: `errorMessage` is no longer always the domain's sentence — it is the domain's
  sentence *presented in the resolved culture* where the catalog knows the code, and the
  domain's own words everywhere else. The code stays the stable contract a client branches on.
- **The catalog holds exactly the codes the domain raises with one non-interpolated sentence** —
  thirteen today. A key nothing raises is a translation of nothing, refused by rule. The
  ambiguous codes stay out deliberately: entering them would rewrite one path's sentence with
  another's, and the honest fix — one code per sentence, each aggregate splitting its overloaded
  codes — is domain work for its own day. Interpolated sentences stay out until a
  format-argument mechanism is decided, exactly as ADR 0088 deferred it; they fall back, in
  every language, to the domain's authored English.
- **The annotations resolve by their own text.** `AddApiValidationLocalization` — one
  `IMvcBuilder` extension in `Shared.Api`, called from both hosts' `AddControllers()` chain —
  points the framework's `DataAnnotationLocalizerProvider` at `ValidationResources`, whose keys
  are the four house attributes' English templates themselves. The template is the key: a hit
  answers the satellite's sentence, a miss answers the template, and English is identical either
  way. The family holds exactly those four templates, for the catalog's reason.
- **Sentences, never fragments.** Where data joins a sentence, the entry is a `{0}` template and
  the localizer formats it (`ContactName`, `PhotoTooLarge`, `BetweenCharacters`). Where grammar
  was composed, the composition is gone: `AdministrationFailures` takes an `AdministrationCopy`
  of whole localized sentences instead of a verb and a subject, the trainings page's
  `RunTransition` takes its closing sentences whole, and `Report` takes the retry sentence
  already written rather than appending advice to a fragment. Where a count changes the
  sentence's shape, there are two entries (`TrainingsOfferedOne`/`TrainingsOfferedMany`) —
  three languages pluralize three ways, and a resource entry holds one sentence. Where a
  sentence flows around emphasized markup — a name, a title — it is split at the emphasis and
  each half is its own entry, the halves carrying their own punctuation so each language quotes
  the way it quotes; the pattern holds for the three supported languages because the emphasized
  name ends the leading half, and a language it fails is a redesign of that dialog's line, not
  of the mechanism.
- **Some words deliberately stay as their author wrote them.** Sanction reasons render verbatim
  in whatever language the administrator typed. Topic names render in the domain's canonical
  spelling — they are data with an address, and a multilingual catalog taxonomy is the
  multilingual-*data* record ADR 0088 already pointed at. Protocol values (`"Published"`,
  `"Withheld"`) keep their wire spelling; only the displayed badges and filters translate. The
  brand and the JSON-LD vocabulary stay as they are.

### What was turned down

- **Translating `Detail` alone and leaving the entries English.** The entries are what a client
  renders per field and what the Blazor pages read out; a translated summary above untranslated
  sentences is two languages in one document.
- **Entering the ambiguous codes with one chosen sentence.** It would silently rewrite the other
  path's meaning — the transfer refusal reworded as the publish refusal. The catalog stays
  honest and smaller instead.
- **A `PluralResources` mechanism or ICU message format.** Two entries carry the one plural this
  interface has; a message-format dependency is a decision to take when the count of plurals
  justifies it.
- **Localizing the framework's own English** — Identity's errors, the framework's default
  annotation templates, MudBlazor's built-in strings (a data grid's paging label), the server
  host's template `Error` page. Each is authored outside this repository; each is a named gap
  below rather than a hidden one.

## Consequences

- **A French or Russian visitor now reads every screen, every interface refusal and every
  cataloged domain refusal in their language.** What still answers English, named: the domain
  sentences the catalog cannot hold (interpolated, and the ambiguous codes until their split),
  Identity's account errors, the five FluentValidation `.WithMessage` customs (the application
  layer cannot reference the translations — `NoInnerLayer_ReferencesTheTranslations` — so those
  sentences wait on the same funnel treatment the day their codes are catalogable), the
  framework's own default annotation templates, MudBlazor's internals, and the server host's
  template error page.
- **The English wire and the English screens are byte-identical to what they were.** Every
  English-pinned fact across ten suites passed unchanged; the neutral entries restate the
  authored sentences verbatim, and every fallback answers the authored sentence.
- **The catalog is executable, in both directions.** Its keys must be codes the domain raises
  (`EveryDomainErrorKey_IsACodeTheDomainRaises`), and its cultures must carry exactly the
  neutral keys (ADR 0088's rule, now over six families).
- **`TrainerStanding.WhyDisabled` is gone**: the one sentence every disabled control shows lives
  in `CommonResources` now, its write-once property carried by the key rather than a constant.
- **Dialog helpers take their title from the page** (`ContactTrainerDialogs`,
  `EraseAccountDialogs`), because a static helper has no localizer and the page composing
  `Contact {0}` already does.
- **A new screen is not done in English.** Its words are entries in its surface's family, in the
  three languages, from the first commit — the key-set rule makes a missing translation a red
  build rather than a review comment.

## Verification

- `EveryDomainErrorKey_IsACodeTheDomainRaises` — red with a `Trainer.Nonexistent` entry planted
  in the neutral catalog, green on revert.
- `EveryKeyAScreenAsks_ExistsInItsFamily` — the read side of the same claim, and the compile-time
  safety this record declined to buy from generated designer classes: every literal key a source
  file asks of a localizer must exist in that family's neutral file. Red with `@L["Catalog"]`
  mistyped to `Catalogg` in the layout, naming the file, the family and the key; green on
  revert. Expression-keyed lookups — the funnel's — carry no literal and are held by the code
  rule instead.
- `BothApiHosts_LocalizeTheirAnnotations` — born red, before either host called
  `AddApiValidationLocalization`, green once both did.
- `ProblemResultExtensionsTests` hold the funnel: a cataloged code answers the request's
  language with its code untouched, English reads the domain's sentence verbatim, an uncataloged
  code keeps the authored sentence, and every entry is answered rather than the first.
- `ErrorFormatTest.FrenchRequest_ReadsTheSameCode_WithTheRefusalTranslated` proves the whole
  pipeline on both hosts — `Accept-Language: fr` through the middleware to a French
  `domainErrors` entry (integration suites, Docker).
- `LocalizationExtensionsTests` hold the annotations bridge: the wired provider resolves a house
  template in French, and English reads the template verbatim, hit or miss alike.
- `NotFoundTests.Renders_InTheVisitorsLanguage_WhenTheResolvedCultureIsTheirs` — one rendered
  French screen, the render the per-language lookup facts cannot see.
- The client suite's two hundred sixty English-pinned facts passed unchanged after every surface
  was rewired — the byte-identity invariant, measured.
