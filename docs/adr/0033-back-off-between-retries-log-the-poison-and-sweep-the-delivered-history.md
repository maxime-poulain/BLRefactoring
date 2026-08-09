# 0033 — Back off between retries, log the poison, and sweep the delivered history

- **Status:** Accepted — the per-consumer isolation it left out arrives in [0034](0034-deliver-once-per-consumer-not-once-per-message.md); the poison gains a pollable gauge in [0037](0037-answer-for-the-hosts-health-at-two-endpoints.md); the dead-letter surface it deferred is built by [0061](0061-give-the-poison-a-url-and-an-operator-a-way-back-in.md)
- **Date:** 2026-08-05

## Context

ADR 0024 carved the retry contract into the envelope — every attempt counts, the last failure is
kept beside the message it poisoned — and ADR 0025 gave it a worker: claim under lease, deliver,
save the outcome per message, give up after `MaxAttempts`. Living with that machinery exposed
three weaknesses, all of the same species: the mechanism is correct and the operations around it
are not.

**The budget burns back to back.** A failed attempt releases the lease and nothing else, so the
row is claimable again the instant it is saved — and the worker's drain loop claims until the
table is quiet, so it re-claims that row on its very next pass. Five attempts against a
downstream dependency that is down do not probe it five times over minutes; they hammer it five
times within seconds. The arithmetic is unforgiving: under the defaults, any outage longer than
roughly the time it takes to fail five deliveries — well under a minute — turns every in-flight
message into poison that no recovery will ever redeem.

**Poison is silent.** ADR 0025 said the quiet part honestly: "a poison message halts nothing and
alerts nobody — it waits in the table for an operator," and deferred the dead-letter surface —
"an endpoint, a metric, an alert" — until it is wanted. But the processor writes that transition
with no logger at all: the one log line in the whole mechanism belongs to the worker and fires
only when a *drain* fails. A committed business fact the system has given up delivering deserves
at least a sentence in the log, and today it does not get one.

**The history never ends.** Delivered rows keep `ProcessedOnUtc` and stay forever. The filtered
index keeps the claim fast regardless — 0024 said so and it remains true — but the table itself
grows without bound, and 0024's own versioning promise ("entries outlive the events they name:
the old entry stays until no stored message carries it") can never come true in a table where
every message is stored forever.

## Decision

**The envelope learns to wait, the processor learns to speak, and the worker learns to sweep.**

- **The retry schedule is the envelope's third column.** `RecordFailure` now takes the clock and
  a base delay, and writes `NextAttemptOnUtc = failedOn + delay × 2^(attempts−1)` beside the
  count and the reason — the schedule lives where 0024 put the rest of the retry contract. The
  claim refuses rows whose next attempt has not come due, which is the whole fix: the drain loop
  needs no change, it simply stops seeing the row until the schedule says otherwise. The base is
  `OutboxOptions.RetryDelay`, thirty seconds by default, spreading five attempts over roughly
  eight minutes: an outage shorter than that now heals itself. Delivery nulls the schedule — a
  delivered message waits for nothing. No cap and no jitter: with five attempts and two competing
  hosts, both are machinery without a customer. The claim keeps its one filtered index; the new
  predicate joins `Attempts` and `ClaimedUntil` as residual filters over the owed set that index
  keeps tiny.
- **Failure is logged where it is recorded.** The processor gains the logger it never had. An
  ordinary failed attempt logs a Warning naming the message, the spent budget and when it will be
  tried again; the attempt that exhausts the budget logs one Error with the exception, because
  poisoning is the moment the system gives up on a committed fact. This is deliberately the
  smallest surface 0025's deferral permits — a line, not an endpoint, not a metric, not an alert.
  That deferral is narrowed by this record, not contradicted: the rows an operator would read
  still exist, and now the log says when to go read them.
