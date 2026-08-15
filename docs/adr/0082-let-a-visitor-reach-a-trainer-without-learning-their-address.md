# 0082 — Let a visitor reach a trainer without learning their address

- **Status:** Accepted — amended by [0083](0083-ask-the-visitor-for-proof-where-their-address-is-real.md): a Turnstile challenge stands in front of the contact endpoint, judged at the BFF where the visitor's connection ends
- **Amends:** [0070](0070-open-a-trainers-public-page.md),
  [0024](0024-publish-facts-not-intents-and-version-them-in-the-envelope.md),
  [0031](0031-send-email-over-smtp-and-prove-it-against-a-real-server.md)
- **Date:** 2026-08-14

## Context

ADR 0070 opened a trainer's public page and decided its shape by what it leaves out — *"no contact
address, because the platform is the channel"*. The platform was never made into a channel. A
visitor reading a profile, or one of that trainer's trainings, has no way to reach the person; the
sentence was a promise the product had not kept.

Two facts settle the design before any code.

The domain already models the recipient. `Trainer.ContactEmail` is an `Email` value object, and its
own documentation draws the distinction: *"a business attribute of the trainer, not the credential
of their account: authentication is handled by the Identity context, which the aggregate only ever
references through `UserId`."* ADR 0056 already split the two in one direction — a sanction is
addressed to the account, through `ITrainerAccountQuery`. This record splits them in the other.

And *"do not expose the address"* was already executable, in
`NoCatalogContract_CarriesAPrivateMember`. The public reads are clean: `CatalogTrainerDto`,
`CatalogTrainerHttpResponse` and `CatalogTrainingDetailDto` carry no address, and the adapter behind
them never selects the column.

## Decision

**A visitor writes to a trainer through the platform, and the platform never tells them where it
went.**

- **One anonymous endpoint, on both hosts:** `POST /Catalog/trainers/{trainerId:guid}/contact`, on
  `CatalogControllerBase` — the only base carrying `[AllowAnonymous]`. It answers **202 Accepted**:
  nothing was created, and the message leaves later, so a caller is told what is true rather than
  what would be convenient (ADR 0011). One endpoint serves both public pages; the training page
  already knows its owner's identifier (ADR 0070) and adds its own `TrainingId` to the body, which
  only lets the notice say what prompted it.
- **The recipient is resolved at delivery, through a port of its own.** `ITrainerContactQuery` is
  `ITrainerAccountQuery`'s deliberate mirror, and it has exactly one consumer:
  `SendContactMessageWhenTrainerContactedIntegrationEventHandler`, which runs inside the outbox
  worker. **No request-scoped code can read the address**, so no response can carry it — the
  privacy requirement becomes structural rather than a property somebody remembered to leave out.
- **The address is not on the fact either.** `TrainerContactedIntegrationEvent` carries the trainer's
  *identifier*, the visitor's name, their address and their message — never the trainer's. Where
  `TrainerCreatedIntegrationEvent` legitimately carries a contact address because the fact *is* that
  address, here the recipient is a routing detail, and keeping it off the envelope means the only
  place in this application that ever holds it is the moment of sending.
- **A fact with no aggregate behind it, and that is the amendment to ADR 0024.** Every other
  integration event is published by a domain-event handler translating an aggregate's own event.
  Nothing about a trainer changes when somebody writes to them, so there is no aggregate to raise
  one — but *a visitor contacted a trainer* is a fact, already true when the command is accepted.
  The command handler publishes it directly and the outbox row is its only record. That also buys
  what every other email in this codebase has: an SMTP server that is down while the visitor is on
  the page costs nothing (ADR 0002, ADR 0031).
- **`EmailMessage` gains a `Reply-To`, and the `From` does not move.** The four notices this
  application sends on its own behalf leave it unset. This one sets it to the visitor, so the
  trainer answers by pressing reply. Sending *as* the visitor would be a forgery any receiving
  domain with an SPF record is entitled to refuse; `Reply-To` is the header that exists for exactly
  this (ADR 0031).
