# Bounded contexts

What the system is about, cut into the pieces that have their own language — and the argument for
each cut. Everything below is read off the model as it stands; where a boundary is intended rather
than built, it says so.

## Subdomains

Not every part of a system deserves the same care. Distinguishing them is the first strategic act,
because it decides where effort goes.

| Subdomain | Kind | Why | Where it lives |
|---|---|---|---|
| Training Catalog | **Core** | The reason the system exists: a trainer describes what they teach. Every rule that is specific to this business is here. | `src/TrainingHub.Shared.Domain/` |
| Identity & Access | Supporting | Necessary, not distinctive. Bought rather than modeled — ASP.NET Core Identity, unmodified. | `Shared.Infrastructure/ThirdParty/Identity/` |
| Notification | Generic | Sending an email is the same problem for everybody. | `IEmailSender` in `Shared.Application/Notifications/`, a MailKit adapter behind it — see [ADR 0031](../adr/0031-send-email-over-smtp-and-prove-it-against-a-real-server.md) |
| Search Indexing | Generic | Keeping a read model in step with writes. | `ITrainingSearchIndexer` and `ITrainingSearchQuery`, over an inverted index |
| Media Storage | Generic | Bytes under a key. Solved by an industry protocol. | `IObjectStore`, S3 adapter — see [ADR 0021](../adr/0021-store-a-photo-beside-the-row-that-names-it.md) |

The core subdomain is the only one this repository models. The other four are consumed through
ports, which is what makes them replaceable and what keeps their vocabulary out of the domain.

## Why two contexts, and not four

`Trainer` and `Training` look like candidates for a boundary each. They are not, and the model says
so in three places:

1. **They share one transaction and one `DbContext`.** Both are mapped in `TrainingContext`, and a
   single `UnitOfWork` commits them together.
2. **One references the other directly.** `Training.TrainerId` is a typed identifier of the *same*
   model, not an identifier copied across a boundary.
3. **A change to one cascades inside the other's unit of work.**
   `DeleteTrainingWhenTrainerDeletedEventHandler` deletes trainings while the trainer's transaction
   is still open — the opposite of what crossing a boundary would allow.

Two aggregates that commit together, reference each other by type and cascade synchronously are two
aggregates in **one** context. Drawing a line between them would produce a diagram that no code
honors, which is the failure mode this document exists to avoid.

The line that *is* real runs between the domain and authentication, and the code states it out loud —
see the next two sections.

---

## Context — Training Catalog

**The core.** A trainer keeps a professional profile and publishes the trainings they teach.

### Responsibility

Own everything that is true about a trainer's public identity and about the trainings they offer:
what a training may be called, what it promises, what it requires, and who is allowed to change it.

### Ubiquitous language

Every term below is a type in `src/TrainingHub.Shared.Domain/`. The name in the document *is* the
name in the code; that is the point of a ubiquitous language.

| Term | What it means in this business | Rule it carries |
|---|---|---|
| **Trainer** | Someone who publishes trainings. Not an account — see *Identity & Access*. | Aggregate root; created only by registration |
| **Training** | A training a trainer offers. A catalog entry, not a scheduled event. | Aggregate root; belongs to exactly one trainer |
| **Name** | A trainer's `Firstname` and `Lastname` | Each 2 to 50 characters once trimmed; both refusals are reported together |
| **Email** | The address a trainer wishes to be contacted at, split into `LocalPart` and `Domain` | Must be a valid address. **Not unique** — see below |
| **Bio** | A trainer's own description of themselves | Optional; at most 500 characters, and blank is a refusal rather than an empty bio |
| **TrainerPhoto** | The portrait a trainer publishes | At most 5 MiB; PNG, JPEG or WebP, recognized by reading the bytes rather than trusting the caller |
| **TrainingTitle** | What a training is called | 5 to 100 characters, and unique **per trainer** |
| **TrainingDescription** | What the training covers | Required, at most 500 characters |
| **TrainingPrerequisites** | What a participant needs beforehand | Required, at most 500 characters |
| **AcquiredSkills** | What a participant leaves with | Required, at most 500 characters |
| **Topic** | What a training is filed under | A **closed set of sixteen**: Programming, Design, Marketing, Business, Personal Development, Leadership, Software Architecture, Cloud Computing, DevOps, Databases, Security, Web Development, Data and Analytics, Testing and Quality, Project Management, Agile Practices — each a subject, never a product (ADR 0079) |
| **TrainingStatus** | Whether a training is offered to the public, withdrawn by its owner, or kept back by the administration | `Published`, `Unpublished` or `Withheld`; born published, and only the owner's two states are the owner's to leave |
| **TrainerStatus** | Whether a trainer is in good standing or under sanction | `Active` or `Suspended`; the whole of a suspension is this one field and its reason |
| **WithholdingReason** | Why the administration kept a training back, in its own words | Required, at most 500 characters; present if and only if the training is `Withheld` |
| **SuspensionReason** | Why a trainer was suspended, in the administration's own words | Required, at most 500 characters; present if and only if the trainer is `Suspended` |
| **TrainingTransferDomainService** | Handing a training to another trainer, who becomes its owner | The recipient must be able to accept it; the giver keeps nothing |

