# Bounded contexts

What the system is about, cut into the pieces that have their own language — and the argument for
each cut. Everything below is read off the model as it stands; where a boundary is intended rather
than built, it says so.

## Subdomains

Not every part of a system deserves the same care. Distinguishing them is the first strategic act,
because it decides where effort goes.

| Subdomain | Kind | Why | Where it lives |
|---|---|---|---|
| Training Catalogue | **Core** | The reason the system exists: a trainer describes what they teach. Every rule that is specific to this business is here. | `src/TrainingHub.Shared.Domain/` |
| Identity & Access | Supporting | Necessary, not distinctive. Bought rather than modelled — ASP.NET Core Identity, unmodified. | `Shared.Infrastructure/ThirdParty/Identity/` |
| Notification | Generic | Sending an email is the same problem for everybody. | `IEmailSender` in `Shared.Application/Notifications/`, a MailKit adapter behind it — see [ADR 0031](../adr/0031-send-email-over-smtp-and-prove-it-against-a-real-server.md) |
| Search Indexing | Generic | Keeping a read model in step with writes. | `ITrainingSearchIndexer`, one fake implementation |
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
honours, which is the failure mode this document exists to avoid.

The line that *is* real runs between the domain and authentication, and the code states it out loud —
see the next two sections.

---

## Context — Training Catalogue

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
| **Training** | A training a trainer offers. A catalogue entry, not a scheduled event. | Aggregate root; belongs to exactly one trainer |
| **Name** | A trainer's `Firstname` and `Lastname` | Each 2 to 50 characters once trimmed; both refusals are reported together |
| **Email** | The address a trainer wishes to be contacted at, split into `LocalPart` and `Domain` | Must be a valid address. **Not unique** — see below |
| **Bio** | A trainer's own description of themselves | Optional; at most 500 characters, and blank is a refusal rather than an empty bio |
| **TrainerPhoto** | The portrait a trainer publishes | At most 5 MiB; PNG, JPEG or WebP, recognised by reading the bytes rather than trusting the caller |
| **TrainingTitle** | What a training is called | 5 to 100 characters, and unique **per trainer** |
| **TrainingDescription** | What the training covers | Required, at most 500 characters |
| **TrainingPrerequisites** | What a participant needs beforehand | Required, at most 500 characters |
| **AcquiredSkills** | What a participant leaves with | Required, at most 500 characters |
| **Topic** | What a training is filed under | A **closed set of six**: Programming, Design, Marketing, Business, Personal Development, Leadership |
| **TrainingStatus** | Whether a training is offered to the public or withdrawn from it | `Published` or `Unpublished`; born published, and reachable in both directions |
| **TrainerStatus** | Whether a trainer is in good standing or under sanction | `Active` or `Suspended`; the whole of a suspension is this one field |
| **TrainingTransferDomainService** | Handing a training to another trainer, who becomes its owner | The recipient must be able to accept it; the giver keeps nothing |

Four entries in that table are worth pausing on, because each encodes a business decision rather
than a technical one.

**A contact address is not a login.** `Trainer.ContactEmail` carries no uniqueness rule, and the
aggregate says why: a trainer may publish a professional address different from the one their
account was opened with, and two trainers of the same organisation may legitimately share one. The
account's email is unique; the contact address is not. They are different concepts that happen to
have the same shape.

**A title is unique per trainer, not globally.** Two trainers may both teach "Introduction to
Domain-Driven Design"; one trainer may not list it twice. The rule is the only one the aggregate
cannot answer alone, so it asks `IUniquenessTitleChecker` — a port, so the domain states the rule
without knowing how uniqueness is looked up.

**A transfer belongs to neither aggregate.** Handing a training over reads the *recipient's*
catalogue in order to mutate the *giver's* training, and neither `Training` — which knows one owner
— nor `Trainer` — which holds no training — can decide it alone. It is the model's one recorded
domain service, and the only term in this table that is not a noun of the business:
`TrainingTransferDomainService`, static and stateless, deciding through the same two ports creation
uses ([ADR 0036](../adr/0036-model-the-decision-that-has-no-home-as-a-domain-service.md)).

