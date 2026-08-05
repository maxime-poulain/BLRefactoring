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
- Numbers are never reused, and a record is never rewritten once merged. A decision that changes
  gets a new record that supersedes the old one, and the old one is marked as such and left in
  place — the reasoning that was true at the time is what makes the change legible.
- Status is one of `Proposed`, `Accepted`, `Superseded by NNNN`.
- Record the alternatives and why they lost. A record without them documents an outcome, not a
  decision, and cannot be revisited.

## Index

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-paginate-on-the-query-side-over-a-total-order.md) | Paginate on the query side, over a total order | Accepted — amended by 0029 |
| [0002](0002-keep-domain-reactions-in-the-transaction-and-deliver-integration-events-through-an-outbox.md) | Keep domain reactions in the transaction, deliver integration events through an outbox | Accepted — implemented by 0024 and 0025 |
| [0003](0003-apply-migrations-on-startup-in-development-only.md) | Apply migrations on startup in Development only | Accepted |
| [0004](0004-publish-every-error-as-rfc-7807-problem-details.md) | Publish every error as RFC 7807 Problem Details | Accepted — amended by 0012 |
| [0005](0005-store-audit-timestamps-at-full-precision.md) | Store audit timestamps at full precision | Accepted |
| [0006](0006-describe-the-api-with-the-frameworks-openapi-generator.md) | Describe the API with the framework's OpenAPI generator | Accepted — one paragraph superseded by 0008 |
| [0007](0007-assert-with-awesomeassertions.md) | Assert with AwesomeAssertions | Accepted |
| [0008](0008-generate-the-http-client-from-a-script-and-verify-it-in-ci.md) | Regenerate the HTTP client from the API, and commit it automatically | Accepted — one argument dated by 0029 |
| [0009](0009-hold-the-access-token-in-the-bff-instead-of-the-browser.md) | Hold the access token in the BFF instead of the browser | Accepted |
| [0010](0010-declare-the-conditional-request-contract-in-the-document.md) | Declare the conditional-request contract in the document | Accepted |
| [0011](0011-answer-a-creation-with-201-and-the-address-of-what-was-created.md) | Answer a creation with 201 and the address of what was created | Accepted |
| [0012](0012-finish-the-one-error-shape-and-name-its-members-apart.md) | Finish the one error shape, and name its members apart | Accepted — amended by 0016 |
| [0013](0013-make-every-record-answer-to-a-test.md) | Make every record answer to a test | Accepted |
| [0014](0014-seal-by-default-and-let-inheritance-be-a-decision.md) | Seal by default, and let inheritance be a decision | Accepted |
| [0015](0015-let-each-aggregate-own-the-errors-it-raises.md) | Let each aggregate own the errors it raises | Accepted |
| [0016](0016-let-a-rejected-command-fail-like-every-other-command.md) | Let a rejected command fail like every other command | Accepted |
| [0017](0017-measure-what-the-rules-cannot-with-sonarqube-cloud.md) | Measure what the rules cannot, with SonarQube Cloud | Accepted — amended by 0018 |
| [0018](0018-fail-on-the-gate-where-failing-stops-something.md) | Fail on the gate where failing stops something | Accepted |
| [0019](0019-enforce-the-ruleset-this-repository-already-declared.md) | Enforce the ruleset this repository already declared | Accepted — amended by 0020 |
| [0020](0020-declare-every-rule-this-codebase-already-satisfies.md) | Declare every rule this codebase already satisfies | Accepted |
| [0021](0021-store-a-photo-beside-the-row-that-names-it.md) | Store a photo beside the row that names it, and never overwrite in place | Accepted |
| [0022](0022-name-the-repository-after-the-domain-it-serves.md) | Name the repository after the domain it serves | Accepted |
| [0023](0023-document-the-strategic-design-and-hold-it-to-the-model.md) | Document the strategic design, and hold it to the model | Accepted |
| [0024](0024-publish-facts-not-intents-and-version-them-in-the-envelope.md) | Publish facts, not intents, and version them in the envelope | Accepted — the email half's "still a fake" dated by 0031; the retry contract gains its schedule in 0033 |
| [0025](0025-deliver-the-outbox-with-a-hosted-service-in-each-host.md) | Deliver the outbox with a hosted service in each host | Accepted — the email half's "still a fake" dated by 0031; the retry cadence, the poison's silence and the table's growth are hardened by 0033 |
| [0026](0026-log-with-serilog-to-console-and-files-through-typed-options.md) | Log with Serilog to console and files, through typed options | Accepted |
| [0027](0027-stamp-the-callers-identity-on-every-log-line.md) | Stamp the caller's identity on every log line | Accepted |
| [0028](0028-a-specification-names-a-business-rule-or-it-does-not-exist.md) | A specification names a business rule, or it does not exist | Accepted |
| [0029](0029-answer-a-list-the-same-way-on-both-hosts.md) | Answer a list the same way on both hosts | Accepted |
| [0030](0030-bring-the-fact-to-the-aggregate-not-the-decision-to-a-service.md) | Bring the fact to the aggregate, not the decision to a service | Accepted |
| [0031](0031-send-email-over-smtp-and-prove-it-against-a-real-server.md) | Send email over SMTP, and prove it against a real server | Accepted |
| [0032](0032-flatten-a-value-object-as-a-complex-property-not-an-owned-entity.md) | Flatten a value object as a complex property, not an owned entity | Accepted |
| [0033](0033-back-off-between-retries-log-the-poison-and-sweep-the-delivered-history.md) | Back off between retries, log the poison, and sweep the delivered history | Accepted |
