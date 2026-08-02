# 0005 — Store audit timestamps at full precision

- **Status:** Accepted
- **Date:** 2026-08-01

## Context

`AggregateRootTypeConfiguration` declared `.HasPrecision(2)` on `CreatedOn` and `ModifiedOn`, so the
migrations produced `datetime2(2)`: two fractional digits, **buckets of 10 ms**. Two writes 3 ms
apart were stored with the same instant, whatever the clock had given.

That was never a decision. `git log -S "HasPrecision(2)"` traces the line to `3d69889`, a
restructuring commit that also moved the layers, enriched the Training aggregate, and added JWT
authentication and a Blazor front end. It arrived as a detail of something else, was never isolated,
and was never written down.

It had a consequence this repository created for itself. ADR-less but deliberate, the
`AuditableEntitiesInterceptor` was changed to read the clock **once per entity** so each carries the
instant it was stamped at. On Linux — CI, containers — the system clock advances well below the
millisecond, so those readings genuinely differ. `datetime2(2)` then rounded them into the same
bucket. The guarantee existed in memory and disappeared on the way to the row.

The unit test written to prove it could not see any of this: `AuditableEntitiesInterceptorTests`
runs on EF InMemory, which stores a `DateTime` as a `DateTime`. It asserted a property that SQL
Server undid.

## Decision

**`datetime2(7)` — full precision — on both audit columns.**

Seven fractional digits store every one of a `DateTime`'s 100-nanosecond ticks, so a stamp read back
is the stamp that was written. No other precision has that property: a .NET `DateTime` is a tick
count, and anything shorter rounds it.

Declared explicitly rather than left to the default. `datetime2(7)` is already what EF Core produces
for a `DateTime` on SQL Server, so removing `HasPrecision` would reach the same schema — but an
implicit default is exactly what let a `2` sit there unnoticed for the life of the project.

**The identifier tie-break stays**, and this decision does not weaken the case for it. Storage
precision was never the only source of equal timestamps: `TimeProvider.GetUtcNow` reads a system
clock that advances at the platform's timer interval — around 15.6 ms on Windows — and concurrent
requests collide regardless. Nothing makes the column unique. What changes is that truncation is no
longer *an additional* cause on top of those, which is what
`QueryableOrderingExtensions.NewestFirst` documents.

## Consequences

- What the interceptor writes is what the row holds. The per-entity clock reading now reaches
  persistence instead of stopping at the change tracker.
- Ordering, filtering and any future audit query see the resolution the clock actually provided.
- Two extra bytes per column, on two columns of two tables — `datetime2(7)` is 8 bytes against 6.
  At this scale, and against a `rowversion` and a `uniqueidentifier` key already on every row, it
  does not register.
- The unit test on the interceptor stops being misleading, and an integration test now asserts the
  resolution survives the round trip — something no in-memory provider can check.

Against that:

- **Rows written before the migration keep their 10 ms granularity.** Widening a column changes the
  type, not the values already rounded into it. No backfill can recover what was discarded.
- `Down` loses data, unlike most migrations here: narrowing back to two digits rounds every stored
  instant irreversibly. Said so in the migration.
- The migration was hand-written, because the environment it was authored in has no .NET SDK. The
  model snapshot must match what `dotnet ef` would have produced or the next generated migration
  carries a spurious diff — see the verification note below.

## Alternatives considered

**Keep `datetime2(2)` and document it.** No migration, no risk. Rejected because it would mean
writing down that the audit stamp is a 10 ms bucket, that the interceptor's per-entity reading does
not survive persistence, and that a test in this repository proves something the database undoes.
Documenting a defect is not the same as deciding one.

**`datetime2(3)`, the millisecond.** The usual compromise, 7 bytes, a granularity everyone
understands and plenty for a business audit trail. Rejected for the reason `2` was: it still
truncates what the clock gave, so the in-memory value and the stored value can still differ, and the
only argument for it — one byte — is not an argument.

**Drop `HasPrecision` and inherit EF's default.** Reaches the same `datetime2(7)`. Rejected because
the resulting schema would then depend on a provider default rather than on a stated intent, and
this record exists precisely because an unstated choice went unexamined for a year.

**Make the column unique, or `datetime2(7)` plus a sequence, to get a total order from the
timestamp alone.** Rejected: a unique constraint on a creation instant fails concurrent inserts,
which trades a cosmetic ordering property for a real availability one. The identifier already
settles ties for free.

## Verification note

`dotnet ef migrations add Probe` against this model must produce an **empty** migration. If it does
not, the hand-written snapshot diverged from what the tooling expects and should be regenerated.