**Visibility is composed, never stored.** A training is publicly visible when it is `Published`
**and** its trainer is `Active`. There is no third field holding the answer, and that is what makes
a suspension liftable: it writes one column on one aggregate and touches no training, so nothing
has to remember which trainings it hid — it hid none, and the catalogue simply became invisible
with its owner. Cascading the sanction onto each training was considered and refused for exactly
this reason
([ADR 0050](../adr/0050-retire-a-training-rather-than-delete-it.md)).

### Aggregates

- `Trainer` — the profile, its contact address, its bio and its portrait.
- `Training` — the catalogue entry, its content and its topics.

Each is an independent consistency boundary: `Training` names its owner by `TrainerId` and never
holds a `Trainer` instance.

### Invariants

- A training's title is unique among the trainings of the same trainer.
- A trainer publishes at most ten **published** trainings (`Training.MaximumPerTrainer`); the
  eleventh is refused, both at creation and when a withdrawn training is published again. A
  withdrawn training holds no place in the quota — otherwise ten of them would end a trainer's
  catalogue for ever — and checking only at creation would leave the limit bypassable in three
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
  catalogue, which was the last one still silent. It did not before, and the
  absence is why a deleted training used to stay in the search index for ever.
- A withdrawn training keeps its title, and the title stays taken. It is taken by something its
  owner can see in their own listing and can republish, rename or delete, so the refusal names
  something actionable rather than something invisible.
- A suspended trainer may not increase their public footprint: creating, publishing and
  transferring — giving and receiving alike — are refused. Editing and unpublishing stay open, so a
  trainer can repair what earned them the sanction.

### Actors

| Actor | What they may do | Status |
|---|---|---|
| **Trainer** | Everything in this context, and only to their own data | Implemented — `ICurrentUserService.TrainerId` |
| **Visitor** | Register, then sign in | Implemented |
| **Administrator** | Suspend and reinstate a trainer, remove one | **Entitled, not yet reachable.** The role, the policy and a token that needs no trainer exist (ADR 0051); `Trainer.Suspend`, `Reinstate` and `MarkForDeletion` state the rules; no endpoint calls them yet |

The third row used to say the permission was absent — no role was ever granted, and none was seeded,
so the rules it names could only be reached from a unit test. What is missing now is narrower and
says so: the authority exists and the use cases that exercise it do not. An administrator is an
**account, not a trainer**, which is why their token carries no `trainer_id` and why the trainer
surface refuses them rather than raising on the missing claim.

The first row's *only to their own data* has one deliberate exception, and it is worth naming
because it looks like a leak and is not. Transferring a training reads two facts about the
**recipient's** catalogue — how full it is, and whether a title of that name is already in it. The
caller never sees either: both come back as a refusal or as nothing at all, so the command decides
on data it is not shown. That is the whole of what one trainer may learn about another here.

### Business capabilities

