# 0059 — Give the search index a body, and a query surface

- **Status:** Accepted — amended by [0069](0069-give-the-catalog-its-first-facet.md): the index gains its first non-title dimension — the topics a training declares, served as the catalog's facets
- **Amends:** [0055](0055-let-the-administration-read-what-the-catalogue-may-not.md)
- **Date:** 2026-08-09

**What this changes in 0055.** That record names one destination for two searches: *"the same
destination settles both — a training search belongs to the Search Indexing context"*. It settles
one. The index composes **public** visibility, and an administrator looks for exactly what public
visibility excludes, so the sentence narrows to the public half and the administration's missing
`?search=` stays open with a reason it did not have. Everything else in 0055 stands: the two
listings, the line at named criteria, the `LIKE` on the trainers' names — and the trigger it named,
which this record meets.

## Context

ADR 0055 refused this context a query surface, and said exactly what would change its mind:

> **Delegate search to the Search Indexing context now.** The right destination, and premature. That
> context is a fake with one consumer; giving it a query surface before it has an index is building
> the second system to avoid a `LIKE`. What this record does instead is **name the trigger**.

Since then the context has grown everywhere except where it counts. Its port carries four
operations, nine post-commit consumers feed it through the outbox, ADR 0050 gave it the removal and
ADR 0056 made it compose public visibility out of two aggregates that store it nowhere. Behind all
of that: `FakeTrainingSearchIndexer`, four log lines, nothing kept.

It is the one place in this repository where a decision is written, defended, tested — and absent.
ADR 0023 marks it on the map as unimplemented precisely so that the map does not lie, and ADR 0031
left a note that the port would move out of the kernel *"the day it grows a real adapter"*.

Two facts decide the shape of what follows, and neither was obvious before the inventory:

- **The port carries two identifiers and no document.** `IndexAsync(Guid trainingId, Guid trainerId)`
  is enough to *say* a training changed and not enough to *index* one.
- **The index cannot serve the administration.** `RemoveAsync` takes a withdrawn or withheld
  training out; `HideTrainerCatalogueAsync` takes a suspended trainer's catalogue out. A moderator
  looks for those. ADR 0055's promise of one destination for both searches cannot be kept.

## Decision

**The Search Indexing context gets a real index — an inverted one, in this database, written by one
adapter — and the query surface ADR 0055 made conditional on it.**

- **Two tables it owns.** `TrainingSearchEntry` holds one row per training: the title as a visitor
  reads it, the trainer it is filed under, and public visibility stored as the two facts it is
  composed of, `IsPublished` and `IsTrainerHidden`. `TrainingSearchTerm` holds one row per word of
  that title, keyed by the pair, indexed **term first**.
- **The second table is what makes this an index rather than a copy.** A title kept whole can only
  be searched with a leading wildcard, which no index can seek — the cost ADR 0055 recorded and paid
  on the trainers' listing. Split into tokens and indexed by token, each word of a search is a range
  seek. A training answers when its title matches **every** word, each by prefix: two words are a
  narrower question than one, and a search that widened as the caller typed would get worse the more
  it was told.
- **Tokens are upper-cased and bounded.** Upper because lower-casing is not a lossless round trip in
  every culture; bounded at ten words on both sides, because a hundred-character title can hold
  fifty and a fifty-way join answers a question nobody asked. A word is a run of letters or digits,
  which separates on the punctuation of every alphabet without a list to keep.
- **The adapter reads the document back.** The port speaks two identifiers, so the title and the two
  visibility facts are read from the write model, once, for the one training the fact just spoke
  about. That is what a projection does. It is **not** the rebuild ADR 0056 rejected — that one was
  a read per training for a sanction that wrote to none of them, and the sanction below still costs
  one statement.
- **The sanction stays one statement each way.** `Hide` and `Show` are a single `ExecuteUpdate` over
  the trainer's rows, leaving each training's own publication untouched, which is what ADR 0056
  promised a real engine would do. Storing the two halves separately rather than their conjunction
  is what buys it.
- **Every operation stays safe to run twice**, because every caller is an outbox consumer reading a
  committed fact that a lapsed lease may deliver again (ADR 0025, ADR 0034). Indexing a training the
  write model no longer holds writes nothing rather than racing it back into the catalogue.
- **The query surface is one anonymous endpoint on each host**: `GET /Catalogue/trainings?term=`,
  paged under ADR 0029's published cap, ordered by title then identifier. A blank term is no term
  and answers the offered catalogue — the reading ADR 0055 already gives one.
- **Both stacks read through one port.** `ITrainingSearchQuery` is the whole read half of this
  context's published language, and the layered service and the CQRS handler both call it. What
  usually separates the two hosts is how each drives the write model; over a read model there is
  nothing to separate, and a second reading of the same rows to make the halves look different would
  be duplication (ADR 0049).
