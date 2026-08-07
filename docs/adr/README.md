# Architecture decision records

One file per decision that would be expensive to reverse, or that a reader would otherwise
reasonably assume was an accident.

The code says *what*, and the comments say *why this line*. What neither can hold is the shape of a
decision: the options that were open, what each would have cost, and why the one that lost was
rejected. Without that, the second reader either takes the design on trust or rediscovers the
argument — and sometimes reverses it, since the rejected option is usually the one that looks
simpler from the outside.

## Conventions

- One record per file, numbered in order: `NNNN-a-sentence-in-the-imperative.md`.
- Numbers are never reused, and a record's **body** is never rewritten once merged. A decision that
  changes gets a new record that supersedes or amends the old one, and the old one is marked as such
  and left in place — the reasoning that was true at the time is what makes the change legible.
- The **status line is the exception**, and the only one: it carries the record's standing, not its
  argument, so a later decision annotates it. A record that amends another declares it in an
  `- **Amends:** NNNN` field, and the amended record's status names it back (ADR 0039).
- Status is one of `Proposed`, `Accepted`, `Superseded by NNNN`, optionally followed by ` — ` and
  what later records did to the decision. It is written in the record; the table below repeats it,
  and a rule holds the two to each other.
- Record the alternatives and why they lost. A record without them documents an outcome, not a
  decision, and cannot be revisited.

