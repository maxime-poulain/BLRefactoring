# 0015 — Let each aggregate own the errors it raises

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

The shared kernel declared `InvalidTitle`, `DuplicateTitle`, `BioEmpty` and
`BioExceeds500Characters`. A project whose entire purpose is to know no business held the business
vocabulary of errors, and nobody had noticed — not the transversal audit, and not the architecture
suite that ADR 0013 introduced to catch exactly this class of thing. The dependency rule had been
applied to types and not to words.

It was found while examining `ErrorCode` for an unrelated reason: sealing it, per ADR 0014, turned
its `protected` constructor into a CS0628, which is the compiler saying that a base class nobody
inherits is not a base class. Reading it properly turned up three more facts.

The type was an `Ardalis.SmartEnum`, and the code consumed `.Name` and `.Value` at three call sites
and nothing else — never `List`, `FromName`, `TryFromName` or `FromValue`. Everything the library
exists for went unused.

The number was read by nobody. `ErrorCodeResponseHttp` does not appear in the generated client at
all, because since ADR 0012 errors travel as a `domainErrors` extension on a problem document, which
is untyped. The front end never reads a code. The single reader anywhere — one assertion in the
shared test kit — reads the name. Yet every new code had to be assigned a number to keep the field
fed.

And the contrast with `Topic` was hard to unsee. `Topic` is the other closed set in this domain, the
one that genuinely needs parsing from a string because the API takes a topic in a route, and it is
hand-written as a value object with its own `TryFromName`. The repository was using a library for
the case that did not need it and writing by hand the case that did.

## Decision

**The kernel defines what an error code is. Whoever raises it declares which ones exist.**

`ErrorCode` stays in the kernel, because `Result` carries `ErrorCollection` carries `Error` carries
a code, and that chain is the kernel's. It is now a value object over a single string.

The fourteen codes move to holder classes named `*ErrorCodes`, owned by whoever raises them:
`TrainingErrorCodes` and `TrainerErrorCodes` beside their aggregates, each code prefixed with its
owner — `Training.DuplicateTitle`. Three stay in the kernel — `Unspecified`, `NotFound`,
`ConcurrencyConflict` — and they carry no prefix, which is the point of them: "not found" is true of
any aggregate, so naming one would be a lie.

The prefix here is the aggregate rather than the bounded context, which is where this departs from
the usual formulation of the pattern. This repository has one bounded context with two application
stacks over it, so the context would name everything and distinguish nothing. The reasoning is
unchanged — the prefix is whatever guarantees that no two owners claim the same code — it just lands
one level down.

**The published contract becomes a string.** `errorCode` was `{ "name": "DuplicateTitle", "value": 2 }`
and is now `"Training.DuplicateTitle"`.

`Ardalis.SmartEnum` leaves the solution. The kernel now depends on exactly one third-party package,
`Mediator.Abstractions` — the one ADR 0012 argues about. One dependency, one justification.

## Consequences

**The set of codes is now open, and that is the real cost.** A smart enum could not be misspelled:
fourteen members existed and nothing else compiled. A string can be, and
`"Traning.DuplicateTitle"` would ship and be discovered by whoever was branching on the correct
spelling. `ErrorVocabularyRules` is what buys that back — every code declared on a holder, nothing
constructed at a call site, every aggregate's code carrying its name, the kernel's carrying none,
and no two codes sharing a value. It is a convention held by a test rather than by the compiler,
which is the trade this whole suite is about; it is worth being explicit that it *is* a trade.

**Equality improved on the way through.** The twenty-one `error.ErrorCode == X` comparisons used to
work because every smart-enum instance was a singleton — reference equality that happened to be
right. `ValueObject` overloads `==` and `!=` by value, so two codes carrying the same string are now
the same code whoever built them.

**Removing a field from a published contract is harsher than adding one**, and it was checked rather
than assumed: nothing typed models it, nothing reads it. The client is regenerated from the change,
which is what ADR 0008 exists for.

**The hosts now name a domain type where they named a kernel type.** A controller branching on
`TrainingErrorCodes.DuplicateTitle` reaches the domain, where before it reached the kernel. The
dependency was always there — a host can see the domain through its infrastructure — and the code is
doing exactly what it did before. Translating codes at the application boundary would remove it, and
that is a larger decision than this one.

## Alternatives considered

**Keep `Ardalis.SmartEnum` and only seal the type.** The smallest change, and the one this started
as. Rejected once the codes turned out to be in the wrong project: sealing would have preserved a
kernel that knows what a duplicate training title is.

**Replace it with a plain C# `enum`.** Switch exhaustiveness, no allocation, and the same removal of
the dependency. Rejected because an enum is a closed set again, in the kernel again — it solves the
library question and not the ownership one, which was the important half.

**Keep the numeric value.** It costs nothing to serialise. Rejected because it costs something to
maintain: every new code needs a number, chosen by hand, unique by hand, read by nobody.

**Declare the holders in the application layer instead of the domain.** It would keep the hosts from
naming a domain type. Rejected because the codes describe broken invariants, and the invariants are
the aggregate's — putting the vocabulary anywhere else repeats the mistake this record exists to fix,
one layer further out.
