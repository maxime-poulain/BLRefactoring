# Context map

Who depends on whom, in which direction, and — the part most context maps leave out — **where the
seam is visible in the code**. A relationship you cannot point at is a relationship nobody is
keeping.

## The map

```mermaid
flowchart TB
    subgraph built ["Built"]
        TC["<b>Training Catalog</b><br/>core domain<br/>Trainer · Training"]
        IA["<b>Identity &amp; Access</b><br/>supporting · off the shelf"]
        MS["<b>Media Storage</b><br/>generic · S3 protocol"]
        NT["<b>Notification</b><br/>generic · SMTP"]
        SI["<b>Search Indexing</b><br/>generic · inverted index"]
    end

    subgraph planned ["Announced"]
        CD["<b>Catalog Discovery</b><br/>public read side"]
    end

    IA -- "upstream · ACL: UserId + ICurrentUserService" --> TC
    TC -- "outbox facts · delivery worker · EmailMessage" --> NT
    TC -- "outbox facts · delivery worker · Guid, Guid" --> SI
    TC -- "port: IObjectStore, no Replace" --> MS
    SI -. "feeds the read model" .-> CD
    TC -. "outbox facts" .-> CD
```

## Contexts on this map

| Context | Kind | State |
|---|---|---|
| Training Catalog | Core domain | Built |
| Identity & Access | Supporting | Built — off the shelf |
| Media Storage | Generic | Built |
| Notification | Generic | Built |
| Search Indexing | Generic | Built |
| Catalog Discovery | Core (read side) | Announced |

Each has its own section in [bounded-contexts.md](bounded-contexts.md), and an architecture rule
checks that the two lists agree.

---

## Identity & Access → Training Catalog

**Pattern:** customer/supplier, with an **anti-corruption layer**. Identity is upstream: it decides
what an account is, and the catalog adapts.

**Why an ACL and not conformity.** The catalog refuses to adopt Identity's model. It does not
store an `IdentityUser`, it does not treat the account's email as the trainer's contact address, and
it does not let a controller pass an account identifier where a trainer is expected. Conforming
would have been less code and would have produced the bug the code names out loud: *"conflating them
is what lets one caller edit another's work."*

**The seam, in three files:**

| File | What it does |
|---|---|
| `Shared.Domain/UserId.cs` | The only account-shaped thing the domain owns — an opaque typed identifier |
| `Shared/ICurrentUserService.cs` | Translates a request into **both** `UserId` and `TrainerId`, as separate properties |
| `Shared.Infrastructure/ThirdParty/Identity/TrainingIdentityDbContext.cs` | A `DbContext` of its own, with its own migration history |

**What it costs.** Two identifiers for one person, and a mapping to maintain. Bought in exchange for
being able to replace the authentication scheme without touching an aggregate.

---

## The one place both write sides commit together

Registration is the exception, and it is worth stating plainly rather than hiding behind the map.

`AuthControllerBase.RegisterAsync` opens a `TransactionScope` around **both** the creation of the
Identity account and the creation of the `Trainer`. Either both exist or neither does.

**Why this is acceptable here:** the two contexts share one physical database, so this is a local
transaction rather than a distributed one — no coordinator, no two-phase commit, no partial-failure
protocol.

**What it would cost to separate them:** the moment Identity moves to its own store, this becomes a
saga — create the account, then create the trainer, then compensate by deleting the account if the
second step fails. That is a real design, not a refactoring, and the code marks the spot: the
transaction is completed only when the whole registration succeeded.

**The rule this protects:** there is no such thing as an account without a trainer. `POST /Trainer`
does not exist; a trainer comes into being through registration or not at all.

---

## Training Catalog → Notification

**Pattern:** open host service with a **published language**, in miniature.

The catalog publishes facts; the notifier consumes them. The contract is `EmailMessage(Recipient,
Subject, Body)` — three strings, no domain type. The notifier cannot develop an opinion about
trainers because it is never shown one.

**The seam:** `Shared.Application/Notifications/IEmailSender.cs` — and, since the outbox landed, the facts that feed it:
`TrainerCreatedIntegrationEvent`, `TrainerContactEmailChangedIntegrationEvent`,
`TrainerSuspendedIntegrationEvent`, `TrainerReinstatedIntegrationEvent`,
`TrainingWithheldIntegrationEvent`, `TrainerContactedIntegrationEvent`,
`PasswordResetRequestedIntegrationEvent`, `EmailVerificationRequestedIntegrationEvent`,
`PasswordChangedIntegrationEvent` and
`AccountErasedIntegrationEvent`, committed to the
outbox by ten producers — six policies in `Shared.Application/EventHandlers/`, and four flows
whose endpoints commit their own fact, the contact message, the account recovery, the email
verification and the account's
erasure (ADR 0002, ADR 0024, ADR 0056, ADR 0082, ADR 0084, ADR 0085, ADR 0090).
A second port sits beside the sender for the three sanction notices, `ITrainerAccountQuery` —
one of the three read ports in `Shared.Application/Queries/`, and the only one that opens two
stores. Those notices are addressed to the account rather than to the published contact address,
and it answers where that is when the notice is sent rather than when the decision committed.

