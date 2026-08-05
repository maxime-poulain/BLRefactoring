# 0031 — Send email over SMTP, and prove it against a real server

- **Status:** Accepted
- **Date:** 2026-08-04

## Context

`IEmailSender` has existed since ADR 0002 named the welcome email as the reaction that had to
leave the transaction. ADR 0024 and ADR 0025 gave the port real callers: two integration event
handlers, fed by the outbox after the commit — one welcomes a new trainer, one warns a contact
address that just lost its profile. Through all of it the implementation stayed
`FakeEmailSender`, which wrote the message to the log and promised that "choosing a provider
stays a one-line change". A promise like that is only worth something the day somebody collects
on it, and until then the whole email path — composition, delivery, retry on failure — rested on
an adapter that could not fail.

The object store already answered the same question for bytes (ADR 0021): a real protocol behind
the port, a real server in development and in the integration suites, and a rule confining the
client library to the infrastructure. This record collects on the email promise the same way.

## Decision

**A MailKit adapter behind the port, configured by typed options, proven against Mailpit.**

### The port moves to the layer that owns it

`IEmailSender` and `EmailMessage` leave the kernel for
`TrainingHub.Shared.Application/Notifications/`. Their only consumers are the two integration
event handlers of that same project; nothing in the domain, in either stack's services or in any
controller names them. A port consumed by exactly one layer is that layer's vocabulary, and the
direction was already established: `IIntegrationEventHandler` is declared in the application
layer and the infrastructure — which references it — implements against it. `ITrainingSearchIndexer`
sits in the same situation and deliberately does not move today: it will follow the day it grows
a real adapter, and moving it with somebody else's change would leave its record unwritten.

### The client is MailKit; the protocol is what matters

Microsoft documents `System.Net.Mail.SmtpClient` as obsolete for new development and names
MailKit as its replacement. But the reason is the same one ADR 0021 gave for `AWSSDK.S3`: SMTP
is the protocol every provider on any shortlist speaks, so the choice of client is what makes
the mail server a configuration value rather than a rewrite. Locally the server is a Mailpit
container; in production it is whatever relay the deployment names in the `Smtp` section.

### One typed options class, validated at start-up

`SmtpOptions` carries the host, the port, the sender identity and the optional credentials —
everything that ties this solution to one mail server, and nothing else. `ValidateOnStart`
refuses a host that cannot name its relay before traffic arrives, the `ObjectStorageOptions`
discipline. The sender address lives here rather than on `EmailMessage` on purpose: which
identity a deployment sends as is infrastructure configuration, like which bucket it writes to —
the application composes recipient, subject and body, and that is the whole of its vocabulary.

### A connection per send, and no retry of its own

MailKit's `SmtpClient` is not safe to share, the adapter is a singleton serving both hosts'
outbox workers, and the volume is one message per trainer-lifecycle fact, dispatched
sequentially. A pooled connection would need liveness checks, reconnection and a lock — machinery
defending a throughput nobody measured a need for. A failed send is not caught in the adapter
either: the outbox processor records the exception on the envelope and retries within its
budget, and a retry policy duplicated one layer down would just argue with it. For the same
reason there is no hosted bootstrapper: SMTP has nothing to provision, and a host must be able
to start while the mail server is down — an unsendable email is exactly what the outbox's retry
budget exists for.

### The fake is deleted, with no conditional fallback

The registration always wires the real adapter, as it always wires the real object store — no
fake `IObjectStore` exists and now no fake `IEmailSender` does. An environment switch keeping
the fake alive would be a path production never runs, kept green by tests that prove nothing
about delivery. ADR 0002's honesty note stands unchanged: `SendAsync` is not idempotent, and
under at-least-once delivery a duplicate fact now produces a real duplicate email. No
deduplication key is added — both current messages are harmless as duplicates, the welcome
handler says so in its own remarks, and a dedup store is a decision for the first message that
is not harmless.

### Proven against a real server, in both suites

The shared `EmailTest` registers a trainer through the API and reads the welcome message back
out of a real Mailpit container through its HTTP API — then changes the contact address and
reads the warning from the address that lost it. Both hosts run both proofs, over the same wire
production uses: MailKit connecting, the configured sender on the envelope, the handler's exact
subject and wording delivered.

## Consequences

- Development gains a container: `docker compose up -d` now starts Mailpit beside SQL Server and
  SeaweedFS, with the messages readable at `http://localhost:8025`. The integration suites start
  a third Testcontainer, same tag, no shared configuration.
- A deployment must supply the `Smtp` section or the host refuses to start — the ObjectStorage
  precedent, and the intended one: a misconfigured relay heard about at start-up costs minutes,
  one heard about via the outbox's poison rows costs an investigation.
- The "remain fakes that write to the log" statements in ADR 0024 and ADR 0025 are dated by this
  record for the email half; the search indexer half stays true.
- `OnlyTheInfrastructure_SpeaksSmtp` holds the containment line: no backend assembly but the
  infrastructure may depend on the `MailKit` or `MimeKit` namespaces — the same line
  `OnlyTheInfrastructure_KnowsTheObjectStore` holds for `Amazon`.

## Alternatives considered

**`System.Net.Mail.SmtpClient`.** In the box, and documented by its own vendor as obsolete for
new development: a synchronous-era API kept for compatibility. Choosing it would trade a
package reference for a client Microsoft says not to build on.

**A provider SDK — SendGrid, SES, Mailgun.** Each puts a vendor's dialect at the seam and makes
the provider a rewrite instead of a configuration value. SMTP is to mail what S3 is to storage
in ADR 0021: the protocol is the replaceable part's contract, so the adapter speaks the
protocol.

**Keeping the fake behind an environment switch.** A conditional composition root this
repository has nowhere else, guarding a path production never executes. The object store set the
precedent the other way, and the suite is faster to trust than a fallback: the real adapter runs
in every integration test.

**MailHog as the development server.** The original of the genre, unmaintained since 2020.
Mailpit is its actively maintained successor with the same shape — an SMTP sink with an HTTP
API — which is what the tests read delivery back through.
