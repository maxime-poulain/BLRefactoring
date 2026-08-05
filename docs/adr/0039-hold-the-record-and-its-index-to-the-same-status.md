# 0039 — Hold the record and its index to the same status

- **Status:** Accepted
- **Date:** 2026-08-05
- **Amends:** [0013](0013-make-every-record-answer-to-a-test.md)

## Context

ADR 0013 made every record answer to a rule, and the coverage rules have held that line since. They
read two things from `docs/adr/`: the number in each record's heading, and the numbers in the
index's links. Everything else in a record — including the line that says whether the decision is
still in force and what has happened to it since — is parsed, carried, and compared to nothing.

The drift that followed is the ordinary kind. An audit found **thirteen of thirty-eight records
disagreeing with their own row in the index**, in both directions:

- The index annotates what the record does not say. ADR 0012, 0017, 0019, 0030 and 0033 each carry
  a bare `Accepted`, while the index says `amended by 0016`, `amended by 0018`, `amended by 0020`,
  `narrowed by 0036`, and — for 0033 — that its isolation arrives in 0034 and its poison gains a
  gauge in 0037. A reader who opens the record learns none of it.
- The record says more than the index. ADR 0011's own status reads `Accepted, amended (see
  Amendment)` where the index says only `Accepted`; ADR 0008's status carries a sentence the index
  compresses to four words; ADR 0024 and 0025 name 0031 and the index names 0031, 0033 and 0034.

Four records declare an amendment in a field — `- **Amends:** 0004`, `0012`, `0017`, `0019` — and
**one** of the four is acknowledged by the record it amends. The rest of the chaining lives in
prose that nothing reads: 0036 says it "narrows" 0030, 0034 reopens 0024, 0025 and 0033, 0037
extends 0033's poison. Each of those is a sentence a reader of the amended record never sees.

The same rot reached the exemption ledger this record's predecessor created. ADR 0013 says "Three
records are exempt today"; there are four, since 0027 joined without saying so. `UnguardedRecords`
opens by claiming "every one of these was merged before this suite existed" — which is false of
0027, merged two days after the suite.

## Decision

**A record's status is written once, in the record, and the index repeats it: an index that
annotates what the record does not say is a second source of truth.**

- **The record is the source; the index is a copy.** Where the two disagree today, the annotation
  moves into the record and the index repeats it. A reader who opens ADR 0030 must learn there that
  0036 narrowed it, without being expected to have come through the table.
- **The status line is living metadata; the body is frozen.** This is the distinction the
  convention needed and did not have. "A merged record is never rewritten" is about the argument —
  the context, the decision, the alternatives, all of it true as of its date and left alone. What a
  later decision changes is the *standing* of the record, and standing belongs on the status line,
  which is why several records already carry one that was edited after merge. Saying so out loud is
  what stops the convention being read as a ban on maintaining the index of one's own decisions.
- **An amendment is declared on both sides.** A record that carries `- **Amends:** NNNN` obliges
  NNNN's status to name it back. The reciprocity is the point: the amending record is easy to find
  from the amended one only if the amended one says so.
- **The comparison ignores markup, not substance.** Records link the records they cite —
  `[0029](0029-….md)` — and the index does not. The rule flattens a link to its number before
  comparing, so the two files may differ in how they spell a reference and in nothing else.
- **The ledger of exemptions is held to its own claim.** `UnguardedRecords` says an excuse cannot
  outlive its reason; three of its four reasons had already outlived themselves, each claiming that
  no *type-level* rule could see the decision while the suite had long since learnt to read source
  files and MSBuild properties. Those three become rules. The fourth stays, with a reason rewritten
  to what is still true of it.

The exemption arithmetic inside ADR 0013 is not corrected in place — that is body, and the body is
frozen. This record amends it: the ledger holds what it holds, the rules read it, and the number in
0013's prose is true as of the day it was written.

## Consequences

- Thirteen status lines change, and from now on a fourteenth cannot: `EveryRecordsStatus_IsRepeatedByTheIndex`
  compares all thirty-eight on every build. Adding a record means writing its status twice, in the
  two places a reader looks — which is the cost, and it is one line.
- Three records leave the exemption ledger and gain a rule: 0003 (migrations applied inside the
  Development branch and nowhere else), 0005 (`HasPrecision(7)` on every hand-configured audit
  column, including the two outbox configurations the existing persistence scan never reached), and
  0027 (the output template names the enricher's property, and the enricher is wired).
- The ledger keeps ADR 0009, whose claims are about what travels over the wire and are held by
  `BffTests`. Its stated reason loses a false clause — the suite does reference the BFF's assembly,
  and two rules already read its `Program.cs` by path — and keeps the true one.
- The index's conventions gain the shape they always used: a status is a claim, optionally followed
  by what later records did to it. Seven records and thirteen index cells already wrote it that way
  against a sentence that admitted only three literals.

## Alternatives considered

**Let the index own the annotations.** The record would carry a bare claim — `Accepted` — and the
index alone would say what happened since. Rejected: the index is a table one scrolls past, and the
record is what a reader opens after following a link from the code. Putting the history where only
the table shows it makes the record the last place to learn its own standing.

**Strip the annotations and compare bare claims.** A rule comparing only `Accepted` /
`Superseded by NNNN` would be trivial to write and would have caught none of the thirteen
divergences, since every one of them is an annotation. It would guard the case that has never
happened and ignore the one that has.

**Generate the index from the records.** A script, and the drift becomes impossible. Rejected for
the reason ADR 0038 rejected generating the counted sentences: a build step and a template for one
table of thirty-eight rows, in a repository whose documents are written to be read.

**Correct ADR 0013's arithmetic in place.** One word, and the sentence would be true. It would also
be the first rewrite of a merged record's body in this repository, for a number that a rule now
computes anyway.

## Verification

`EveryRecordsStatus_IsRepeatedByTheIndex` was red on thirteen records before a single status line
moved, and `EveryAmendment_IsDeclaredByBothRecords` on the three amendments that only one side
declared; both captures are in the pull request. Each was then broken on purpose and watched to
fail — an index annotation edited away from its record, an `Amends` field pointed at a record whose
status does not name it back. The three rules replacing exemptions were proven the only way their
shape allows: a rule and its ledger entry cannot coexist, since `NoRecord_IsBothDefendedAndExcused`
fails on the pair, so each arrived with its entry deleted and was then made to fail by moving a
`MigrateAsync` out of the Development branch, deleting a `HasPrecision(7)`, and taking `{User}` out
of the log template.