**State:** real, since [ADR 0031](../adr/0031-send-email-over-smtp-and-prove-it-against-a-real-server.md):
the facts land durably with the changes that justify them, the delivery worker (ADR 0025)
hands them to the policies that build the `EmailMessage`s, and a MailKit adapter sends each one
over SMTP — to a Mailpit container locally, to whatever relay the `Smtp` section names elsewhere.
The words themselves are nobody's policy: a consumer asks `INotificationComposer`, whose adapter
sits beside the sender and reads the translations, and it asks in the recipient's own language —
read wherever that consumer reads their address, and never taken from whoever caused the notice
([ADR 0091](../adr/0091-write-to-everyone-in-the-language-they-read.md)).
Choosing a provider was, as promised, a registration in the composition root; the integration
suites read the delivered messages back out of the real server.

---

## Training Catalog → Search Indexing

**Pattern:** open host service with a published language, and the clearest example of one here.

`IndexAsync(Guid trainingId, Guid trainerId)` takes **primitives**. The port's own remark explains
the decision: *"the search engine sitting behind it knows nothing about the domain's typed
identifiers."* Passing `TrainingId` would have made the index a downstream *conformist* of the
domain model; passing `Guid` keeps the contract translatable.

The language now has a read half as well, `ITrainingSearchQuery`, which
[ADR 0055](../adr/0055-let-the-administration-read-what-the-catalog-may-not.md) refused to open
until there was an index behind it and
[ADR 0059](../adr/0059-give-the-search-index-a-body-and-a-query-surface.md) opens now that there is.

**The seam:** `Shared.Application/Search/ITrainingSearchIndexer.cs` — and the facts that feed it:
`TrainingCreatedIntegrationEvent`, `TrainingEditedIntegrationEvent`,
`TrainingTransferredIntegrationEvent`, `TrainingPublishedIntegrationEvent`,
`TrainingUnpublishedIntegrationEvent`, `TrainingDeletedIntegrationEvent`,
`TrainingWithheldIntegrationEvent`, `TrainerSuspendedIntegrationEvent` and
`TrainerReinstatedIntegrationEvent`, committed to the outbox
by nine policies (ADR 0002, ADR 0024, ADR 0056). The port answers four operations, not one:
`RemoveAsync` is what a withdrawal, a deletion and a withholding call, and its absence is what used
to leave a deleted training in the index for ever (ADR 0050); `HideTrainerCatalogAsync` and
`ShowTrainerCatalogAsync` are what a sanction calls, one call about a trainer rather than one per
training, because a suspension writes to none of them.

**State:** built (ADR 0059). Two tables of this database — one entry per training, one row per word
of its title — written by a single adapter and read through the port's query half. The write side is
unchanged: the delivery worker (ADR 0025) replays the committed facts into this port after each
commit, so the index only ever learns of trainings the database accepted, and it learns of them
eventually rather than at once. The port carries no document, so the adapter reads back the one
training a fact spoke about; a public visibility composed from two aggregates and stored nowhere on
the write side (ADR 0050, ADR 0056) is stored here, which is what a read model is for.

Its readers are the three anonymous endpoints of this API — `GET /Catalog/trainings`,
`GET /Catalog/trainings/{id}` and `GET /Catalog/topics` — with a screen above the first two since
ADR 0062, and the catalog's facet chips over the third since ADR 0069. The second reads this
index for one thing only — whether the training is on offer — and reads the write model for what it
says, which is the shape a title-only index leaves: a description copied here would go stale on the
next edit, and a trainer's name copied here would go stale on a rename no fact carries. Catalog
Discovery as a *context* — a store of its own, a language of its own — still does not exist,
though its first word now runs: the facets ADR 0069 counts from this index.

**Why a transfer is one of the nine.** Nothing about the training's content changes when it
changes hands, which is why it looks like an ownership detail and is not: the index is what a
public catalog would read, and the trainer a training is filed under is part of what that page
shows. A seam described as fed by *create and edit* is a seam that would serve the wrong author
([ADR 0036](../adr/0036-model-the-decision-that-has-no-home-as-a-domain-service.md)).

