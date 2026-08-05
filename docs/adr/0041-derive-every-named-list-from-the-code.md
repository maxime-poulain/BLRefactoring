# 0041 — Derive every named list from the code

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

ADR 0038 answered one half of a question. A number the documentation states about the code is now
derived from the code, anchored on the sentence that carries it, and a rule computes the truth. The
other half went unasked: the documents do not only count, they **enumerate**.

`docs/strategic-design/` is the one place in this repository that claims to describe the model *as
it stands* — "Everything below is read off the model as it stands; where a boundary is intended
rather than built, it says so." Four rules hold parts of that claim: an aggregate is placed in
exactly one context, a domain event appears on a board, the map and the sections name the same
contexts, and their states agree. Nothing held what those documents list.

The transfer proved it. ADR 0036 added a third fact to the search-indexing seam —
`TrainingTransferredIntegrationEvent`, consumed by `ReindexTrainingWhenTrainingTransferredIntegrationEventHandler`
beside the create and edit consumers — and the documents went on describing two:

- `context-map.md` contains no occurrence of the word *transfer* at all. Its Search Indexing seam
  names two facts and "two policies"; its *Already there* table says the index is "maintained on
  every create and edit"; its Catalogue Discovery section names the two facts a subscriber would
  read.
- `bounded-contexts.md` names the transfer in one place — the use-case table, corrected by hand
  under ADR 0038. Its Search Indexing section and its Catalogue Discovery section each name two
  facts.
- `event-storming.md` opens a paragraph with **"Two events, two facts, one future consumer."**

Six sentences, one omission, and no test with anything to say. A count going stale is visible to a
reader who recounts; a list going stale is invisible even to one who does, because a list that is
short by one still reads as complete.

The mechanism was already built. What was missing is that ADR 0038 decided about numbers, in those
words, and a decision is not stretched to cover what it did not decide.

## Decision

**A list of code the documentation states is derived from the code, or it is not stated: every
named list anchors on the sentence that carries it, and a rule computes its members.**

- **A second ledger, beside the first.** `DocumentationRules` gains a table of named lists — the
  document, the anchor, the subject in English, and the function that computes the members. The
  anchor captures the enumeration itself as a group, and the members are read from it as the
  backticked names the prose already writes. Comparison is set equality, in both directions: a
  missing member is an incomplete claim, and an extra one names something the code does not have.
- **An anchor that matches nothing fails.** The same anti-vacuity discipline ADR 0038 established,
  for the same reason: rewording a guarded sentence has to move the claim with it, or the claim
  leaves the guarded set without anybody deciding it should.
- **A ledger, not a sweep — and this was measured, not assumed.** The obvious rule is "a paragraph
  naming one integration event names them all". Run against these documents it is wrong twice:
  `README.md` names `TrainerCreatedIntegrationEvent` alone to illustrate what a fact should be
  *called*, and `event-storming.md` names `TrainerContactEmailChangedIntegrationEvent` alone to
  explain why the previous address can be warned. Neither is an enumeration. The anchor is what
  tells an enumeration from a mention, and no heuristic replaces it.
- **The truth comes from reflection.** The facts that feed a context are read from the consumers:
  each `IIntegrationEventHandler<TEvent>` implementation is grouped by the port its constructor
  takes, so `ITrainingSearchIndexer` answers with three events and `IEmailSender` with two. Nothing
  is pinned, and a fourth consumer changes the answer without anybody editing the ledger — which is
  the whole point, since a consumer arriving is exactly the merge that made these sentences wrong.

**Where this stops.** A rule derives what the code declares. It does not derive an invariant, an
actor or a business capability — those are the sentences a human writes about what the model
*means*, and this record does not pretend otherwise. The transfer's absence from the Training
Catalogue's invariants, actors and capabilities is repaired by hand in the commit that introduces
this rule, and the repair is not defended by anything. Saying so is more honest than a rule shaped
to look like it covers them.

## Consequences

- Four enumerations change in the commit that introduces the guard, and two counted claims move
  with them — the mechanism proving itself on its first run, as ADR 0038's did.
- Rewording an enumerating sentence costs a moment, exactly as rewording a counting one does.
- The documents can now be read as what they claim to be: a list of the facts that feed a context
  is the list of the facts that feed it, or the build fails.
- The two ledgers sit in one class and share the anchor discipline, the whitespace normalisation
  and the `Selected` guard. A third kind of claim — should one arrive — has a shape to follow.

## Alternatives considered

**Extend ADR 0038's ledger with list rows.** One table, two kinds of row, one rule. Rejected on the
record rather than on the code: 0038 decided about *a number*, in those words, and the rule that
defends it quotes that sentence. Widening a decision by widening its rule is how a record stops
describing what was decided — the objection 0038 itself raised against hanging its rules on 0023.

**Hang the rule on ADR 0023.** Every enumeration this ledger holds today is in a strategic-design
document, and 0023 is the record that holds those documents to the model. Rejected for the same
reason 0038 was not hung there: the decision is about any living document that lists code, and the
first row outside `docs/strategic-design/` would make the citation false. The two rules added to
`StrategicDesignRules` in the same commit — the ubiquitous language, the domain services — *do*
cite 0023, because those are about the domain the documents describe.

**Generate the enumerations.** A template rendering the fact lists from the code would make the
drift impossible. Rejected as 0038 rejected it: these documents are written to be read as prose,
and the sentence around the list carries the argument. The ledger costs one row.

**Say less.** "The facts that feed it" instead of naming them. Rejected: the enumeration *is* the
value here — a reader following the seam wants the three names to grep for, and the map's own
premise is that a relationship you cannot point at is a relationship nobody is keeping.

## Verification

`EveryNamedList_AgreesWithTheCode` walks the ledger, resolves each anchor, reads the members out of
the matched span and compares. It was red first, on the four enumerations that omitted
`TrainingTransferredIntegrationEvent`, and the capture is in the pull request. It was then broken on
purpose and watched to fail in both directions — a member removed from a corrected list, a member
added that no consumer declares — and the anchor guard proven by rewording a sentence the ledger
points at.
