# 0060 — Look inside the column a search has to read

- **Status:** Accepted
- **Amends:** [0032](0032-flatten-a-value-object-as-a-complex-property-not-an-owned-entity.md)
- **Date:** 2026-08-09

**What this changes in 0032.** That record maps a value object by its shape and puts
`TrainingTitle` in the branch where *"one scalar converts"*, giving the index as the reason: *"a
plain column is what indexes want — the unique `(TrainerId, Title)` index sits on one"*. It also
rejects doing otherwise as churn, *"to make no observable difference"*. Here the difference is
observable and is the entire point, so the rule narrows: **one scalar converts, unless the column
has to be looked inside.** Nothing else in 0032 moves — the flattening of several scalars, the side
table for a collection, and the refusal of `OwnsOne` all stand, with the rule that defends them.

## Context

ADR 0055 left one consequence open and repeated it in six places: `/Administration/trainings` has no
`?search=`, because a converted column is one EF Core compares for equality without being able to
look inside. ADR 0059 narrowed the way out rather than opening it — the search index cannot answer
this question either, since it holds none of the states a moderator is looking for.

Both records priced the closure the same way: *"a migration and a decision of its own"*.

**Both were wrong about the price, and measuring it is what this record is for.** The remapping needs
no migration at all: the column is `nvarchar(100) NOT NULL` named `Title` before and after, which is
ADR 0032's own precedent. What it costs instead is the unique index — and not to a schema change, to
a version. Three things were run rather than reasoned about:

| Asked | Answered |
|---|---|
| `training.Title.Value.Contains(term)` against the converted column | `The LINQ expression 'DbSet<Training>().Where(t => t.Title.Value.Contains("Driven"))' could not be translated.` |
| the same, against the complex property | translated |
| `HasIndex(t => new { t.TrainerId, t.Title })` against the complex property | `The property or navigation 'Title' cannot be added to the 'Training' type because a property or navigation with the same name already exists on the 'Training' type.` |

Indexing a scalar nested in a complex type lands in EF Core 11. This repository is on 10.0.10.

One thing that was expected to break did not: `TrainingTitleExistsForTrainerSpecification` compares
the whole value object — `training.Title == title` — and still translates. The specification is
untouched, and the fact that this was checked rather than assumed is why it is stated here.

## Decision

**A value object whose column a query has to look inside is mapped as a complex property, and the
index that column carried moves to the database alone.**

- **`Training.Title` becomes a complex property**, one member, column `Title`, hundred characters,
  required. That single change is what makes a substring match translate, and everything below is
  its consequence.
- **The unique index on `(TrainerId, Title)` leaves the model and stays in the database.** It is
  already there, created by `20260730163126_AddUniqueTrainingTitlePerTrainer`, and nothing about the
  schema changes. `TrainingConfiguration` stops declaring it and says why in its place.
- **Nothing a caller sees changes.** `IUniquenessTitleChecker` still pre-checks for a clean message,
  the index still settles the check-then-act race, `UnitOfWork` still turns SQL error 2601 or 2627
  into `Training.DuplicateTitle`, and the same `409` comes back with the same code.
- **`GET /Administration/trainings` gains `?search=`**, matched against the title and nothing else —
  a description and a set of prerequisites are prose, and searching them turns a bounded scan into
  an unbounded one, which is the argument ADR 0055 already makes for keeping a bio out of the
  trainers' term. Two criteria compose; neither replaces the other.
- **The cost of the term is the one ADR 0055 recorded and is unchanged**: a `LIKE '%term%'` no index
  can seek, over a bounded population, read by one authority. The search that seeks is the public
  catalogue's, over the inverted index of ADR 0059 — which cannot answer this one.
- **The snapshot moves and no migration is written.** ADR 0005's acceptance check is the arbiter, and
  it is named under Verification together with the fact that this environment could not run it.

## Consequences

- **A guarantee left the model, and a rule replaced it.** `TheUniquenessTheModelCannotExpress_IsStillCreatedByAMigration`
  reads the migrations rather than the configuration, because the model is where the claim no longer
  is. Delete the `CreateIndex` while squashing history and the pre-check goes on answering while two
  concurrent creations both win — a defect whose only other witness runs in the integration workflow
  the fast one excludes.
- **The two administrative listings are symmetric again.** ADR 0055 said a reader would notice the
  asymmetry before reading the record; there is nothing left to notice.
- **The asymmetry ADR 0059 described has swapped sides and shrunk.** The public catalogue seeks and
  the administration scans, which is a difference in cost rather than in capability, and each is
  matched to the population it reads.
- **One test suite existed for the first time.** Nothing fast covered the translation of
  `AnyAsync(spec.Criteria)`: the specification's own facts evaluate the compiled delegate, and every
  other caller substitutes the port. That gap predates this record and is closed by it.
- **A future EF Core 11 lets the index come home.** The configuration says so, so nobody has to
  rediscover why it left.
- **The InMemory provider still cannot query a complex property**, and `Training` now has two.
  Nothing in the suites queries a title through it today; the day something does, it moves to SQLite
  like the read-side suites already have.

## Alternatives considered

**Wait for EF Core 11.** Honest, and it makes the record a note rather than a change. Rejected
because the wait buys nothing: the index is a database object, the database keeps enforcing it, and
what is actually lost is the model's ability to *describe* something it does not enforce.

**Keep the conversion and search some other way.** There is no other way. `EF.Property<string>` fails
as an invalid cast — ADR 0055 measured that — and a converted property has no member a translator
can reach.

**Mirror the title into a second, plain column.** A duplicate that two writers keep in step, for a
listing one role reads. The kind of second system ADR 0055 warned against, arriving by a smaller
door.

**Serve the administration from the search index.** ADR 0059 rejected it a day earlier and the
reason has not changed: the index is defined by what it excludes, and a moderator asks for exactly
that.

**Widen the term to the description and the prerequisites.** More matches, an unbounded scan, and a
result set nobody can act on — the argument ADR 0055 makes about a trainer's bio, applied to the
same shape.

## Verification

- **The three measurements above**, run against the real model rather than read in a release note,
  and in both directions — the failure on the conversion is as much of the record as the success on
  the complex property.
- **`TheUniquenessTheModelCannotExpress_IsStillCreatedByAMigration`**, watched failing first, with
  `unique: true` turned to `unique: false` in the migration that creates the index — the exact shape
  a careless squash would produce.
- **`TitleUniquenessTests`**, against SQLite through the model itself: the specification still
  answers as SQL, the term matches a title as a substring, the term and the state compose rather
  than replace one another, and a blank term narrows nothing.
- **The trainings' half of `AdministrationListingServiceTests`**, which asserted for the trainers
  only that the criteria reach the named question unchanged — the suite's own doc claimed both.
- **Shared facts in `tests/TrainingHub.Api.TestKit/`**, so both hosts answer them against SQL
  Server: a withheld training is findable by a word of its title, the two criteria compose, and a
  term nothing answers to empties the page without emptying the envelope.
- **`Create_DuplicateTitleForSameTrainer_Returns409` and `Create_SameTitleForAnotherTrainer_Returns201`
  are unchanged**, and that is the point: they are what proves the index is still there after it
  left the model.