**Why withdrawal and deletion are two of the nine, and not one.** Both call `RemoveAsync`, and merging
them would look like a simplification. It would cost the index the only distinction that matters
downstream: a withdrawn training is one its owner can offer again, and a deleted one is gone. A
consumer that will one day do more than remove — retain a tombstone, keep a redirect, count what a
trainer withdrew — must not have to guess which happened
([ADR 0050](../adr/0050-retire-a-training-rather-than-delete-it.md)).

---

## Training Catalog → Media Storage

**Pattern:** generic subdomain behind a port, with a deliberately reduced interface.

Three operations — put, get, delete — and **no replace**. That absence carries the safety argument:
replacing a photo means writing a *new* key, committing the row that names it, then deleting what
was displaced. A `Replace` operation would read as one atomic step, would be implemented as an
overwrite, and would put that ordering out of reach of everyone who called it. See
[ADR 0021](../adr/0021-store-a-photo-beside-the-row-that-names-it.md).

**The seam, in layers:**

| Layer | Speaks |
|---|---|
| `Trainer.Photo` | A `TrainerPhoto` — an identity, a media type, a size. Never an address |
| `ITrainerPhotoStore` | Trainers and photos |
| `IObjectStore` | Keys and bytes |
| `S3ObjectStore` | Buckets and endpoints |

The key layout — `trainers/{trainerId}/{photoId}` — belongs to the third layer and to nothing above
it. An architecture rule holds the claim that exactly one project in the solution has ever heard of
the AWS SDK.

The port's newest caller is the only post-commit one: the erasure's collector. Its fact,
`TrainerDeletedIntegrationEvent`, carries the photo's identity precisely because the rows that
could answer it are gone by delivery time, and the delivery worker deletes the bytes *after* the
transaction that removed the trainer has committed — the outbox retries what a cleanup between
commit and crash would orphan forever (ADR 0085).

---

## Training Catalog → Catalog Discovery *(announced)*

**Expected pattern:** downstream read model, fed by domain events, with its own storage and its own
language.

**Why it is on the map before it exists:** because three pieces of the current model were shaped for
it, and a reader who does not know that will read them as over-engineering.

| Already there | For |
|---|---|
| A real search index, maintained on every create, edit, transfer, publication and withdrawal, cleared on deletion, told a trainer's standing in one call, and readable through `ITrainingSearchQuery` (ADR 0059) | The search the public pages read (ADR 0062, ADR 0070), already answering at `GET /Catalog/trainings` |
| A public portrait at `GET /Catalog/trainings/{id}/photo/{photoId}`, whose address names a photo rather than a person and is therefore `immutable` by construction, serving only bytes the domain records as stripped of their metadata (ADR 0063) | A portrait served publicly, behind a CDN |
| A CQRS query side that projects into DTOs without loading aggregates | A read model that does not pay for the write model |

**What is not decided:** whether discovery gets its own store, or reads a projection of the same
database. The facts it would subscribe to are now durable — `TrainingCreatedIntegrationEvent`,
`TrainingEditedIntegrationEvent`, `TrainingTransferredIntegrationEvent`,
`TrainingPublishedIntegrationEvent`, `TrainingUnpublishedIntegrationEvent`,
`TrainingDeletedIntegrationEvent`, `TrainingWithheldIntegrationEvent`,
`TrainerSuspendedIntegrationEvent` and `TrainerReinstatedIntegrationEvent` land in the
transactional outbox with every commit (ADR 0024, ADR 0056) —
but the subscriber still does not exist. What ADR 0050 added to that list is the ability to express
what a public catalog must *not* show, and ADR 0056 the ability to express it about a trainer
rather than about each of their trainings — which is what makes this context buildable rather than
merely announced. ADR 0059 went one step further and built the index those facts maintain, so what
is missing here is no longer a store: it is whatever else a discovery experience turns out to be.
The facets arrived with ADR 0069 — each topic at least one matching training declares, counted
from this index, browsable at `GET /Catalog/topics`, several of them at a time and under the
visitor's own search since ADR 0080 — the trainer's public page with ADR 0070,
reachable from every training it offers, and the second order with ADR 0071, newest first over
the training's own age; none of them needed a store of its own, which is itself evidence that a
page over the same database is still the honest size of this context.

---

## How to read the direction of an arrow

Upstream is whoever can change without asking. In this map:

- **Identity is upstream of the catalog** — the framework's model changes on its own schedule, and
  the ACL absorbs it.
- **The catalog is upstream of everything else** — it publishes facts, and the consumers adapt.
  That is why every port it owns speaks the smallest possible vocabulary: a published language is
  only cheap to keep if there is little of it.
