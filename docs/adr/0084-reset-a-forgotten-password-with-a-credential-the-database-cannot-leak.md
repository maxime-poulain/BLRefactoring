# 0084 — Reset a forgotten password with a credential the database cannot leak

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

A person who forgot their password needs a way back into their account, and the only proof of
ownership this platform can ask for is control of the account's mailbox. That makes recovery a
security feature wearing a convenience feature's clothes: the reset link is a credential that
grants a password change, the endpoint that mails it is an oracle for which addresses have
accounts unless it is built not to be, and every stored trace of the link is a way for a database
leak to become an account takeover.

The context this lands in was decided long ago. Identity & Access is a supporting context, bought
rather than modeled — accounts are the framework's `IdentityUser<Guid>`, deliberately absent from
the domain — and authentication has never been a use case of either stack: registration and login
are actions on the shared `AuthControllerBase`, published identically by both hosts. Sign-in
already holds the anti-enumeration line (`InvalidCredentials()`, one sentence for every way of
failing), while registration deliberately does not (its `409` names a taken email, an oracle this
repository documents as chosen). Email leaves through the transactional outbox and its delivery
worker, never inline (ADR 0002, ADR 0025, ADR 0031), and the outbox's payloads are plaintext
JSON, retained fourteen days after delivery.

## Decision

- **Recovery is an Identity & Access capability, not a domain concept.** No aggregate, no domain
  event, no command in either stack: two actions on `AuthControllerBase`, a shared
  `PasswordRecoveryService` in `Shared.Api/Identity` beside `TokenService`, and a port into the
  Identity store. The invariants the feature carries — one live link, one redemption, fifteen
  minutes — are real, and they are written in this context's own idiom: a table shape, a primary
  key, one guarded `DELETE`.

- **The credential is 256 bits of the system's entropy, stored only as a SHA-256 digest.** One
  row per account in the Identity store: `UserId` (the primary key), `TokenHash`, `CreatedOnUtc`,
  `ExpiresOnUtc`. The raw token exists in exactly two places — the email, and the redeeming
  request. A copy of the table forges nothing, which is the property the title names. At this
  entropy a digest needs no salt, and the row is found by its account and compared in fixed time,
  never looked up by digest.

- **Latest-only is the primary key, single-use is a delete, expiry is a timestamp.** Issuing
  replaces the account's row, so every earlier link dies the moment a new one is minted — however
  readable its email remains. Redeeming is one atomic
  `DELETE WHERE UserId AND TokenHash AND ExpiresOnUtc > now`: of two racing redemptions, exactly
  one reads row count 1, and the loser changes nothing. A consumed credential does not exist;
  an expired row is inert until the next request replaces it. No sweeper, no consumed flag.

- **The request path does constant work, and the token is minted at delivery.**
  `POST /Auth/forgot-password` publishes `PasswordResetRequestedIntegrationEvent` — the address
  and nothing else — commits it, and answers `202`, whether or not the address names an account.
  The lookup, the minting and the email happen later, in the outbox consumer, where no caller can
  time them. This closes the enumeration side channels by construction rather than by padding,
  and it keeps the one secret off the outbox row entirely: a payload retained in plaintext for
  fourteen days must never carry a live credential, so the credential is created on the far side
  of the table. An unknown address ends in silence — no email, and no log line either, because a
  probed address written to a log outlives the probe (ADR 0026).

- **The redemption is ordered so failures never burn a good link.** Find the account, peek at the
  credential, run the password validators, and only then consume — atomically — and change the
  password. A policy-refused password answers Identity's own words and leaves the link alive,
  because only a caller holding a live link can ever reach that verdict. Everything else — unknown
  address, wrong token, expired, spent, superseded, lost race — answers one fixed sentence through
  one method, sign-in's discipline one flow over. Registration's oracle stays as documented; this
  feature declines to add a second, quieter one.

- **The password write stays the framework's.** Once this repository's credential is consumed, a
  framework reset token is generated and redeemed inside the same request — mint-and-burn — so
  validators, hashing, security-stamp rotation and the concurrency stamp are all
  `UserManager`'s, and no code here touches a password hash. The consumption, the password write
  and the `PasswordChangedIntegrationEvent` commit inside one `TransactionScope`, registration's
  own shape (ADR 0040). A successful reset also clears the lockout: whoever owns the mailbox owns
  the account, and a lockout earned by whoever was guessing has no business outliving the proof.

