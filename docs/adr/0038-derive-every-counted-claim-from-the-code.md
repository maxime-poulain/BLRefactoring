# 0038 — Derive every counted claim from the code

- **Status:** Accepted — amended by [0065](0065-ship-every-host-as-an-image-and-build-them-in-the-pipeline.md): the compose file and the Dockerfiles it recorded as read by nothing are read by two rules
- **Date:** 2026-08-05

## Context

This repository's whole argument is that a decision nothing keeps true has already been half
reversed (ADR 0013). Thirty-seven records say so, a hundred and thirty-two rules enforce it, and the
documents that describe the model are held to it in three places — the project graph is compared
edge by edge with the project files, the bounded contexts are compared with the aggregates, the
event-storming boards with the domain events.

Between those guarded regions sits prose that counts. The README says how many handlers the
application layer holds, how many endpoints the API publishes, how many rules defend the strategic
design. `CLAUDE.md` opens with the number of records and the number of rules, because that is how a
reader — or an agent — sizes the repository before reading it. None of it was checked, and an audit
found what an unchecked number does over eleven merges:

- The README claimed *six* domain event handlers and *four* post-commit consumers where the code has
  seven and five — and said *seven handlers* itself, two hundred lines further down.
- `docs/strategic-design/event-storming.md` opened on *six events* and closed on *Seven events,
  seven handlers*. One document, two answers, in the set of documents whose purpose is fidelity.
- The strategic-design index claimed *eleven use cases* against twelve, and said
  `StrategicDesignRules` *checks three things* against four — the rule that was added when the
  context map drifted is the one the sentence forgot.
- The error-code table listed twelve of twenty-one codes. It is presented as the vocabulary of the
  API's `domainErrors` extension, so the four photo codes and the four transfer codes were missing
  from a contract description, and the kernel's own file said *these four* while the table said
  *only the three*.
- `generator.nswag` still named `TrainerClient.GetById`, an operation withdrawn long ago — the
  exact failure ADR 0010 had written down in advance: *"An entry that matches nothing wraps nothing,
  silently: the setting has no way to tell a typo from a deliberate omission."*

Every one of these was written true and went stale on a later merge, which is the definition of the
drift this repository exists to argue against.

## Decision

**A number the documentation states about the code is derived from the code, or it is not stated:
every counted claim names its anchor, and a rule computes the truth.**

- **A ledger, not a sweep.** `DocumentationRules` carries an explicit table of counted claims —
  the document, the sentence to anchor on, the subject in English, and the function that computes
  the number from the code. Adding a sentence that counts something means adding a row; that is the
  cost, and it is the point. A rule that tried to find every number in every document by itself
  would be a spell-checker, and it would fail on "the two hosts" and on a version number.
- **The anchor is part of the claim.** A row whose anchor matches nothing fails, with a message
  saying to re-anchor it or drop the claim. This is the same anti-vacuity discipline `Selected`
  gives every other rule: a claim that silently stops being checked is worse than one that is
  wrong, because nothing will ever say so. Rewording a guarded sentence is therefore a build
  failure, deliberately — the number travels with the words, and the ledger is where the two are
  tied together.
- **The truth comes from reflection, never from a file count.** `TrainingEditedDomainEvent` is
  declared in a file named after another event; counting files would have made the corrected number
  wrong in a different way. Every truth function reads what the assemblies declare, through the
  helpers the suite already owns: `RuleIndex`, `AdrCatalog`, `ProjectGraph`, `DeclaredTypes`.
- **The error-code table is checked in both directions.** A code the table omits is a hole in the
  published vocabulary; a row naming no code is an invitation to branch on something that will
  never arrive. The comparison is against the three `*ErrorCodes` holders, read the way
  `ErrorVocabularyRules` already reads them.
- **A generator setting that names an operation names one that exists.** The entries in
  `generator.nswag` are resolved against the controllers the hosts declare, through the same
  operation-identifier rule the OpenAPI document is built with. ADR 0010 named this failure and
  could not act on it, because nothing in the suite read that file; now something does.

The records themselves are out of scope, and stay out: a merged record is never rewritten (ADR
0013's own convention), so the numbers frozen inside one are correct as of its decision and are
allowed to age. Only the living documents — `README.md`, `CLAUDE.md`, the ADR index and the
strategic-design set — answer to this rule.

## Consequences

- Two of the guarded numbers change in the commit that introduces the guard: the rule count goes
  from a hundred and thirty-two to a hundred and thirty-five, and the record count from
  thirty-seven to thirty-eight. That is the mechanism proving itself on its first run, and it is
  why the ledger includes claims that are already true — the point is not to fix six sentences, it
  is that the next merge cannot quietly falsify them.
- Rewording a guarded sentence now costs a moment: the anchor has to move with it. The alternative
  — an anchor that quietly stops matching — is the failure this record was written about.
- Three documents lose an argument they were making with a wrong number, and the error-code table
  becomes usable as what it claims to be: the list a client branches on.
- `generator.nswag` joins the configuration files the suite reads. `docker-compose.yaml`, the
  Dockerfile and `.config/dotnet-tools.json` are still read by nothing; that is recorded here as
  known, not as decided.

## Alternatives considered

**Stop counting in prose.** The cheapest way to keep a number from going stale is not to write one:
"the domain event handlers" instead of "the six domain event handlers". It would work, and it is
what most repositories should do. Rejected because the numbers here carry the argument — "thirteen
endpoints, and not one of them serves a resource the caller does not own" is a sentence about scope,
and "the endpoints, and not one of them…" says less. It also does nothing for the error-code table,
which is an enumeration and not an adjective.

**Generate the documents.** A tool that renders the counted sentences from the code would make drift
impossible. Rejected: a generator, a template language and a build step for six sentences and one
table, in a repository whose documents are written to be read as prose. The ledger costs one row.

**Hang the rules on existing records.** ADR 0023 holds the strategic-design documents to the model,
and the rules could have cited it. Rejected: 0023 is about four documents describing a domain, and
this decision is about every living document making a claim about the code — including `CLAUDE.md`
and a generator's configuration. Stretching a record to cover what it did not decide is how a record
stops meaning anything.

**Check the numbers in review instead.** They were, for eleven merges, and the audit found six wrong
ones and two documents contradicting themselves. A convention nobody can forget is the only kind
this repository keeps.

## Verification

`EveryCountedClaim_AgreesWithTheCode` walks the ledger, resolves each anchor and compares. It was
red first, on every claim the audit had found wrong, and the capture is in the pull request.
`TheErrorCodeTable_ListsEveryCode` was red on the nine missing codes, and
`EveryOperationNamedInTheGenerator_Exists` on `TrainerClient.GetById`. Each was then broken on
purpose and watched to fail: a correct number edited to a wrong one, a table row removed, a
withdrawn operation put back — and the anchor guard proven by rewording a sentence the ledger
points at, which is the failure mode this record cares most about.
