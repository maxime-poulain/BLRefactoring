# 0061 — Give the poison a URL, and an operator a way back in

- **Status:** Accepted
- **Date:** 2026-08-09

## Context

Three records deferred the same thing, each promising the next one could take it.

ADR 0025, on introducing the delivery worker: *"A dead-letter surface (an endpoint, a metric, an
alert) is deliberately not designed here; the day it is wanted, the rows it would read already
exist."* ADR 0033, on hardening the retries: *"**A dead-letter endpoint, metric or alert.** Still
deferred, exactly as 0025 left it."* ADR 0037, on the health endpoints: *"The dead-letter surface
with a URL — a list, a requeue — remains deferred exactly as 0025 and 0033 left it; this gauge is
the pollable half of 0033's log line, one notch further, still not that endpoint."*

It is the most-repeated open item in this repository, and the deferral was right each time: nobody
had asked. This record is the day somebody did.

**What exists today.** A message is poison when its retry budget is spent and it is still
undelivered — `ProcessedOnUtc IS NULL AND Attempts >= MaxAttempts`. The claim query excludes exactly
that set, so the worker never touches it again; the retention sweep removes delivered rows only,
because *"deleting it would be the mechanism destroying its own crime scene"*. So the row stays, and
what an operator has is one `Error` log line at the moment of the transition and a `Degraded`
readiness probe that counts how many there are. Neither says **which**, and nothing says **retry**
short of an `UPDATE` typed against production.

**What the work uncovered, and did not expect.** A requeue cannot simply put the attempt counter
back to zero. `OutboxProcessor` short-circuits the delivery ledger on that exact value:

```csharp
if (message.Attempts == 0)
{
    return new HashSet<string>();
}
```

The comment gives the reason and it was true — *"no attempts means no rows"* — because until now
nothing could lower the counter. A requeue that did would make the processor skip a ledger that has
rows in it, and re-run consumers that already succeeded: a second welcome email for one operator's
retry, which is the guarantee ADR 0034 exists to give, destroyed by the feature meant to make it
useful. The defect belongs to this change rather than being inherited, so it is settled here.

## Decision

**A poison message gets a URL an administrator can read and one verb they can press, and the row
keeps everything that makes pressing it safe.**

- **Two operations and no third.** `IOutboxOperations` declares a paged read and a requeue. There is
  no discard, no delete, no way to edit a payload: the honest verbs over evidence are *try again* and
  *leave it alone*, and a delete would hand the mechanism's own crime-scene deletion to a person, one
  click from a listing that shows how noisy the backlog is. A rule holds the pair closed.
- **The requeue is a transition on the envelope.** `Requeue` resets the budget, clears the schedule
  and the lease, and stamps `RequeuedOnUtc`. The last error is deliberately **kept**: it is what the
  operator was looking at when they decided, and the next attempt overwrites it by itself.
- **The attempt counter stops being the ledger's short-circuit.** The envelope answers
  `MayHaveSettledConsumers`, which is `Attempts > 0 || RequeuedOnUtc is not null`, and the processor
  asks that instead. The question lives beside the data it is about, where a fast test can reach it —
  `OutboxProcessor` itself is testable only against SQL Server, since its claim is an
  `UPDATE … OUTPUT` with `READPAST`.
- **`RequeuedOnUtc` is a new nullable column**, added by `20260809160000_AddOutboxRequeue`. It is
  needed for correctness rather than for audit: without it the short-circuit cannot tell a fresh row
  from a requeued one. That it also tells an operator the row has been retried once already is a
  second use, not the reason.
- **The payload is not published.** Several integration events carry an address a person is reached
  at. Deciding whether to retry needs what failed, when, and why; none of those is the content. The
  `Error` string is published, stack trace included, which is one of the reasons this is behind the
  administrator and nowhere else.
- **The listing names the consumers already settled.** One extra statement per page, an `IN` over at
  most one page of identifiers. It is the column that makes a requeue a decision rather than a
  gamble: it says exactly what the retry will *not* run again.
- **It lives under `/Administration`, on that base class.** The administrator is an authority rather
  than a context (ADR 0051), and this is that authority applied to the platform instead of to the
  domain. A fifth controller base would have differed from the fourth by a route prefix and nothing
  else.
- **A screen, not only an endpoint.** `/administration/outbox` lists the backlog and offers the
  retry. An operator surface reachable only by `curl` is the shape this repository already regrets
  elsewhere.

## Consequences

