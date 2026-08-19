# 0092 — Hold a document's claims of absence to the code

- **Status:** Accepted
- **Amends:** 0038
- **Date:** 2026-08-19

## Context

ADR 0038 made a counted claim answerable to the code, and ADR 0041 did the same for a named list.
Between them they cover what a document *asserts*: how many handlers there are, which facts feed a
port. Neither covers what a document *denies*, and an audit of the strategic design found nine
statements the code contradicts. Five of them are denials.

The worst is the shape of the failure rather than its size. `bounded-contexts.md` carried this, in
a bulleted list under the heading *What this context deliberately does not do*:

> **It does not serve a catalog.** Every read is scoped to the caller.

It had been false since ADR 0062 opened the anonymous reads, and by ADR 0074 the catalog was the
application's front door — the root address serves it. Eleven merges passed. The strategic design's
own entry point said the same thing in its own words, *there is no browsing, no directory, no
search*, and called the public catalog "the announced next step" while the pipeline built images of
it every night.

A denial ages differently from an assertion, and that difference is the whole of this record:

- **An assertion that goes stale becomes incomplete.** "Nine facts feed the index" reads as
  suspicious the moment a reader counts ten, and ADR 0041 catches it before a reader has to.
- **A denial that goes stale becomes a lie that reads as a decision.** Nothing about *it does not
  serve a catalog* looks out of date. It looks argued — it was written under a heading claiming it
  was deliberate — so the next reader does not check it, they inherit it. A contributor who
  believed that sentence would have proposed the catalog as new work, and a reviewer trusting the
  same sentence would have agreed.

The other three mechanisms could not have caught it. There was no number to compare and no list to
enumerate; the sentence is a boolean about the shape of the system, and nothing in this repository
had ever asserted on one of those.

## Decision

**A claim of absence the documentation makes about the code is derived from the code, or it is not
made.** A third ledger joins the two `DocumentationRules` already carries, and
`EveryClaimOfAbsence_AgreesWithTheCode` walks it.

- **A row is a denial, an anchor and a predicate.** The document, the sentence to anchor on, the
  subject in English, and a `Func<bool>` computing from the model whether the denial still holds.
  The shape is ADR 0038's on purpose: a ledger rather than a sweep, because a rule hunting every
  negation in every document would trip over "no aggregate holds another" and over every sentence
  describing what a port does not take.
- **It fails the same two ways as its siblings**, and the second is the one worth stating: a denial
  whose predicate has turned false, and an anchor that has stopped matching. Rewording a guarded
  sentence is a build failure by design — a guarantee quietly rephrased is a guarantee quietly
  withdrawn, and the rule cannot tell the two apart, so it refuses both.
- **The first four rows are the denials that are still true**: a training that is described rather
  than scheduled (no date, no session, no capacity, no price), a catalog nobody enrolls in (no
  `Participant`, no `Registration`, no `Attendance`), stated in two documents, and a trainer who
  comes into being through registration or not at all (`POST /Trainer` does not exist).
- **A row is worth writing when the denial is load-bearing** — when it explains a shape a reader
  would otherwise take for an omission. That is the same test ADR 0038 applies to a number, and it
  is what keeps the ledger from becoming an inventory of every sentence containing *no*.
- **A denial another rule already enforces stays out.** `IObjectStore` offers no `Replace`, and
  `ObjectStorageRules.TheObjectStore_OffersNoWayToOverwriteInPlace` fails before any document could
  go stale about it. Two rules for one fact is a second place to forget.

The sentences the audit found are corrected in the same commit, and the correction is not symmetric
with the failure: *it does not serve a catalog* is replaced by what is true, and what replaced it is
held by a **counted** claim — the catalog's seven anonymous endpoints, computed off
`CatalogControllerBase`. An affirmation is not a denial and does not belong in this ledger; putting
it in the right one is the point.

