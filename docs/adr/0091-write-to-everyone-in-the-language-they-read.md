# 0091 — Write to everyone in the language they read

- **Status:** Accepted
- **Amends:** 0090
- **Date:** 2026-08-18

## Context

ADR 0088 resolved the visitor's culture at the door and ADR 0089 made every surface read its words
from a resource family, so the screens answer in English, French or Russian. The emails did not
follow. ADR 0090 built the mechanism for one of them — a composer port, an adapter in the shared
infrastructure's `Email` namespace, the `NotificationResources` family in three languages — and
recorded, in as many words, that the nine notices already in place stayed English because their
facts carried no culture.

The gap is exactly measurable. Ten consumers send an email; one is composed from the translations.
Nine take `IEmailSender` alone and build their subject and body from string literals. One
integration event carries a culture, `EmailVerificationRequestedIntegrationEvent`, and it carries
the culture of the request that caused it.

That last detail is why finishing the work is not a matter of copying the culture onto nine more
facts. The ten notices split in two, and only one half is served by the requester's culture:

- **Five are addressed to the person who asked** — the welcome, the warning to a contact address a
  trainer just left, the erasure receipt, the password-changed notice, the reset link. Whoever
  asked is whoever reads.
- **Four are addressed to somebody else entirely** — suspension, reinstatement and withholding are
  caused by an administrator and read by a trainer; a contact message is written by a visitor and
  read by a trainer. Carrying the requester's culture would write to a Russian-speaking trainer in
  the administrator's English, or worse, in a language they chose for their own session that day.
  That is not an improvement on English: English claims nothing, while a wrong language claims to
  know.

The screens have a second, quieter gap of the same kind. Twenty-five components read their words
from a family, and nothing makes them. `EveryKeyAScreenAsks_ExistsInItsFamily` holds a key a screen
asks against its family's neutral file, so a screen that asks for nothing satisfies it perfectly.
One `<MudText>Saved</MudText>` would have shipped English to a French visitor with no build, no
request and no test saying so. Four such words are in the tree today: the sixteen topic names on
four screens, the framework's scaffolded error page, the document title and description of every
non-catalog route, and the refusal `/bff/culture` answers.

## Decision

**An email is written in the language of the person who opens it, and that language is read from
the same source as their address.**

- **The language becomes a fact about the account, stored beside it.** Written at registration from
  the culture ADR 0088 resolved for that request, and rewritten whenever a signed-in visitor
  changes language in the selector — which is where a person looks for it, so the profile form
  gains no duplicate field. It is the account's preference and not the session's: a trainer who
  reads one page in English while abroad has not asked for their suspension notice in English.

- **It is a guest of the Identity store, in the mold the two credentials already use.**
  `AccountLanguage` is one table keyed by `UserId`, cascading from `AspNetUsers`, configured by a
  static class the Identity context applies by name. Not a column of the `Trainer` aggregate: a
  language preference settles no business rule, and the domain may not name the resource assembly
  that says which languages exist (ADR 0088). Not a derived `ApplicationUser` either: that would
  propagate a generic argument through three hosts, `UserManager`, the token service, the seeder
  and the test kit, to add one column.

- **The language travels with the address, never with the fact of what happened.** Each consumer
  reads it wherever it already resolves its recipient — off the integration event when the address
  rides on the event, off the invitation when the token store just minted one, off the read port
  when a port answered who to tell. `EmailVerificationRequestedIntegrationEvent` therefore *loses*
  its `Culture`: its address comes from the invitation, so its language does too, and one authority
  is better than two that can disagree.

- **One composer, ten notices.** `IVerificationEmailComposer` becomes `INotificationComposer`, one
  method per notice, answering the same `Notification(Subject, Body)` pair. Ten ports would have
  been thirty types for one concept. The adapter keeps ADR 0090's mechanics unchanged — the culture
  pinned around the reads and restored in a `finally`, because the delivery worker's thread serves
  every consumer in turn — and stays in the `Email` namespace the fence admits.

- **The subjects become words as well as values.** A new `TopicResources` family names the sixteen
  topics in three languages. The names themselves stay English everywhere they are a value rather
  than a word: the filter's query string, the contract's `[KnownTopic]`, the rows of the search
  index. What a visitor reads and what a request carries are two different things on purpose.