- **Three records' deferral is closed and their statuses say so.** ADR 0025, ADR 0033 and ADR 0037
  each predicted this surface; none is amended, because none of them was wrong. What they deferred
  arrived.
- **One holder of error codes names no aggregate.** `OutboxErrorCodes.Outbox.NotPoison` is the first
  code whose owner is a table rather than an aggregate, and ADR 0015's rules bend accordingly: the
  prefix rule they enforce covers the domain assembly, and the argument behind it — two owners never
  collide — is what the prefix here keeps true.
- **A rule that could not fill a date was filling nothing.** `EveryMappingToAPublishedContract_AssignsEveryMember`
  builds a source object with every member set to something unlike a default, and it had never met a
  `DateTime` or a read-only list: it threw instead of reporting. Both are now fillable, which makes
  the rule check more than it did before this record rather than less.
- **Two suites now assert what their own names claimed.** `AMessageNobodyCanRead_SpendsItsBudget_AndIsLeftPoisoned`
  never checked that the attempts stop climbing, and nothing anywhere observed the poison probe in
  any state but healthy. Both gaps predate this record and are closed by it.
- **The requeue is idempotent in the way that matters.** Pressing it twice cannot send a second
  welcome email, because the ledger's rows outlive it. That is a property of the ledger rather than
  of this surface, and this surface is what finally depends on it.
- **The alert is still not built.** ADR 0025 named three things — an endpoint, a metric, an alert.
  The endpoint is here, the metric has been here since ADR 0037, and the alert belongs to whoever
  runs this rather than to the code that emits it.

## Alternatives considered

**Leave it deferred a fourth time.** Defensible for as long as nobody asks, and somebody has. The
row already holds everything the surface needs, which is exactly what the three earlier records kept
saying.

**Delete rather than requeue.** The operation an operator reaches for when the backlog is noisy, and
the one that loses the fact permanently. ADR 0033 argues it about the sweep and the argument does not
weaken because a person is doing it by hand.

**Clear the ledger on a requeue, so the retry runs everything again.** Simpler to explain and wrong
in the one case that matters: the message that poisoned because one consumer of five kept failing.
Replaying the other four is a duplicate welcome email, and the ledger exists to make that impossible.

**Delete the processor's short-circuit instead of making it exact.** One `SELECT` per message's first
delivery, forever, to avoid one nullable column. It also moves the invariant out of the envelope and
into a private method, where nothing fast can reach it.

**Requeue every poison message at once.** A button an operator would press during an incident, and a
thundering herd against whatever dependency was failing. One row at a time is the shape that keeps
the retry a decision.

**Publish the payload in the listing.** It would help diagnosis and would put contact addresses on a
screen. The name, the version and the error say what failed; anybody who needs the payload has the
database.

## Verification

- **`TheOperatorsSurface_OffersNoWayToForgetAMessage`**, watched failing first with a `DiscardAsync`
  added to the port — the exact shape a helpful pull request would take.
- **`MayHaveSettledConsumers_SurvivesTheRequeue_ThatResetsTheCounter`**, written before the property
  was exact and watched failing against the naive translation of today's condition. It is the fact
  the whole design turns on.
- **`OutboxOperationsTests`**, against SQLite through the real model: the selection takes spent
  budgets and nothing else, the order is the worker's, the page is bounded, the settled consumers
  are joined correctly, and the requeue gives back the budget **while leaving the ledger alone**.
  A message nobody stored and one that is not poison are each refused with their own code.
- **`EveryMappingToAPublishedContract_AssignsEveryMember`**, watched failing on the new translation
  once the rule could fill the members it previously threw on.
- **The two hosts, over HTTP**, in `OutboxOperatorSurfaceTest`: a planted poison row is listed with
  its error and its settled consumers, a requeue is followed by an actual delivery, a message nobody
  stored answers 404, a delivered one answers 409, and a trainer is answered 403 on both actions.
- **`AMessageNobodyCanRead_SpendsItsBudget_AndIsLeftPoisoned`**, which now checks that the counter
  stops at the budget and that readiness reports `Degraded` while the row sits there.
- **`dotnet ef migrations add Probe` could not be run in the environment this was built in**
  (`dotnet-ef` is absent from `.config/dotnet-tools.json`, and no workflow runs it either). The
  snapshot is hand-edited against the block `AddOutboxBackoffAndRetention` wrote for the same table
  and re-read line by line; ADR 0005's check remains a manual one.