Five entries in that table are worth pausing on, because each encodes a business decision rather
than a technical one.

**A contact address is not a login.** `Trainer.ContactEmail` carries no uniqueness rule, and the
aggregate says why: a trainer may publish a professional address different from the one their
account was opened with, and two trainers of the same organization may legitimately share one. The
account's email is unique; the contact address is not. They are different concepts that happen to
have the same shape.

**A title is unique per trainer, not globally.** Two trainers may both teach "Introduction to
Domain-Driven Design"; one trainer may not list it twice. The rule is the only one the aggregate
cannot answer alone, so it asks `IUniquenessTitleChecker` — a port, so the domain states the rule
without knowing how uniqueness is looked up.

**A transfer belongs to neither aggregate.** Handing a training over reads the *recipient's*
catalog in order to mutate the *giver's* training, and neither `Training` — which knows one owner
— nor `Trainer` — which holds no training — can decide it alone. It is the model's one recorded
domain service, and the only term in this table that is not a noun of the business:
`TrainingTransferDomainService`, static and stateless, deciding through the same two ports creation
uses ([ADR 0036](../adr/0036-model-the-decision-that-has-no-home-as-a-domain-service.md)).

**Visibility is composed, never stored.** A training is publicly visible when it is `Published`
**and** its trainer is `Active`. There is no third field holding the answer, and that is what makes
a suspension liftable: it writes one column on one aggregate and touches no training, so nothing
has to remember which trainings it hid — it hid none, and the catalog simply became invisible
with its owner. Cascading the sanction onto each training was considered and refused for exactly
this reason
([ADR 0050](../adr/0050-retire-a-training-rather-than-delete-it.md)).

### Aggregates

- `Trainer` — the profile, its contact address, its bio and its portrait.
- `Training` — the catalog entry, its content and its topics.

Each is an independent consistency boundary: `Training` names its owner by `TrainerId` and never
holds a `Trainer` instance.

### Invariants

- A training's title is unique among the trainings of the same trainer.
- A trainer publishes at most ten **published** trainings (`Training.MaximumPerTrainer`); the
  eleventh is refused, both at creation and when a withdrawn training is published again. A
  withdrawn training holds no place in the quota — otherwise ten of them would end a trainer's
  catalog for ever — and checking only at creation would leave the limit bypassable in three
  moves: unpublish one, create a replacement, publish the first back.
- A training always belongs to a trainer; there is no orphan training.
- A training changes hands only to a trainer who could have published it themselves: room under the
  ten, and no training of theirs already carrying that title. A transfer that would break either
  rule for the recipient is refused, and the giver keeps the training.
- Every value object is valid by construction — an aggregate never holds a malformed field, because
  it never accepts a raw `string`.
- A trainer never disappears silently: deletion takes their trainings with it.
- A training moves between `Published` and `Unpublished`, and each move announces itself. A
  transition to the state it is already in is refused rather than ignored: a change that changes
  nothing must not raise a fact. Deleting announces itself too, on all three paths that delete a
  training — the everyday one on each stack, and the cascade that removes a departing trainer's
  catalog, which was the last one still silent. It did not before, and the
  absence is why a deleted training used to stay in the search index for ever.
- A withdrawn training keeps its title, and the title stays taken. It is taken by something its
  owner can see in their own listing and can republish, rename or delete, so the refusal names
  something actionable rather than something invisible.
- A suspended trainer may not increase their public footprint: creating, publishing and
  transferring — giving and receiving alike — are refused by the domain. Every other write, editing
  and unpublishing included, is refused at the boundary (ADR 0053): repairing means something only
  if repairing leads somewhere, and no review loop exists. The trainer keeps every read, and reads
  the reason on their own profile.

### Actors