- **A hard-coded word on a screen becomes a red build.** `NoScreen_ShowsAWordItDidNotAskFor` reads
  the markup of every component — the `@code` block, the directives, the comments, the scripts and
  the Razor transitions removed first — and refuses a literal between the tags. The brand is the
  one exemption, and it is exempt because a product name is not a word.

## Consequences

- **A payload changes shape.** Four integration events gain a `Language`, and the verification
  event loses its `Culture`. An outbox row written before the deployment and delivered after it
  fails to deserialize into the new record. On this repository that window is a `docker compose up`
  and the retention is fourteen days; on a system where it mattered, the answer would be to drain
  the outbox before deploying, and it is named here rather than discovered later.

- **Every notice now costs a language lookup.** Six of the ten already query a port to resolve
  their recipient and pay nothing extra — the column rides along in the projection they already
  build. Two take it off the invitation the store just minted, also free. Two read it through a
  port of their own at publish time, which is one indexed read on a primary key, on the write side,
  outside any request a visitor is waiting on.

- **The default is still English, and now it is a decision rather than an accident.** An account
  with no row, a language code the framework does not know, a fact from before this change: all
  three resolve to `SupportedLanguages.Default`, and a delivery is never poisoned by a culture it
  cannot parse.

- **Adding a language costs more than it did.** `EveryCultureResource_CarriesExactlyTheDefaultsKeys`
  already demanded a full translation of every family; there are now ten families and roughly forty
  more keys. That is the price of the promise, and the alternative — a partial translation the
  framework silently backfills with English — is the failure this repository decided to make loud.

## Alternatives considered

- **Carry the requester's culture on every fact.** The cheapest change: nine one-line additions,
  no table, no port, no migration. Rejected because it is wrong for four of the ten notices in a
  way no test would catch and every recipient would notice — the sanction arrives in the language
  of the person who imposed it.

- **Put the language on the `Trainer` aggregate.** Cheaper than a new table, and the profile edit
  flow already exists to change it. Rejected twice over: the preference is a fact about the account
  and not about the trainer — the erasure receipt and the reset link go to accounts, and one of
  them goes to an account whose trainer is already gone — and a domain that stored a language would
  have to know which languages exist, which is exactly the reference ADR 0088 forbids it.

- **Derive `ApplicationUser : IdentityUser<Guid>` and add a column.** The framework's own answer,
  and the one a reader expects. Rejected for its blast radius: the generic argument appears in
  three composition roots, the token service, the claims enricher, the development seeder, both
  token stores and the test kit, and every one of them would change to carry a preference that has
  nothing to do with identity.

- **Ten composer ports, one per notice.** Faithful to ADR 0090's precedent and to the one-file-per-
  use-case habit. Rejected as ceremony: thirty types, thirty registrations and one concept. The
  fat port is honest about what it is — the presenter of this application's outbound prose — and
  the rule that every consumer composes through it is what a reader actually needs.

- **Translate the topics at the boundary, as a display name on the contract.** It would have kept
  the front end free of a resource family. Rejected because the catalog's topic is a filter value
  that round-trips through the URL and the search index: a localized contract would either carry
  both forms or force the client to send back a translated word, and both are worse than one lookup
  on a screen.

## Verification

- `LocalizationRules` gains four rules, each proved red against the tree before the code that
  satisfies it: `NoNotice_ComposesItsOwnProse` found the nine consumers writing their own subject
  and body; `EveryNotice_ReadsItsLanguageWhereItReadsItsAddress` found all ten reading no language
  at all; `EveryTopic_IsNamedByTheTranslations` found no topic family; and
  `NoScreen_ShowsAWordItDidNotAskFor` found the seventeen literals of the scaffolded error page.
- `NotificationComposerTests` proves the ten notices in the three languages, that the announced
  reset window is the one the store stamped, that a visitor's own words cross a translated notice
  untouched, and that the thread's culture is the one it started with whichever way a composition
  ends.
- `AccountLanguageStoreTests` proves the preference against SQLite: one row per account whatever the
  number of choices, the silence an account that never chose answers with, and the cascade that
  takes the row out with the account.
- `AdministrativeNoticeTest` in the shared TestKit proves the decision end to end on both hosts,
  through real SQL Server and real SMTP: an English-speaking administrator suspends a trainer who
  registered in Russian, and the notice that arrives is in Russian with the administrator's own
  words quoted as written.
- The consumer suites hold the routing rather than the prose — each asks the composer in the
  language it read beside the address, and mails back exactly what came out.