- **Offered or invisible, the same predicate the profile answers under.** A trainer the catalog will
  not show a visitor is a trainer that visitor may not write to: the handler asks
  `ICatalogDetailQuery.FindOfferedTrainerAsync` and refuses with the profile's own 404, reusing the
  index's composed visibility rather than writing a second one (ADR 0062, ADR 0070).
- **Two anti-abuse measures, each where it works.** A honeypot field the form renders hidden: a
  filled one is answered exactly as a good request is — accepted, and dropped without sending —
  because a different answer is how a bot learns. And a fixed window on the endpoint, **partitioned
  by the recipient**. Per-caller was the obvious choice and is unavailable here: nothing installs
  `UseForwardedHeaders` and nothing reads `X-Forwarded-For`, so behind the proxy every browser
  arrives wearing the BFF's address and a per-caller window would be one global window. The
  recipient partition is not spoofable and bounds the thing worth bounding — a person buried in
  messages.
- **`NoCatalogContract_CarriesAPrivateMember` is narrowed to what the catalog answers**, derived by
  reflection from the actions' return types and `[ProducesResponseType]` rather than filtered by
  namespace. Its population was every type under `Contracts.Catalog`, which read as *the catalog
  names no address anywhere* and was never the decision: what ADR 0070 withheld is the trainer's
  address, a fact this API discloses. A visitor's own address, on a request, is the opposite kind
  of thing. The new population is **stricter**, not looser — it walks the property graph, so a
  withheld word on a nested type the namespace filter never reached now fails too.

## Consequences

- **The catalog's application service stops being reads-only**, and its class documentation is
  rewritten rather than left to rot. The exception is narrow: `ContactAsync` changes no aggregate
  either, and it answers a bare `Result` because it can be refused where the reads cannot.
- **The message is never logged.** What a visitor wrote is theirs and the address they left is
  personal data; a log line is the one place in this system that keeps both for a year (ADR 0026).
  Only the trainer's identifier appears, and only when there is nobody to deliver to.
- **`ContactTrainerCommandValidator` guards identifiers and nothing else.** Length and presence are
  the contract's at model binding, and a second opinion here would be dead or a divergence only one
  host has (ADR 0043).
- **No migration, no new table, nothing stored.** The outbox row is the only trace, and the existing
  retention sweeps it (ADR 0033). A visitor's name, address and message do not accumulate at rest.
- **The counted claims move**: fifteen post-commit consumers, six policies feeding Notification,
  thirty endpoints, twenty-nine use cases — each derived from the code by ADR 0038's rule rather
  than restated.

## Alternatives considered

**Send synchronously from the handler.** The visitor would learn immediately whether it failed.
It contradicts `IEmailSender`'s stated shape — its only consumers are integration-event handlers —
gives up the outbox's retry and poison handling, and makes an anonymous request wait on an external
server.

**Carry the trainer's address on the fact.** One fewer read at delivery, and precedent in
`TrainerCreatedIntegrationEvent`. It writes the address into the outbox, where it sits until the
retention sweep — which is exactly the accumulation this record is trying not to create.

**Store the contact request as an aggregate.** Auditable, re-sendable, the start of an inbox. It
puts a stranger's name, address and words at rest with no retention policy written, for a feature
whose whole content is *forward this*. Priced and declined.

**A per-caller rate limit at the API.** The measure everybody reaches for first, and here it would
be one global window that the first busy visitor closes for everybody, for the forwarded-headers
reason above. Named so its absence reads as a decision.

## Verification

- **`TheTrainersContactAddress_IsReadOnlyWhereItIsSent` watched failing** with the port injected
  into a catalog controller, then restored.
- **The amended `NoCatalogContract_CarriesAPrivateMember` watched still failing** with a
  `ContactEmail` added to `CatalogTrainerHttpResponse` — the leak it exists for — while accepting
  the visitor's own address on the request.
- **Every non-Docker suite green**, and the two completeness guards that demanded the new fact
  (`EveryRegisteredEvent_HasARoute`, `TheTheoryAbove_CoversEveryRegisteredEvent`) satisfied by
  adding it rather than by widening them.