- **The owner is told.** The committed fact becomes a notice to the account's own address — the
  alarm bell for a reset the owner did not perform, whose remedy is one more reset into the same
  mailbox, which invalidates whatever the intruder used. The house precedent is the warning sent
  to the previous address when a contact email changes (ADR 0056's direction of travel).

- **The browser's door is the BFF's, with one per-visitor window.** `/bff/forgot-password` and
  `/bff/reset-password` stand beside `/bff/register`, for its reason: the proxy's anonymous
  family stays exactly the catalog's (ADR 0062). Both share a fixed window of ten requests per
  fifteen minutes per visitor address — a human finishes the flow in two or three — because
  asking costs the platform an email and, latest-only being the rule, costs the account's owner
  their pending link: the window is what stands between one address and a mail bomb, and between
  an attacker and a victim who can never finish resetting. The emailed link carries the token and
  nothing else — no address, no identifier — and the reset form asks for the email again.

- **The link's base address is configuration.** `PasswordReset:LinkBaseAddress` names the web
  origin a browser opens; it is required, validated whole at startup, and consumed by the store's
  adapter — the application layer holds no options and receives a finished link through the port.

## Consequences

- A database read leak yields digests that redeem nothing, and an outbox dump yields addresses
  but no credentials. The threat the design does not close is a live SMTP compromise, which no
  server-side token scheme can.

- **Outstanding sessions survive a reset for at most the access token's remaining lifetime.**
  JWTs are validated purely cryptographically, nothing reads Identity's security stamp at request
  time, and the BFF cookie dies with its JWT — so an attacker holding a live session keeps it for
  up to `Jwt:ExpireMinutes` (sixty minutes) after the owner resets. Accepted, with the short
  lifetime as the compensating control: the same staleness this repository already accepts for a
  suspension decided mid-session. The alternative — a security-stamp check on every authenticated
  call — buys immediate revocation at the price of a database read per request, and is the first
  thing to revisit if token lifetimes ever grow.

- **A distributed attacker can still churn a victim's pending link** by requesting resets from
  many addresses, since every request invalidates the previous link by design. The per-visitor
  window bounds a single source; the residual is accepted and this sentence is its record. The
  fallback that would blunt it — keeping older links alive — was rejected because one live link
  per account is the stronger property.

- The delivery worker's at-least-once semantics fit the invariant instead of fighting it: a
  retried or duplicated issue mints a fresh link and kills the one before it, and whichever email
  arrives last is the one that works — which is also what the reset email tells its reader.

- Restarting a container invalidates nothing: the credential lives in the database, not in a key
  ring, so a link survives any restart within its fifteen minutes and validates on either host.

## Alternatives considered

- **ASP.NET Identity's own reset tokens** (`GeneratePasswordResetTokenAsync`, already wired
  through `AddDefaultTokenProviders`). Rejected on where the secret would end up living. Those
  tokens are stateless ciphertext validated against a DataProtection key ring — which this
  repository never persists, so the tokens would die on every container restart and never
  validate across the two API hosts. Persisting a shared key ring fixes that and creates the
  worse problem: a key ring at rest is a master secret, and one stored unencrypted in the shared
  database turns any read leak into the ability to forge reset tokens for every account — the
  exact failure the hashed row cannot have. Their invalidation story is also coarser: superseding
  a link means rotating the security stamp, a side channel wearing an invariant's clothes, where
  the primary key states it as schema.

- **A JWT reset token.** Rejected because a bearer token that must die on use, and must die when
  a newer one is issued, needs a server-side record either way — a stateless design for a
  stateful requirement — and because putting a password-granting credential on the same signing
  key and validation path as access tokens invites the two being confused, in code and in review.

- **Sending the email inline from the request.** Rejected by ADR 0002's whole argument: the mail
  would leave for a request that could still fail, retries would be the controller's problem, and
  the request's duration would say whether an address was looked up. The outbox already owns
  delivery, retries, backoff and poison; recovery gets all of it by publishing a fact.

- **A `PasswordReset` aggregate in the domain.** Rejected by the strategic design: Identity &
  Access is bought, its model is the framework's, and a credential-lifecycle aggregate would put
  authentication vocabulary inside a domain that deliberately holds none. The invariants fit in
  a schema; a model that re-states them adds a language, not a guarantee.

- **An API-side rate window.** The contact endpoint's window partitions by a route value; these
  routes carry none, and a per-address partition would need the body before the limiter runs. The
  API-side exposure equals `/Auth/login`'s — anonymous and unthrottled at the API — so the
  posture is consistent, and the BFF window guards the only door a browser uses.

- **A re-issue cooldown** (refusing to mint again within a minute). Rejected because the
  requirement is that every request invalidates every earlier link, and a cooldown keeps an old
  link alive to honor a throttle — trading the invariant for a weaker abuse bound the per-visitor
  window already provides.

- **Extending the Turnstile challenge to the recovery form** (ADR 0083). Not taken, and the
  difference from the contact form is the payload: contact relays attacker-authored content to a
  third party, so a bot behind it is a spam cannon, while recovery sends one fixed sentence to
  the address's own mailbox — the only abuse is volume, and volume is what the window answers.
  The challenge remains the named hardening if mail-bomb abuse appears; the infrastructure is a
  page widget and one header check away.

## Verification

- `PasswordRecoveryRules` holds the three structural decisions: the row is only ever a digest
  beside its timestamps (`TheResetCredential_IsStoredOnlyDigested`), the account is the primary
  key (`OneResetCredential_PerAccount`), and the lifetime literal stands
  (`TheResetCredential_DiesInFifteenMinutes`).
- `PasswordRecoveryTest` in the shared TestKit proves the flow whole on both hosts, through real
  SQL Server and real SMTP: the round trip, the identical answer and permanent silence for an
  unknown address, latest-only, single-use, expiry (the row aged through the host's own
  container), the byte-identical refusal for a wrong token and an unknown address, the
  policy-refused password that leaves the link alive, and the digest at rest.
- `PasswordResetTokenStoreTests` proves the store's schema and SQL against SQLite; the BFF suite
  proves the door — forwarding, the forgery header, the window's 429, the problem passthrough —
  and the bUnit suites hold the two pages to the one generic confirmation.
