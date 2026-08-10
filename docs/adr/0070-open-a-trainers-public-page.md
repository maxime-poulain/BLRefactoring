# 0070 — Open a trainer's public page

- **Status:** Accepted
- **Amends:** [0062](0062-let-the-proxy-forward-one-family-of-paths-without-a-token.md),
  [0063](0063-strip-the-metadata-before-the-bytes-are-stored.md)
- **Date:** 2026-08-10

## Context

Both strategic documents name the same missing word: *"what is missing here is no longer a store:
it is a trainer's public page, an ordering by anything other than a title, a store shaped by
reading rather than by writing."* ADR 0069 delivered the facets; the page a person exists on is
what this record makes real. A visitor reading a training today sees *Offered by Ada Lovelace* as
a dead label — the search row hands out `TrainerId` (it always has), the detail prints a name, and
neither resolves to anything. A person who publishes trainings has no public existence beyond each
training's page.

Two earlier positions stand in the way, and both were written before there was a page to point at.
ADR 0063 argued that *a trainer's identifier is not something a catalog hands out*, and the detail
contract repeated it — *nothing here hands out a way to address a person*. ADR 0055 withdrew the
anonymous read that listed trainers to anybody. The first is reversed here, deliberately and
narrowly; the second is not touched.

## Decision

**An offering trainer has a public page, and the navigation runs both ways.**

- **`GET /Catalog/trainers/{trainerId}`, on both hosts, anonymously.** One document: first name,
  last name, bio, the sanitized portrait's photo identity, and the offered trainings as catalog
  rows, alphabetically by the catalog's own order (ADR 0001, ADR 0029). The rows are the search's
  shape because that is what they are — ways of finding a training, each one navigation away from
  its reading. The list is bounded by the domain's quota rather than paged: it cannot outgrow one
  screen.
- **Offered or invisible.** The profile answers if and only if the index holds at least one entry
  for this trainer — published, its owner in good standing. One predicate, the index's own, so a
  person nobody registered, a suspended one and one with nothing published are the same 404, and
  the reader never touches the write model's states. This is ADR 0062's sharing of authority,
  extended to a person: visibility from the index, content — the name, the bio — from the write
  model, read at the moment of the request because no integration event carries a rename. The
  amendment to 0062 is exactly that the detail port gains the profile's reads; the composition of
  "on offer" still exists in one adapter, watched by the same rules.
- **The identifier now travels, and the old refusal narrows to what it always defended.**
  `CatalogTrainingDetailDto` and its contract gain `TrainerId`, so *Offered by X* becomes a link;
  the profile's rows link back to `/catalog/{id}`. What ADR 0063 defended was never the identifier
  itself — the search row already published it — but the absence of a page that would turn it into
  a person. Now that the page exists by decision, the identifier is its address. What stays
  refused is the *directory*: ADR 0055's withdrawn listing stays withdrawn, and
  `TheCatalog_NamesAPersonOnlyByIdentifier` pins it — every catalog route that says `trainers`
  says which trainer in the same breath, so a pageable `GET /Catalog/trainers` cannot reappear as
  a widening of the profile.
- **The portrait gains the profile's own address.**
  `GET /Catalog/trainers/{trainerId}/photo/{photoId}` serves the same sanitized bytes as the
  per-training address, under the same conditions (ADR 0063): the current photo, stamped, or 404.
  Two addresses rather than one redirected, because each page asks with what it has in hand, and
  both are cacheable forever because both name the photo.
- **The bio is the one new disclosure, and it is named here.** It was written by the trainer for
  their profile and never published before, because there was no profile. It leaves as the plain
  text the domain bounded at five hundred characters; nothing else the authenticated contracts
  carry — e-mail, status, reason — leaves with it.

## Consequences

- **The catalog's language gains its second word.** ADR 0069 made *facet* real; this makes *a
  trainer's public page* real. Catalog Discovery still owns no store and remains *announced* —
  what shrinks is its missing list.
- **No migration, no new fact.** The profile reads tables that exist: the index for visibility and
  the list, the trainer row for identity. The events stay two identifiers.
- **A profile is exactly as fresh as the catalog.** Same index, same outbox, same eventual
  consistency window — a suspension takes the page down when its consumer runs, and a
  reinstatement brings it back, with nothing new to keep consistent.
- **"Offered or invisible" costs a state on purpose.** An active trainer with only drafts has no
  public page, and cannot preview one. The alternative — answering 200 with an empty shelf —
  would need a second source of public visibility (the write model's standing), which is the one
  thing ADR 0062 forbids the reader to hold.

## Alternatives considered

**A profile for every active trainer, empty shelf included.** Reachable only by guessed URL —
nothing links to a person with nothing on offer — and it prices a second visibility predicate into
the one adapter that must not own one. The 404 also tells an anonymous caller less, which is what
ADR 0055 asks of every public refusal.

**A slug instead of the identifier.** Prettier addresses, and a second identity to mint, store,
uniquify and redirect on rename — for a page whose every inbound link is built by this
application from an identifier it already publishes on the search row.

**Paging the offered list.** `Training.MaximumPerTrainer` bounds it at ten by domain rule; a page
protocol around a list that cannot reach a second page is ceremony, and the paged envelope would
be the contract's only reason to change when the quota moves.

**A separate profile port.** `ICatalogTrainerQuery` beside `ICatalogDetailQuery`, symmetrical and
empty: same two authorities, same composition rule, same adapter file for the textual rules to
watch. A port earns its existence by holding a different decision, and this one would hold the
same one twice.

## Verification

- **`TheCatalog_NamesAPersonOnlyByIdentifier` watched failing** with the profile route temporarily
  rewritten as a listing (`trainers`), then restored.
- **SQLite facts** on the real schema: the profile composes identity, bio, stamped-portrait
  identity and the alphabetical offered list; answers nothing for a hidden trainer, an unpublished
  catalog, an unknown identifier; the per-person portrait serves the bytes and refuses the
  unstamped, the replaced and the lost.
- **End to end, on both hosts** (`CatalogTrainerProfileTest`, Docker-backed): the training's page
  hands out the address the profile answers; a suspension takes the page down and a reinstatement
  brings it back, through the outbox; withdrawing the last training makes the trainer as absent as
  a person who never existed; the portrait answers `immutable` at the profile's own address.
- **The screens**, in bUnit: *Offered by* links to the profile; the profile renders the name, the
  bio and one linked row per offered training, distinguishes "not available" from "unreachable",
  and renders no address the portrait endpoint would refuse.
