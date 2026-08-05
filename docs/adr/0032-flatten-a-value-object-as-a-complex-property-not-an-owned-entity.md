# 0032 — Flatten a value object as a complex property, not an owned entity

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

The trainer's four profile value objects — `Name`, `ContactEmail`, `Bio`, `Photo` — were mapped
with `OwnsOne`, the construct EF Core offered back when it had nothing value-shaped: an owned
entity type is an entity wearing a value object's clothes. It carries a hidden key (a shadow
`TrainerId` that is both primary key and foreign key), holds its own entry in the change tracker,
participates in identity resolution, and the model snapshot records `WithOwner().HasForeignKey` —
a foreign key from a value to its owner, which is a sentence the domain never said. Two owners can
never share one instance, not because values are not shareable but because entities are not; a
replaced value object is tracked as a delete and an insert, machinery a value does not have.

EF 8 introduced complex types with none of that: no key, no tracker identity, comparison by
members. What kept them out of reach here was optionality — `Bio` and `Photo` are absent for many
trainers, a null column must round-trip as a null navigation, and a complex property could not be
optional before EF 10. The version this repository builds against removed that limitation, so the
owned mapping stopped being a constraint and became a leftover.

This record decides value-object persistence as a whole rather than patching one aggregate,
because three strategies already coexist — value conversions on `Training`'s texts and every typed
identifier, owned types on `Trainer`'s profile, an owned collection for `Topics` — and no record
said when each applies. A reader meeting all three could reasonably take the mix for accident. It
was: the split fell out of API history, not out of a decision.

## Decision

**A value object is persisted by its shape: one scalar converts, several scalars flatten as a
complex property, a collection owns a side table.**

- **One scalar converts.** A value object wrapping a single value maps through `HasConversion`
  onto an ordinary column: `TrainingTitle`, `TrainingDescription`, `TrainingPrerequisites`,
  `AcquiredSkills`, and every typed identifier through `AggregateRootTypeConfiguration`. A plain
  column is what indexes want — the unique `(TrainerId, Title)` index sits on one — and what a
  specification's criteria translate onto (ADR 0028). A complex property of one member would buy
  the same semantics with more machinery.
- **Several scalars flatten.** **A value object flattened into its owner's table is a complex
  property, never an owned entity.** `Name`, `ContactEmail`, `Bio` and `Photo` keep their columns
  in `Trainer` to the byte, and lose the shadow key, the tracker entry and the phantom foreign
  key. Optionality lands where it belongs: the `Bio` complex property is optional while its
  `Value` stays required — matching the factory, which refuses an empty bio — where the owned
  mapping had to declare the value itself nullable and explain in a comment why the type said
  otherwise.
- **A collection owns a side table.** **A collection of value objects lives in a relational side
  table, never in a JSON column.** `Topics` stays an `OwnsMany` into `TrainingTopic`: a real
  table with a typed, length-bound, indexable `Name` column that plain SQL can see. Owned
  entity types remain exactly right here — a row in a side table has a key because rows do.

The rules `NoConfiguration_FlattensAValueObjectAsAnOwnedType` and
`EveryValueObjectCollection_LivesInARelationalSideTable` defend the second and third branch; the
first needs no rule of its own, because a single-scalar value object mapped any other way trips
one of the two.

## Consequences

- **The schema did not move.** Every column name, type, length and nullability is byte-identical;
  the proof is ADR 0005's own acceptance check — `dotnet ef migrations add Probe` against the
  converted model produces an empty migration. Only the model snapshot changed, so there is no
  new row in the migrations table.
- The change tracker now treats a profile value as a value: no per-value-object entries, no
  identity resolution, and the "same instance owned twice" exception cannot exist because
  nothing owns anything.
- The boundary of the second branch is the third: a complex type cannot map to its own table,
  and its collections go to JSON columns only. The day a flattened value object needs a table of
  its own, it becomes a row and rows are owned — that move re-crosses this record and gets a new
  one.
- The frozen migration designer files keep their `OwnsOne` history; the rules scan only the
  hand-written configurations, because a migration records what the model was, not what it is.
- The InMemory provider accepted the converted model unchanged — the one test that builds the
  real `TrainingContext` on it (`NoTrackingDuringQueryExecutionBehaviorTests`) passes untouched.

## Alternatives considered

**Keeping the owned entity types.** They work, and every EF version since 2.0 understands them.
Rejected because they say the wrong thing in every place a reader looks: an identity in the
tracker, a shadow key in the model, a foreign key in the snapshot — entity vocabulary for types
whose whole point is having none. The mapping predated the better construct; keeping it once the
constraint was gone would have turned a leftover into a decision without writing the decision
down.

**JSON complex collections for `Topics`.** EF 10 can store a complex collection in a JSON column,
which would retire the `TrainingTopic` table. Rejected: the table gives every topic a typed,
length-constrained, indexable column that plain SQL and future queries can reach, and the swap is
a schema migration that buys none of that back. A document column is where a collection goes to
disappear from the relational surface, and nothing about topics wants to disappear.

**One converter packing a multi-property value object into one column.** `HasConversion` can
serialise `Name` into a single string. Rejected without much debate: it trades two typed,
individually bounded columns for a format only the converter understands, and every constraint
the database could enforce moves into application code.

**Complex properties for the single-scalar branch too.** Uniformity has appeal — one construct
for every value object. Rejected as churn: the conversions already give value semantics on plain
columns, the typed identifiers need a converter regardless because they cross into keys, and the
rewrite would touch every configuration to make no observable difference.

## Verification

The Probe check above proves the schema stood still. `Register_WithoutABio_AnswersANullBio` in
the shared TestKit proves on both hosts, against SQL Server, that a null `Bio` column comes back
as a null `Bio` rather than an empty value object — the exact seam optional complex properties
introduce — and `Read_TrainerWithNoPhoto_AnswersNotFound` has proved the same for `Photo` since
ADR 0021. Both architecture rules were broken once — `ContactEmail` reverted to `OwnsOne`, the
`Topics` table mapping removed — and failed naming the offending file, before being restored.
