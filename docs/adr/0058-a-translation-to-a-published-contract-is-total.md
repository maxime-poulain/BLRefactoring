# 0058 — A translation to a published contract is total

- **Status:** Accepted
- **Date:** 2026-08-09

## Context

`ApplicationToHttpMappings` turns what the application layer answers into what the API publishes.
Four methods, each an object initialiser, each a field-by-field copy from a `*Dto` to a
`*HttpResponse`.

Twice in eight pull requests, a member was added to both sides and **the copy between them was
forgotten, and compiled**:

- a training row learned its owner's name, and the mapping did not carry it — the API would have
  served `null` on the column that whole change existed to add;
- three members in the following change — a trainer's standing, their suspension reason, a
  training's withholding reason — caught only because the author had written themselves a note to
  check by hand.

The mechanism is not carelessness, it is the language. **A member that is not `required` is not
missed by an object initialiser.** The compiler is satisfied, no test looks, and the omission
surfaces as a `null` on a screen — or not at all, since a client reading a nullable field cannot
tell "absent because the state says so" from "absent because nobody copied it".

Nothing in this repository was watching. The contracts have rules about what they are named
(ADR 0048) and which types belong on the boundary (ADR 0042); none is about whether a translation
into one is complete.

## Decision

**A method that translates a read model into a published contract assigns every member of that
contract, and a rule runs the translation to find out.**

- **`EveryMappingToAPublishedContract_AssignsEveryMember` executes rather than reads.** It fills the
  source read model with values nothing like a default, invokes the translation, and asks whether
  any member of the result came back at one. A forgotten member *is* a member left at its default,
  so the claim is about behaviour rather than about punctuation.
- **Not by parsing the initialiser.** This suite carries no Roslyn, and a regular expression over an
  object initialiser would assert how the code is written rather than what it produces — it would
  pass a mapping that built the contract with a constructor, a `with` expression or a helper, and
  fail a correct one that wrapped a line. Running it is indifferent to all of that.
- **The population takes itself from the signatures**: a public static method taking one `*Dto` and
  answering one `*HttpResponse`. A translation added tomorrow joins by existing, which is the
  direction that fails safely — the opposite, a list of methods to check, is a list somebody forgets
  to extend on the day it would have mattered.
- **The source is filled, so a legitimate `null` is never a false positive.** A withholding reason
  is absent when a training was not withheld — but the rule hands the mapping a source where it is
  present, and a correct mapping therefore produces it. What the rule sees at a default is only ever
  something the translation dropped.

## Consequences

- **Four translations are held today**, and the number is not written down anywhere: the rule counts
  them itself.
- **A member the contract has and the read model does not would fail this rule**, and rightly: it
  would mean the boundary publishes something no layer beneath it can answer. If such a member ever
  has a reason to exist, the reason belongs in a record and the rule gains an exception that names
  it — not a silence.
- **It does not watch the other direction.** `HttpToApplicationMappings` builds commands and
  requests from what a caller sent, where a missing member is caught by validation and by
  `required`. The asymmetry is the point: the risk is on the way out, where nobody is checking.
- **It does not watch `PagedHttpResponse`, `LoginHttpResponse` or `ErrorHttpResponse`**, which are
  built from something other than a read model. They are envelopes and answers, not translations,
  and holding them to this rule would mean inventing a source for them.

## Alternatives considered

**Make every contract member `required`.** The compiler would then refuse the omission outright,
which is stronger than any test. Rejected because `required` is a statement about the *caller* of a
constructor, and these contracts are also deserialised by clients and by the test suite: making a
nullable member required to protect one construction site would distort the type for every other.
It also says nothing about a member whose value is copied from the wrong field.

**A source generator or an AutoMapper-style library.** The omission disappears by construction,
and so does the reader's ability to see what the boundary publishes: the four methods are currently
the one screen where somebody can read what a client receives. This repository has spent eight
records keeping that legible.

**Nothing — rely on review.** It is what was relied on, and it failed twice in eight pull requests,
including once where the whole change existed to add the dropped column.

## Verification

- `EveryMappingToAPublishedContract_AssignsEveryMember`, watched failing twice before being trusted:
  once against the omission of `AdministrationTrainingHttpResponse.TrainerName` — the first real
  defect, restored verbatim — and once against `TrainerHttpResponse.SuspensionReason`, so that it is
  seen naming two different types rather than one special case.