- Maintain a trainer profile (name, contact address, bio).
- Publish and withdraw a portrait.
- Author a training: create, edit, delete.
- Hand a training to another trainer, when their catalogue can take it.
- Consult one's own catalogue.

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
| View a trainer's portrait | Trainer *(shaped to become public)* | `Trainer` |
| Create a training | Trainer | `Training` |
| Edit a training | Trainer | `Training` |
| Delete a training | Trainer | `Training` |
| Transfer a training | Trainer | `Training` (and the recipient's catalogue) |
| Read one own training | Trainer | `Training` |
| List own trainings | Trainer | `Training` |

### What this context deliberately does not do

- **It does not schedule anything.** `Training` has no date, no session, no capacity and no price.
  That absence is the clearest statement in the model about where this context ends.
- **It does not serve a catalogue.** Every read is scoped to the caller. Five endpoints that handed
  out other trainers' data were removed rather than restricted, because a read scoped to one caller
  is not a catalogue read.
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
| **Role** | Modelled by the framework; none is granted yet |
| **Token** | A JWT, issued at sign-in, carrying the account and the trainer it maps to |

### Aggregates

None of this repository's own. The context is `IdentityUser<Guid>` and `IdentityRole<Guid>`, used
unmodified — the model belongs to the framework, which is what *supporting* means here.

### The boundary, and where to see it

This is the one boundary the code makes explicit, in three artefacts:

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
- **Fed by:** the transactional outbox, end to end. Registration and address changes commit
  `TrainerCreatedIntegrationEvent` and `TrainerContactEmailChangedIntegrationEvent` with the
  change itself (ADR 0002, ADR 0024), and the delivery worker hands each fact to the consumer
  that composes its `EmailMessage` — after the commit, at-least-once (ADR 0025).

---

## Context — Search Indexing

**Generic.** Keeping a read model in step with the writes.

- **Language:** `IndexAsync(Guid trainingId, Guid trainerId)` and `RemoveAsync(Guid trainingId)`.
  Note the primitives: the port speaks `Guid`, never `TrainingId`. Its own remark says so — *"the
  search engine sitting behind it knows nothing about the domain's typed identifiers."* That is a
  published language in miniature. The removal is what lets a withdrawn or deleted training leave
  the index; without it ADR 0050 would have changed nothing a visitor could observe.
- **Aggregates:** none.
- **Status:** port only, one fake implementation that logs. It is nonetheless the seed of the
  public catalogue: the index this port maintains is what a search page would read.
- **Fed by:** the transactional outbox, end to end. Creating, editing, handing over, publishing,
  withdrawing or deleting a training commits `TrainingCreatedIntegrationEvent`,
  `TrainingEditedIntegrationEvent`, `TrainingTransferredIntegrationEvent`,
  `TrainingPublishedIntegrationEvent`, `TrainingUnpublishedIntegrationEvent` or
  `TrainingDeletedIntegrationEvent` with it (ADR 0002, ADR 0024), and the delivery worker replays
  each fact into this port after the commit (ADR 0025) — the index only ever learns of trainings
  the database accepted. A transfer is an indexing event like the others: what a public search
  would show changes, because the training is filed under a different trainer. The last three
  arrived with ADR 0050, and two of them remove rather than index.

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

## Context — Catalogue Discovery

**Emerging, and already prepared for.** The public site: search trainings, browse by topic, read a
trainer's profile.

This context does not exist yet. It is documented here because three things in the model were built
for it, and a reader should know that they are not accidents:

- **`ITrainingSearchIndexer`** is the port a public search would read through, and the facts that
  will maintain its index — `TrainingCreatedIntegrationEvent`, `TrainingEditedIntegrationEvent`,
  `TrainingTransferredIntegrationEvent`, `TrainingPublishedIntegrationEvent`,
  `TrainingUnpublishedIntegrationEvent`, `TrainingDeletedIntegrationEvent` — already land durably in
  the outbox on every commit.
- **`GET /Trainer/{id}/photo`** is the one read addressed by identifier rather than by `me`, with a
  year-long immutable cache and an `ETag` cut from the photo's identity. Making it public is
  `[AllowAnonymous]` and nothing else.
- **The CQRS query side** already projects straight into DTOs without loading aggregates, which is
  the shape a public read model wants.

**Expected relationship:** downstream of Training Catalogue, fed by the integration events the
outbox already stores, with its own read model. It would own no aggregate — a discovery context
reads, it does not decide.

**Expected language:** *catalogue*, *search result*, *facet*, *listing* — deliberately different
words from the write side, because a search result is not a `Training`.

---

## Not decided

The two contexts below are **hypotheses**, not plans. They are named because the model's silences
point at them, and they are kept out of the map so nobody mistakes them for a decision.

**Scheduling.** `Training` has no date, no session, no capacity. Offering a training on the 14th of
March, twelve seats, in Lyon, is a different lifecycle from describing what the training *is* — one
changes constantly, the other rarely. That is a boundary, whenever somebody needs it.

**Enrolment.** There is no participant in this system. No `Participant`, no `Registration`, no
`Attendance` — and adding a learner would introduce a second kind of actor, with its own view of the
catalogue and its own rules about seats and cancellation.

Neither is being built. If either ever is, this section should be deleted and replaced by a context
above it, with the same evidence the others carry.

A third hypothesis is worth naming for what it is *not*. **Moderation** — a sanction with a reason, a
duration, an author, an appeal — would be a context, because those concepts describe a judgement
rather than a trainer or a training and nobody else owns them. The trainer standing decided above
presupposes none of it: one reversible state, no reason recorded, no history kept. If a sanction ever
needs to be explained, timed or contested, that is the moment to draw the boundary — and not before.