| Actor | What they may do | Status |
|---|---|---|
| **Trainer** | Everything in this context, and only to their own data | Implemented — `ICurrentUserService.TrainerId` |
| **Visitor** | Register, then sign in | Implemented |
| **Administrator** | Suspend and reinstate a trainer; withhold and release a training; list either, filtered by state | Implemented — six endpoints under `/Administration`, behind the `Administrator` role (ADR 0051, ADR 0052, ADR 0055). Removing a trainer is not among them: `Trainer.MarkForDeletion` states the rule and no command reaches it |

The third row has said three different things in three commits, and the sequence is the point. It
began by saying the permission was absent — no role was ever granted, so the rules it named could
only be reached from a unit test. ADR 0051 gave the actor a role, a policy and a token that needs no
trainer, and the row said the authority existed while the use cases did not. Those use cases are the
four endpoints now, and what is left unclaimed is one line rather than a paragraph. An administrator
is an **account, not a trainer**, which is why their token carries no `trainer_id`, why the trainer
surface refuses them rather than raising on the missing claim, and why the administrative endpoints
sit on a controller base of their own.

The first row's *only to their own data* has one deliberate exception, and it is worth naming
because it looks like a leak and is not. Transferring a training reads two facts about the
**recipient's** catalog — how full it is, and whether a title of that name is already in it. The
caller never sees either: both come back as a refusal or as nothing at all, so the command decides
on data it is not shown. That is the whole of what one trainer may learn about another here.

### Business capabilities

- Maintain a trainer profile (name, contact address, bio).
- Publish and withdraw a portrait.
- Author a training: create, edit, delete.
- Hand a training to another trainer, when their catalog can take it.
- Consult one's own catalog.

### Use cases