- **The port moves out of the kernel**, to `Shared.Application/Search/`, beside the query half. That
  is ADR 0031's own instruction, deferred until today by name.
- **The trainers' `LIKE` is reconsidered, and kept.** This context indexes trainings for the public.
  The administrative listing of trainers is a read of the write model, over a bounded population,
  reserved to one authority — indexing people so that an administrator can find them would be a
  second system for the wrong reason. What ADR 0055 promised was the destination for a *training*
  search, and this is where that promise is kept.

## Consequences

- **Search Indexing stops being a port and becomes a context that is built.** The map's node leaves
  the *declared as a port* subgraph, its table cell and its section's status change with it, and the
  three claims move in this commit or `EveryContextOnTheMap_AgreesWithItsSectionsStatus` fails —
  which is the rule doing its job.
- **The "remain fakes that write to the log" sentences of ADR 0024 and ADR 0025 are now dated in
  full**, and ADR 0031's *"the search indexer half stays true"* with them. None of the three is
  amended: each predicted this and said what would close it, and ADR 0057 already settled that a
  record fulfilling a prediction does not contradict the one that made it.
- **The catalogue is eventually consistent, and visibly so.** A training becomes findable when the
  delivery worker gets to its fact, not when the request returns. That is what the outbox has always
  meant here; it is the first time a user-facing read shows it.
- **`AddSingleton` becomes `AddScoped`.** The adapter holds the session that writes the index.
- **Twenty-two endpoints, and the twenty-second needs no token.** A fourth controller base carries
  it, because a policy on an action is added to its controller's rather than replacing it — the trap
  ADR 0054 named, arriving from the other direction.
- **`/Administration/trainings` still has no `?search=`**, and now says why twice over: the title is
  value-converted (ADR 0055), and the index that could match it holds none of the states a moderator
  is looking for. Closing it means remapping the column the uniqueness index sits on, which is still
  a decision of its own.
- **Nothing renders this.** A catalogue page belongs to Catalogue Discovery, which still does not
  exist. What exists here is the query half of this context's published language, which is what
  ADR 0055 called a query surface.

## Alternatives considered

**Put a search engine behind the port.** Elasticsearch or Azure AI Search, which is what the fake's
own remark predicted. Rejected for a showcase: it would move the interesting part — what an index
*is* — into somebody else's product, and add a container, a package and an availability failure mode
to a repository whose point is that its decisions are legible. The port speaks primitives precisely
so this stays a one-line change the day it is worth making.

**Full-text search in SQL Server.** Free of a new dependency on paper, and not free in fact: the
component's presence in the container image is not asserted anywhere here, a catalogue is DDL that
no migration in this repository writes, and the behaviour would differ between the integration
suites and every developer machine. A table of words is the same idea, written down.

**Grow the facts so they carry the title.** Then no read-back. It costs a version bump on three
events (ADR 0024), a dispatcher change, the two closed-set tests — and `TrainingTransferred` carries
no title anyway, so the read-back would survive for the one fact that changes an entry's owner.

**Store one `IsOffered` column instead of two.** Smaller, and it loses the sanction's whole
argument: lifting a suspension would have to know which trainings were published before it, which is
the catalogue read-back ADR 0056 refused.

**Let the administration read the index too.** One reader for both audiences. It cannot work: the
index is defined by what it excludes, and a moderator's question is *show me what nobody can see*.

**Search substrings rather than prefixes.** Friendlier, and it gives back the sequential scan this
record exists to remove. A prefix per word is what an index can answer.

## Verification

- **`NoLayerAboveTheIndex_NamesTheTablesItIsMadeOf`** — the storage is the context's own; everything
  else reaches it through the two ports. Watched failing first, from a CQRS query handler naming the
  entry type, which is exactly the shortcut every other handler on that host makes legitimately.
- **`NoCatalogueSearch_ReadsTheWriteModel`** — the read path names no aggregate collection and no
  repository. Watched failing first, from the index's own reader touching `Trainings`.
- **The adapter and the reader against a database**, not a substitute: the cascade that takes a
  training's tokens with it, the single statement that flips a whole catalogue without forgetting
  what was published, the convergence of a re-delivered fact, and the composed match that answers
  only what carries every word.
- **Shared facts in `tests/TrainingHub.Api.TestKit/`**, so both hosts answer them end to end: a
  caller with no token finds a published training by one word of its title, stops finding it when
  the owner withdraws it, and stops finding the whole catalogue while its owner is suspended —
  finding all of it again once the sanction is lifted.
- **The three consumers that had no unit test** — publication, withdrawal, deletion — have one now.
  They went untested for as long as a wrong call was a wrong log line.
