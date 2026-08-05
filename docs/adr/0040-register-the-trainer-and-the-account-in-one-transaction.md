# 0040 — Register the trainer and the account in one transaction

- **Status:** Accepted
- **Date:** 2026-08-05
- **Amends:** [0002](0002-keep-domain-reactions-in-the-transaction-and-deliver-integration-events-through-an-outbox.md)

## Context

ADR 0002 closes its consequences with a scope limit: "The outbox lives in `TrainingContext` and
covers only it. `TrainingIdentityDbContext` is a separate context: an operation spanning
registration and trainer creation is still not atomic, and this decision does not change that."

The first sentence is true and worth having. The second is not true of this code, and was not true
on the day it was written. `AuthControllerBase.Register` opens a `TransactionScope` before it
touches anything, creates the Identity user, calls `CreateTrainerAsync`, and reaches `Complete()`
only on the success branch — so a refused trainer takes the account down with it. The scope landed
in February; ADR 0002 was written in August. This is not drift: the record generalised a limit
about the outbox into a claim about a flow that already had an answer.

Everything else in the repository says the opposite of the record. The README says registration is
atomic; the context map says "either both exist or neither does" and explains why this is a local
transaction; the event-storming board calls registration the only command that writes to two
contexts; both `TrainerController` files say "creates the identity user and its trainer
atomically"; and ADR 0016 *depends* on the rollback — it records that a rejected
`CreateTrainerCommand` now "rolls back by reaching the end of the scope without `Complete()`".

Against six statements of atomicity stood one record saying the reverse, and **nothing at all
proving either**. No test creates an account and then fails the trainer half. The only assertion is
a comment.

## Decision

**Registration is one transaction: the Identity account and the trainer are both written, or
neither is — and the outbox's reach, which is what ADR 0002 was right about, stays limited to
`TrainingContext`.**

- **The mechanism, stated with its conditions.** The two contexts bind the *same* configuration key,
  `ConnectionStrings:TrainingContext` — the identity context has no key of its own — so both write
  to one database on one server. Neither opens an explicit transaction: they enlist in the ambient
  one, and because EF Core closes its connection between operations and the connection string is
  byte-identical, the second context is handed the same physical connection, already enlisted.
  Nothing is promoted, so nothing needs a distributed transaction coordinator. That last part is
  not a detail to leave unwritten: distributed transactions were restored to .NET for Windows only,
  and two *overlapping* connections here would fail on Linux rather than degrade. The atomicity is
  real and it rests on a condition — one database, one connection string, no overlap — which is why
  the condition is recorded rather than assumed.
- **`TransactionScopeAsyncFlowOption.Enabled` is load-bearing.** Both writes are awaited; without
  it the ambient transaction does not flow across the await and the scope silently guards nothing.
- **What ADR 0002 was right about is preserved.** The outbox table belongs to `TrainingContext`, so
  an integration event cannot be staged in the same `SaveChanges` as an Identity write. Registration
  is atomic across the two stores; the *outbox* is not a cross-context mechanism and never claimed
  to be. That distinction is the whole of what this record changes.
- **A rule alone cannot defend this, so a fact defends it too.** A `TransactionScope` that rolls
  nothing back compiles perfectly, and a rule can only pin that the scope is opened, that the async
  flow option is on, that `Complete()` is reached on one branch, and that both contexts read the one
  connection string. What proves the behaviour is an integration fact: a registration the domain
  refuses after the account exists, and no account behind it afterwards. That fact did not exist
  until this record.

## Consequences

- ADR 0002's status names this record. Its body keeps the sentence, as every merged body does — the
  status line is where a reader learns it was overtaken (ADR 0039).
- The rollback stops being a claim and becomes a test the two integration suites run. ADR 0016's
  argument, which rests on it, is now standing on something.
- The conditions are written down, so the day one of them changes — a second database for Identity,
  a connection string of its own, a flow that holds both connections open — the reader who breaks
  the atomicity finds out from this record what they are breaking, instead of from a distributed
  transaction failing on a Linux host.

## Alternatives considered

**Leave ADR 0002 alone and correct the code to match it.** Making registration non-atomic to honour
a sentence would create the orphan accounts the scope exists to prevent, and would contradict five
other documents and ADR 0016. The record is what is wrong here, not the code.

**Amend by prose only, with no test.** The cheapest fix: annotate the status, correct nothing else.
Rejected — the whole argument of ADR 0013 is that a claim nothing keeps true has already been half
reversed, and "registration rolls back" was exactly such a claim, asserted in six places and proven
in none.

**Give Identity its own database.** The honest way to make the original sentence true, and a real
option for a system that wanted to scale the two apart. It would turn registration into a saga with
a compensating delete, which the context map already names as the cost. Not today, and this record
says what it would take.

## Verification

`Registration_RunsInOneAmbientTransaction` pins the scope, its async-flow option, the single
`Complete()` and the shared connection-string key in `AuthControllerBase`, and was made to fail by
commenting the completion out. `Register_WhenTheTrainerHalfIsRefused_LeavesNoAccountBehind` in the
shared TestKit runs on both hosts against a real SQL Server: a payload the Identity store accepts
and the domain refuses, then an attempt to sign in with those credentials, answered exactly as an
unknown username is.