Every one of them exists twice — once per application style. See the
[use-case table in the README](../../README.md#use-cases) for the handler of each.

| Use case | Actor | Aggregate |
|---|---|---|
| Register as a trainer | Visitor | `Trainer` (and an Identity account) |
| Read own profile | Trainer | `Trainer` |
| Edit own profile | Trainer | `Trainer` |
| Publish or replace a portrait | Trainer | `Trainer` |
| Remove a portrait | Trainer | `Trainer` |
| View a trainer's portrait | Trainer | `Trainer` |
| Create a training | Trainer | `Training` |
| Edit a training | Trainer | `Training` |
| Delete a training | Trainer | `Training` |
| Transfer a training | Trainer | `Training` (and the recipient's catalog) |
| Read one own training | Trainer | `Training` |
| List own trainings | Trainer | `Training` |

### What this context deliberately does not do

- **It does not schedule anything.** `Training` has no date, no session, no capacity and no price.
  That absence is the clearest statement in the model about where this context ends.
- **It does not serve a catalog.** Every read is scoped to the caller. Five endpoints that handed
  out other trainers' data were removed rather than restricted, because a read scoped to one caller
  is not a catalog read.
- **It does not authenticate.** See below.

---

## Context — Identity & Access

**Supporting, and bought rather than built.** Accounts, passwords, lockout, tokens.

### Responsibility

Establish who is making a request. Nothing else.

### Ubiquitous language

| Term | What it means |
|---|---|
| **User / account** | A set of credentials — username, email, password hash, lockout state |
| **Username** | Unique. What one signs in with |
| **Account email** | Unique. Not the same concept as a trainer's contact address |
| **Role** | Modeled by the framework. One is granted: `Administrator`, seeded in Development and granted by hand elsewhere (ADR 0051) |
| **Token** | A JWT, issued at sign-in, carrying the account and the trainer it maps to |

### Aggregates

None of this repository's own. The context is `IdentityUser<Guid>` and `IdentityRole<Guid>`, used
unmodified — the model belongs to the framework, which is what *supporting* means here.

### The boundary, and where to see it

This is the one boundary the code makes explicit, in three artifacts:

1. **A separate `DbContext` with its own migration history.** `TrainingIdentityDbContext` and
   `TrainingContext` share a database and share nothing else.
2. **A single opaque reference.** `Trainer.UserId` is a typed identifier and the *only* thing the
   domain knows about an account. No `IdentityUser` type is reachable from the domain.
3. **A translation service.** `ICurrentUserService` exposes `UserId` **and** `TrainerId` as separate
   properties, and its own documentation explains why: *"authentication knows about accounts, the
   domain knows about trainers, and conflating them is what lets one caller edit another's work."*

Together those three are an anti-corruption layer reduced to its minimum — two types and a rule.

### Actors

Visitor (register, sign in) and Trainer (present a token).

### Business capabilities

Registration, authentication, token issuance, lockout.

---

## Context — Notification

**Generic.** Telling someone something happened.

- **Language:** `EmailMessage` — a recipient, a subject, a body. Deliberately primitive: the port
  carries no domain type, so the notifier can never grow an opinion about trainers.
- **Aggregates:** none.
- **Status:** real. `IEmailSender` is declared in the application layer beside its two consumers
  and implemented by `SmtpEmailSender` over MailKit — Mailpit locally, any relay by configuration
  ([ADR 0031](../adr/0031-send-email-over-smtp-and-prove-it-against-a-real-server.md)). The
  protocol is the boundary: only the infrastructure names the mail client, and a rule holds that
  line.
- **Fed by:** the transactional outbox, end to end. Registration, address changes and the
  administration's decisions commit `TrainerCreatedIntegrationEvent`,
  `TrainerContactEmailChangedIntegrationEvent`, `TrainerSuspendedIntegrationEvent`,
  `TrainerReinstatedIntegrationEvent`, `TrainingWithheldIntegrationEvent` and
  `TrainerContactedIntegrationEvent` with the change
  itself (ADR 0002, ADR 0024, ADR 0056, ADR 0082), and the delivery worker hands each fact to the consumer
  that composes its `EmailMessage` — after the commit, at-least-once (ADR 0025). The three
  sanction notices go to the account's address rather than the published contact address, resolved
  through `ITrainerAccountQuery` when the notice is sent.

---

## Context — Search Indexing

**Generic.** Keeping a read model in step with the writes.

- **Language:** `IndexAsync(Guid trainingId, Guid trainerId)`, `RemoveAsync(Guid trainingId)`,
  `HideTrainerCatalogAsync(Guid trainerId)` and `ShowTrainerCatalogAsync(Guid trainerId)`.
  Note the primitives: the port speaks `Guid`, never `TrainingId`. Its own remark says so — *"the
  search engine sitting behind it knows nothing about the domain's typed identifiers."* That is a
  published language in miniature. The removal is what lets a withdrawn or deleted training leave
  the index; without it ADR 0050 would have changed nothing a visitor could observe. The pair that
  follows it says a trainer's standing in one call: public visibility is composed from two
  aggregates and stored nowhere on the write side, so the read model composes it too (ADR 0056).
  Since ADR 0059 the language has a read half as well — `SearchAsync(term, paging)` — which is the
  query surface ADR 0055 refused to open before there was an index to answer it.
- **Aggregates:** none.
- **Status:** built (ADR 0059). An inverted index in two tables of the same database: one entry per
  training, holding the title and the two facts public visibility is composed of, and one row per
  word of that title so that a search seeks instead of scanning. One adapter writes it, the query
  port reads it, and `GET /Catalog/trainings` is its first reader. Since ADR 0062 it has a second,
  `GET /Catalog/trainings/{id}`, which asks this index one question only — is this training on
  offer — and reads the write model for everything it then shows. Those two are the whole anonymous
  surface of this API.
- **Fed by:** the transactional outbox, end to end. Every change that alters what a visitor
  would be shown commits its own fact with it — `TrainingCreatedIntegrationEvent`,
  `TrainingEditedIntegrationEvent`, `TrainingTransferredIntegrationEvent`,
  `TrainingPublishedIntegrationEvent`, `TrainingUnpublishedIntegrationEvent`,
  `TrainingDeletedIntegrationEvent`, `TrainingWithheldIntegrationEvent`,
  `TrainerSuspendedIntegrationEvent` and `TrainerReinstatedIntegrationEvent` — and the delivery
  worker replays each fact into this port after the commit (ADR 0002, ADR 0024, ADR 0025) — the
  index only ever learns of trainings the database accepted. A transfer is an indexing event like
  the others: what a public search would show changes, because the training is filed under a
  different trainer. The last three are the sanctions (ADR 0056), and the two trainer facts are
  why this port speaks about a catalog at all: a suspension writes to no training, so the index
  is told about its owner once instead of about each training in turn.

---

## Context — Media Storage

**Generic.** Bytes under a key.

- **Language:** `ObjectKey`, `StoredObject`, and three operations — put, get, delete. No bucket, no
  endpoint, no URL. `ITrainerPhotoStore` sits above it and is the only thing that knows a key looks
  like `trainers/{trainerId}/{photoId}`.
- **Aggregates:** none. The domain holds a photo's *identity* and never its address.
- **Status:** implemented, against SeaweedFS through the S3 protocol. There is deliberately **no
  `Replace` operation**, and that absence is the safety argument — see
  [ADR 0021](../adr/0021-store-a-photo-beside-the-row-that-names-it.md).

---

## Context — Catalog Discovery

**Emerging, and now partly visible.** The public site: search trainings, browse by topic, read a
trainer's profile.

This context still does not exist as a context — it owns no store of its own, and what a visitor
reads is served by Training Catalog over Search Indexing's index. What changed with ADR 0062 is
that the reading exists at all: two anonymous endpoints and two screens above them. A reader should
know which of the things below were built for a context that may never be extracted:

- **The search index** exists and answers (ADR 0059), through `ITrainingSearchQuery`, and the facts
  that maintain it — `TrainingCreatedIntegrationEvent`, `TrainingEditedIntegrationEvent`,
  `TrainingTransferredIntegrationEvent`, `TrainingPublishedIntegrationEvent`,
  `TrainingUnpublishedIntegrationEvent`, `TrainingDeletedIntegrationEvent`,
  `TrainingWithheldIntegrationEvent`, `TrainerSuspendedIntegrationEvent` and
  `TrainerReinstatedIntegrationEvent` — already land durably in the outbox on every commit.
- **The catalog's two reads** exist and are anonymous (ADR 0062): a paged title search over the
  index, and a reading of one offered training that takes its *visibility* from the index and its
  *content* — description, prerequisites, acquired skills, topics, and the trainer's name — from the
  write model, live. Two screens sit above them, `/catalog` and `/catalog/{id}`, behind no
  session at all.
- **The portrait is published**, at `GET /Catalog/trainings/{id}/photo/{photoId}` — an address
  naming a training and a photo and never a person, which is both what a visitor can have been given
  and what makes its year-long `immutable` cache true by construction. What made it publishable is
  ADR 0063: the metadata ADR 0021 deferred stripping is stripped when the bytes arrive, the domain
  records that it was, and a portrait carrying no such record is refused. The authenticated
  `GET /Trainer/{id}/photo` stays where it is, addressed by identifier rather than by `me`, and now
  says `max-age` with an `ETag` instead of `immutable` — its address does not name the photo, so its
  bytes do change.
- **The catalog's facets** exist (ADR 0069): the index files each entry under the topics its
  training declares, `GET /Catalog/topics` counts the offered shelves — absent rather than zero —
  and the search takes a `topic`. The first word of this context's expected language, spoken by
  running code. ADR 0080 turned that word plural: the search takes as many shelves as a visitor
  ticks and answers whatever sits on at least one of them, and the counts answer the term they
  typed rather than the whole catalog.
- **The trainer's public page** exists (ADR 0070): `GET /Catalog/trainers/{id}` answers who an
  offering person is and what they offer — visibility from the index, identity read live from the
  write model — and the navigation runs both ways between a training and its author. Offered or
  invisible: a person with nothing on offer answers the same 404 as one who never existed.
- **The CQRS query side** already projects straight into DTOs without loading aggregates, which is
  the shape a public read model wants.

**Expected relationship:** downstream of Training Catalog, fed by the integration events the
outbox already stores, with its own read model. It would own no aggregate — a discovery context
reads, it does not decide. What is missing is no longer a store, and no longer the experience
either: ADR 0059 built the one and ADR 0062 the other. What is missing is the reason to extract a
context — a store shaped by how a visitor browses rather than by how a trainer writes. The
facets, the trainer's public page and the second order were all on that list and arrived without
needing one (ADR 0069, ADR 0070, ADR 0071), which is itself evidence: until something on it
cannot be served this way, a page over the same database is the honest size of it.

**Expected language:** *catalog*, *search result*, *facet*, *listing* — deliberately different
words from the write side, because a search result is not a `Training`.

---

## Not decided

The two contexts below are **hypotheses**, not plans. They are named because the model's silences
point at them, and they are kept out of the map so nobody mistakes them for a decision.

**Scheduling.** `Training` has no date, no session, no capacity. Offering a training on the 14th of
March, twelve seats, in Lyon, is a different lifecycle from describing what the training *is* — one
changes constantly, the other rarely. That is a boundary, whenever somebody needs it.

**Enrollment.** There is no participant in this system. No `Participant`, no `Registration`, no
`Attendance` — and adding a learner would introduce a second kind of actor, with its own view of the
catalog and its own rules about seats and cancellation.

Neither is being built. If either ever is, this section should be deleted and replaced by a context
above it, with the same evidence the others carry.

A third hypothesis is worth naming for what it is *not*. **Moderation** — a sanction with a reason, a
duration, an author, an appeal — would be a context, because those concepts describe a judgment
rather than a trainer or a training and nobody else owns them. The trainer standing decided above
presupposes none of it: one reversible state, no reason recorded, no history kept. If a sanction ever
needs to be explained, timed or contested, that is the moment to draw the boundary — and not before.