Three counted claims are added beside it, all three of them stale when this record was written: the
closed set of topics, in the two documents that state its size (six, since ADR 0079 made it
sixteen); the consumers of `IEmailSender` (two, where there are ten); and the endpoints under
`/Administration` (six, where there are eight). And one named list: what
`EmailVerificationRequestedIntegrationEvent` carries, which the event-storming board still described
as a culture that ADR 0091 had removed — the newest record contradicted by a living document, with
the event's own remark saying so three lines from the code.

## Consequences

- **A document may now fail the build for a sentence with no number in it.** That is new, and it is
  the reason this is a record rather than three more rows.
- **Adding a denial to a living document costs a row**, or it is not checked. Same bargain as
  ADR 0038's, same argument: the cost is what makes the guarantee real, and a claim nobody checks is
  worse than a wrong one because nothing will ever say so.
- **A predicate is code, so it can be wrong.** Each is written to fail loudly rather than vacuously
  — `EndpointsOn` throws when a controller base matches nothing, rather than counting zero and
  making every claim about that surface true by there being no surface.
- **The ledger does not cover the records.** A merged record is never rewritten, so the denials
  frozen inside one are true as of its decision and are allowed to age. That is ADR 0038's exclusion
  and it applies unchanged.

## Alternatives considered

**Correct the nine sentences and add no mechanism.** Fifteen minutes, and the honest reading of the
audit is that this was the tenth time. The strategic design has been corrected by hand at least four
times before — ADR 0023 built its rules, ADR 0038 and ADR 0041 built the two ledgers, and ADR 0059's
merge fixed six sentences at once. Each pass corrected what it found and left the class of failure
open. A tenth pass would have been the same trade.

**A general negation sweep.** Find every sentence containing *no*, *never*, *does not*, and demand a
predicate. It would flag the domain's own prose about what a port does not take, the records'
argument sections, and every rule remark in the suite — hundreds of matches, nearly all of them
correct English rather than claims about the code. ADR 0038 rejected the same idea for the same
reason and the ledger is what it chose instead.

**A `<!-- checked-by: … -->` comment beside each guarded sentence.** Keeps the row next to the prose
it guards, which is genuinely better for a reader. It also puts executable configuration inside a
document, invisible in rendered Markdown, and makes the rule parse the file for its own instructions
— a document that configures the rule that checks it. The ledger sits in the suite where every other
rule's population sits.

**Assert the denials as code rules rather than document rules.** "No domain type is named
`Participant`" is a perfectly good architecture rule and needs no document. But then the *document*
is still unguarded: it could go on denying a catalog while the code rule about participants passed.
What is being defended here is the sentence, not the shape — the shape has its own rules already.

## Verification

`EveryClaimOfAbsence_AgreesWithTheCode` was proven red before the documents were touched, on the
real defect and by no mutation: a row pointing at *it does not serve a catalog*, with
`EndpointsOn("CatalogControllerBase") == 0` as its predicate, reported

> docs/strategic-design/bounded-contexts.md still denies a context that serves no catalog, and the
> code no longer agrees.

That row was then removed with the sentence it named, and the four surviving denials took its place.
The anchor guard is checked the same way its siblings' is — a guarded sentence reworded fails,
naming the row.

`EveryCountedClaim_AgreesWithTheCode` was red on the three new numbers before they were corrected,
naming each: *'six' where the code has 16 topics*, *'two' where the code has 10 consumers of
IEmailSender*, *'six' where the code has 8 endpoints under /Administration*.
`EveryNamedList_AgreesWithTheCode` was red on the verification fact's row. The captures are in the
pull request.

`EveryWordThisRepositoryWrites_UsesAmericanSpelling` gained the two British forms of *enroll* the
dictionary was missing: it knew the noun *enrollment* and not the verb that noun is built on, which
is how one sentence of the strategic design carried a British spelling under a rule that reads every
word of it. Red on that line before the word was changed.

This paragraph deliberately does not spell either refused form, and the reason is worth the line: a
record is read by that rule like every other document, and the first draft of this one was red for
quoting the two words it had just taught the dictionary. `AmericanSpellingRules.cs` is invisible to
itself precisely because it is the one file that must write them down (ADR 0066); a record is not,
and describing a refused spelling is the way to name one here.
