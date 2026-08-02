# 0007 — Assert with AwesomeAssertions

- **Status:** Accepted
- **Date:** 2026-08-01

## Context

Assertions are the most repeated line in a test suite. This one holds **several hundred of them,
across every test file**, so the choice is not a detail of style: it decides what every test reads like, what a
failure tells you, and — as it turns out — what a reader inherits if they copy the pattern.

FluentAssertions had been the default answer in .NET for a decade. In early 2025 its version 8
moved to a commercial Xceed licence: still free for open-source and non-commercial use, but
requiring a paid licence for commercial development. Version 7, the last permissively licensed
release, stopped receiving work.

That matters here for a reason beyond this repository's own licence. This project is MIT and exists
to be read and lifted from — a reference implementation whose test style is part of what it
demonstrates. A reader who copies these tests into a commercial codebase would be copying a licence
obligation along with them, and would find out later rather than sooner.

There was also a second-order problem, which is what prompted this record: the choice was documented
in a comment in `Directory.Packages.props` and nowhere else. The shared test kit had drifted to
xUnit's `Assert` without anyone noticing, and the drift spread — a new shared test copied the file
next to it, which was the exception rather than the rule.

## Decision

**AwesomeAssertions**, the community fork of FluentAssertions 7 published under Apache 2.0, in every
test project including `BLRefactoring.Api.TestKit`.

The convention is now stated in the README's repository conventions, and this record holds the
reasoning: `subject.Should().Be(…)` everywhere, and no `Assert.*`.

The API is FluentAssertions' API, unchanged. That is the whole point of choosing a fork over a
different library: the 517 assertions did not move, and a .NET developer reads them without
learning anything new.

## Consequences

- A permissive licence end to end. Nothing in this repository obliges a reader to buy anything to
  use what they copy.
- Failure messages name the subject and the expectation — `Expected query.PageSize to be 20, but
  found 1 (difference of -19)`, an actual message from this suite's history, against xUnit's
  `Assert.Equal() Failure: Expected: 20, Actual: 1`. On a suite this size the difference is measured
  in minutes per failure.
- Switching again stays cheap **in one direction**: any FluentAssertions-compatible package is a
  version swap. Moving to a library with a different API is 517 lines.

Against that:

- **A fork is a bet on volunteers.** AwesomeAssertions has neither the history nor the contributor
  base of the project it forked. If it stalls, this repository is on a dead package again — with
  the consolation that the API is FluentAssertions', so the exit is a package swap rather than a
  rewrite.
- The name is not the one people search for, so a newcomer sees `Should()` and looks up
  FluentAssertions documentation. That works, which is both the convenience and the confusion.

## Alternatives considered

**xUnit's built-in `Assert`.** No dependency at all, no licence question, and already present. It
lost on what a failure says: `Assert.Equal` reports two values and names neither the subject nor
the intent, so the message has to be reconstructed from the line number. It is also the option this
repository accidentally half-adopted in the test kit, and the result was a suite that read two
different ways depending on the file.

**FluentAssertions 8 or later.** The mainstream choice, actively maintained, and free for this
repository as it stands — an MIT public project is non-commercial use. Rejected on what the
repository is *for*: its tests are meant to be copied, and a pattern that is free here and paid in
the reader's day job is a bad thing to teach silently. The licence question would have to be
explained to every reader instead of not existing.

**Pin FluentAssertions 7 forever.** Free, permissive, and zero migration — the last release before
the change. Rejected because a pinned dependency that will never receive a fix or a new target
framework is a deferred problem, not a decision. AwesomeAssertions is that same code with someone
maintaining it.

**Shouldly.** The strongest alternative, and the one that would win on grounds this decision
deliberately weighed against it: MIT, maintained independently rather than as a fork, an
established project rather than a bet, and a failure message quality comparable to
FluentAssertions'. It lost on migration cost — 517 assertions across 58 files, a mechanical but
unreviewable diff — and on familiarity, since `Should().Be()` is the idiom most .NET developers
already read without thinking. Worth revisiting if AwesomeAssertions stalls: at that point the
migration cost is being paid either way.

**NFluent, or a hand-rolled assertion helper.** Neither buys anything the above do not, and both
add something to explain.