- **Delivered history is swept, poison never is.** Once per poll, after the drain, the processor
  deletes rows delivered longer ago than `OutboxOptions.RetentionPeriod` — fourteen days by
  default, long enough to audit, short enough to bound the table. A new filtered index over the
  delivered rows (`IX_OutboxMessage_Delivered`) makes the sweep a range seek that finds nothing
  almost every time, which is what makes running it every poll defensible. Undelivered rows —
  poison included — are never swept: a poison row is an operator's evidence, and deleting it
  would be the mechanism destroying its own crime scene. The sweep is also what lets 0024's
  versioning promise terminate: an event entry outlives its stored messages, and now stored
  messages have a lifespan.
- **A knob that binds from configuration is validated at start-up, so a wrong value refuses the
  host rather than surfacing as the worker's first failure.** `OutboxOptions` was the one bound
  section with no validation; it joins `SmtpOptions` and `ObjectStorageOptions` — named section
  constant, every knob checked positive, `ValidateOnStart`. The defaults all pass, so a host with
  no `Outbox` section keeps starting and keeps delivering.

The rule `EveryBoundOptions_IsValidatedAtStartup` defends the last thesis — and with it the
knobs the first three stand on: every options binding in the backend must validate at start-up,
which is exactly the check the outbox failed until this record.

## Consequences

- A downstream outage under about eight minutes self-heals: the budget that used to burn in
  seconds now probes on a doubling schedule. The price is latency on genuine one-off failures — a
  message that would have succeeded on an immediate retry now waits thirty seconds. Accepted:
  delivery was already "eventual, by seconds" under 0025, and eventual-by-a-little-more buys the
  outage survival.
- The poison transition lands once, at Error, in every sink the hosts write — console, file, and
  the test kit's recording provider, which is what makes it assertable in the integration suites.
- The table is bounded: fourteen days of delivered history plus whatever poison waits for an
  operator. An operator who needs older history has fourteen days to copy it somewhere that is
  not a queue.
- The integration suites shrink the new knobs the way they already shrink the poll interval:
  a hundred-millisecond `RetryDelay` keeps the poison proof inside a test's patience, a
  thirty-second `RetentionPeriod` lets a planted stale row be swept while fresh rows survive.
- `dotnet ef` still boots the layered host at design time; the defaults being valid is what keeps
  `ValidateOnStart` from turning migrations into a configuration exercise.

## Alternatives considered

**A retry library (Polly) or a job scheduler (Hangfire) for the backoff.** Rejected on ADR 0025's
own grounds: their retry machinery is a second ledger for a question the envelope already
answers, each ignorant of the other. The schedule is one column and one predicate; a dependency
would be heavier than the feature.

**A dead-letter endpoint, metric or alert.** Still deferred, exactly as 0025 left it. The log
line is the floor, not the ceiling: it costs one logger and answers "when did we give up" in
every sink that already exists. The day somebody wants a surface with a URL, the rows are still
there and this record's log line will have been its prototype.

**Infinite retention.** The status quo, rejected because it makes 0024's versioning promise
unfulfillable and the table a landfill. The counter-risk — sweeping evidence an operator wanted —
is answered by the boundary, not the knob: nothing undelivered is ever swept.

**A second hosted service for the sweep.** A janitor process for the same table the delivery
worker already owns would reopen the scheduler argument 0025 closed with "this system has one
queue, and it is already a table." The worker that writes the history is the right owner of
trimming it; the sweep is one method on the processor, called after each drain.

**Jittered or capped backoff.** The textbook completions of an exponential policy, useful when
many competing consumers thunder against a recovering dependency. This system has two hosts and
a five-attempt budget; both refinements are recorded here as the first thing to reach for if the
fleet ever grows.

## Verification

`OutboxMessageTests` prove the schedule in memory: the first failure books the base delay, the
second doubles it, delivery clears it. `OutboxTest` in the shared TestKit proves the rest against
SQL Server on both hosts: the poison proof now also asserts the schedule was written and the
Error line reached the recording provider, and a new fact plants delivered rows on both sides of
the retention boundary and watches the sweep take exactly the stale one — and never the poison.
`EveryBoundOptions_IsValidatedAtStartup` holds the validation floor, and was red on the outbox's
own binding before this record's change made it green — the rule bit its reason for existing.