## Index

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-paginate-on-the-query-side-over-a-total-order.md) | Paginate on the query side, over a total order | Accepted — amended in part by 0029 |
| [0002](0002-keep-domain-reactions-in-the-transaction-and-deliver-integration-events-through-an-outbox.md) | Keep domain reactions in the transaction, deliver integration events through an outbox | Accepted — implemented; the message design is recorded in 0024, the delivery worker in 0025; its registration consequence is corrected by 0040 |
| [0003](0003-apply-migrations-on-startup-in-development-only.md) | Apply migrations on startup in Development only | Accepted — amended by [0045](0045-fail-readiness-while-a-migration-is-pending.md): the readiness probe this record said to revisit it for now exists, and a pending migration fails it |
| [0004](0004-publish-every-error-as-rfc-7807-problem-details.md) | Publish every error as RFC 7807 Problem Details | Accepted — amended in part by 0012 |
| [0005](0005-store-audit-timestamps-at-full-precision.md) | Store audit timestamps at full precision | Accepted |
| [0006](0006-describe-the-api-with-the-frameworks-openapi-generator.md) | Describe the API with the framework's OpenAPI generator | Accepted — one paragraph superseded by 0008 |
| [0007](0007-assert-with-awesomeassertions.md) | Assert with AwesomeAssertions | Accepted |
| [0008](0008-generate-the-http-client-from-a-script-and-verify-it-in-ci.md) | Regenerate the HTTP client from the API, and commit it automatically | Accepted — the list-shape argument for the source host is dated by 0029; the hosts now answer alike, and the layered one remains the source |
| [0009](0009-hold-the-access-token-in-the-bff-instead-of-the-browser.md) | Hold the access token in the BFF instead of the browser | Accepted |
| [0010](0010-declare-the-conditional-request-contract-in-the-document.md) | Declare the conditional-request contract in the document | Accepted |
| [0011](0011-answer-a-creation-with-201-and-the-address-of-what-was-created.md) | Answer a creation with 201 and the address of what was created | Accepted, amended — see the Amendment section below |
| [0012](0012-finish-the-one-error-shape-and-name-its-members-apart.md) | Finish the one error shape, and name its members apart | Accepted — amended by 0016 |
| [0013](0013-make-every-record-answer-to-a-test.md) | Make every record answer to a test | Accepted — amended by 0039: the ledger of exemptions is what says how many there are |
| [0014](0014-seal-by-default-and-let-inheritance-be-a-decision.md) | Seal by default, and let inheritance be a decision | Accepted |
| [0015](0015-let-each-aggregate-own-the-errors-it-raises.md) | Let each aggregate own the errors it raises | Accepted |
| [0016](0016-let-a-rejected-command-fail-like-every-other-command.md) | Let a rejected command fail like every other command | Accepted — the validation cost it recorded and deferred is paid off by 0043 |
| [0017](0017-measure-what-the-rules-cannot-with-sonarqube-cloud.md) | Measure what the rules cannot, with SonarQube Cloud | Accepted — amended by 0018, and by [0049](0049-measure-duplication-where-repetition-is-a-defect.md): the duplication measure exempts the two hosts, whose published declarations two rules require to be identical |
| [0018](0018-fail-on-the-gate-where-failing-stops-something.md) | Fail on the gate where failing stops something | Accepted |
| [0019](0019-enforce-the-ruleset-this-repository-already-declared.md) | Enforce the ruleset this repository already declared | Accepted — amended by 0020 |
| [0020](0020-declare-every-rule-this-codebase-already-satisfies.md) | Declare every rule this codebase already satisfies | Accepted |
| [0021](0021-store-a-photo-beside-the-row-that-names-it.md) | Store a photo beside the row that names it, and never overwrite in place | Accepted |
| [0022](0022-name-the-repository-after-the-domain-it-serves.md) | Name the repository after the domain it serves | Accepted |
| [0023](0023-document-the-strategic-design-and-hold-it-to-the-model.md) | Document the strategic design, and hold it to the model | Accepted |
| [0024](0024-publish-facts-not-intents-and-version-them-in-the-envelope.md) | Publish facts, not intents, and version them in the envelope | Accepted — the email half of "the ports remain fakes" is dated by 0031; the search half stays true; the retry contract gains its schedule in 0033; the per-consumer half of its at-least-once promise is made true by 0034 |
| [0025](0025-deliver-the-outbox-with-a-hosted-service-in-each-host.md) | Deliver the outbox with a hosted service in each host | Accepted — the email half of "they remain fakes" is dated by 0031; the search half stays true; the retry cadence, the poison's silence and the table's growth are hardened by 0033; delivery is settled per consumer by 0034 |
| [0026](0026-log-with-serilog-to-console-and-files-through-typed-options.md) | Log with Serilog to console and files, through typed options | Accepted |
| [0027](0027-stamp-the-callers-identity-on-every-log-line.md) | Stamp the caller's identity on every log line | Accepted |
| [0028](0028-a-specification-names-a-business-rule-or-it-does-not-exist.md) | A specification names a business rule, or it does not exist | Accepted |
| [0029](0029-answer-a-list-the-same-way-on-both-hosts.md) | Answer a list the same way on both hosts | Accepted |
| [0030](0030-bring-the-fact-to-the-aggregate-not-the-decision-to-a-service.md) | Bring the fact to the aggregate, not the decision to a service | Accepted — narrowed by 0036: a decision with no home is a recorded domain service |
| [0031](0031-send-email-over-smtp-and-prove-it-against-a-real-server.md) | Send email over SMTP, and prove it against a real server | Accepted |
| [0032](0032-flatten-a-value-object-as-a-complex-property-not-an-owned-entity.md) | Flatten a value object as a complex property, not an owned entity | Accepted |
| [0033](0033-back-off-between-retries-log-the-poison-and-sweep-the-delivered-history.md) | Back off between retries, log the poison, and sweep the delivered history | Accepted — the per-consumer isolation it left out arrives in 0034; the poison gains a pollable gauge in 0037 |
| [0034](0034-deliver-once-per-consumer-not-once-per-message.md) | Deliver once per consumer, not once per message | Accepted |
| [0035](0035-give-every-developer-a-git-ignored-local-overrides-file.md) | Give every developer a git-ignored local overrides file | Accepted |
| [0036](0036-model-the-decision-that-has-no-home-as-a-domain-service.md) | Model the decision that has no home as a domain service | Accepted |
| [0037](0037-answer-for-the-hosts-health-at-two-endpoints.md) | Answer for the host's health at two endpoints | Accepted — amended by [0045](0045-fail-readiness-while-a-migration-is-pending.md): a fifth probe answers for the schema, so every "four probes" below now reads five |
| [0038](0038-derive-every-counted-claim-from-the-code.md) | Derive every counted claim from the code | Accepted |
| [0039](0039-hold-the-record-and-its-index-to-the-same-status.md) | Hold the record and its index to the same status | Accepted |
| [0040](0040-register-the-trainer-and-the-account-in-one-transaction.md) | Register the trainer and the account in one transaction | Accepted |
| [0041](0041-derive-every-named-list-from-the-code.md) | Derive every named list from the code | Accepted |
| [0042](0042-close-the-boundarys-vocabulary.md) | Close the boundary's vocabulary | Accepted — amended by [0048](0048-qualify-a-contract-before-naming-what-it-is.md): the qualifier moves to the front of the contract's name, and the rules read the assembly rather than the suffix |
| [0043](0043-validate-once-where-the-rule-lives.md) | Validate once, where the rule lives | Accepted — amended by [0046](0046-refuse-the-empty-identifier-at-every-entry-point.md): the one shape rule this record kept in the pipeline gains a second half at the HTTP boundary, and the pipeline keeps its own; the sentence emptying the creation validators is corrected there |
| [0044](0044-let-the-domain-speak-entirely-in-its-own-terms.md) | Let the domain speak entirely in its own terms | Accepted |
| [0045](0045-fail-readiness-while-a-migration-is-pending.md) | Fail readiness while a migration is pending | Accepted |
| [0046](0046-refuse-the-empty-identifier-at-every-entry-point.md) | Refuse the empty identifier at every entry point | Accepted |
| [0047](0047-verify-the-build-a-pull-request-delegates.md) | Verify the build a pull request delegates | Accepted |
| [0048](0048-qualify-a-contract-before-naming-what-it-is.md) | Qualify a contract before naming what it is | Accepted |
| [0049](0049-measure-duplication-where-repetition-is-a-defect.md) | Measure duplication where repetition is a defect | Accepted |
