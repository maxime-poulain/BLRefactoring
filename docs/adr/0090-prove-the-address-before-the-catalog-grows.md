# 0090 — Prove the address before the catalog grows

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

Registration takes an email address on faith. The account works, signs in, and can put a training
on the public catalog while its address is a typo, a joke, or somebody else's — and the platform's
only channels to a trainer, the password reset and the administrative notices, all point at that
unproven mailbox. What the platform needs is proof of control: a link mailed to the address, and a
click only the mailbox's owner can perform.

The machinery for exactly this shape already exists. ADR 0084 built a mailed credential the
database cannot leak — entropy in the mail, a digest at rest, latest-only by primary key,
single-use by one guarded delete — and ADR 0056 settled that an outbox fact carries identifiers
while the consumer resolves the address at delivery. Identity & Access remains bought rather than
modeled: accounts are the framework's `IdentityUser<Guid>`, and that table has carried an
`EmailConfirmed` column, dead until now, since the day it was created.

Two constraints shape the rest. The refusal is a business rule — *an unverified trainer must not
grow the public catalog* — so its home is the domain, not a framework flag; and the verification
email must speak the visitor's language (ADR 0088), while the domain and the application layer
are forbidden every word of `TrainingHub.Translations` (ADR 0089's discipline, tightened here).

## Decision

- **The state is the framework's own column, and nothing else.** A verified account is
  `AspNetUsers.EmailConfirmed = 1` — no new field, no trainer status, no aggregate. The flag is
  flipped through the only door the framework publishes for it, mint-and-burn:
  `GenerateEmailConfirmationTokenAsync` and `ConfirmEmailAsync` inside one `TransactionScope`
  with the redemption of this repository's own credential, so the burn and the spend commit
  together or not at all. If `ConfirmEmailAsync` refuses — a stamp race — the scope rolls back
  and the link survives: failures never burn a good link, ADR 0084's ordering one flow over.
  `SignIn.RequireConfirmedEmail` and `RequireConfirmedAccount` are never set, and a rule pins the
  absence: an unverified account **must** sign in, manage its space and read everything — one
  door closes, deliberately no other.

- **The credential is ADR 0084's, with three recorded deviations.** One row per account in the
  Identity store — `UserId` the primary key, `TokenHash` a SHA-256 digest of 256 fresh bits,
  `CreatedOnUtc` — spent by one `ExecuteDeleteAsync` guarded by account and digest together.
  The deviations, each a decision and not a drift:
  1. **No expiry.** The honest counter-argument goes on the record: the reset link dies in
     fifteen minutes precisely so a mailbox compromise must be *current*, while an immortal
     verification link redeems from an archived mailbox years later. What saves the decision is
     the stake — verifying somebody's account grants nothing a password reset would not grant
     more of, latest-only makes every resend a revocation, and erasing the account cascades the
     row away. `CreatedOnUtc` stays as the hedge: a future expiry is a rule change, not a
     migration.
  2. **Redemption looks the row up by digest alone**, under a unique index, with no second
     factor. ADR 0084's re-typed address protects a credential that takes the account; this one
     proves a click. The alternative — the user identifier in the URL — would write an account
     identifier into browser history for no gain.
  3. **No peek.** The port is `IssueAsync` and `TryRedeemAsync`, nothing else: there is no
     second door after redemption for a peek to guard, so the peek collapses into the spend.

- **The rule is enforced where it is reachable, not everywhere it can be written.** Verification
  is never revoked — no de-verification exists, and no flow changes an account's email — and the
  backfill below grandfathers everyone, so *an unverified trainer can never own a training*.
  That invariant licenses exactly two domain checks and forbids the rest as untestable theater:
  `Training.CreateAsync` asks the new domain port `ITrainerVerification` (ADR 0030's fourth fact
  port, beside `ITrainerStanding`) and refuses with `Training.TrainerUnverified`; the transfer
  domain service asks the same port about the recipient and refuses with
  `Training.RecipientUnverified`. Publish, edit and the transfer's donor check nothing, because
  an unverified owner cannot exist to be caught there. The condition that would break this
  reasoning is named so it cannot be forgotten: the day an email-change flow un-confirms an
  account, every skipped check becomes reachable and this record's successor owes them.

- **The boundary adds a courtesy, not the enforcement.** `VerifiedTrainerPolicy` sits on
  `CreateTrainingAsync` and nowhere else, on both hosts, answering an empty `403` before the
  request travels to the domain to be refused there — correct either way, cheaper this way. The
  handler reads the caller through the application port `IAccountVerificationQuery` and lets
  non-trainers pass, `ActiveTrainerAuthorizationHandler`'s reasoning. The transfer's recipient
  shows why the boundary alone can never carry the rule: a policy reads the caller, and the
  recipient rides in the body.

- **The request path is a committed fact, and the token is minted at delivery.** Registration
  publishes `EmailVerificationRequestedIntegrationEvent` — the user identifier and the request's
  resolved culture, a code and never prose (ADR 0088) — in the same scope that creates the
  account, the fourth flow whose endpoint commits its own fact. The consumer
  (`SendVerificationLink`) mints through `IEmailVerificationTokenStore.IssueAsync`, which
  resolves the address at delivery (ADR 0056) and answers `null` for an account that is gone or
  already confirmed — absorbed in silence, no log line, so a retried delivery is idempotent and
  a probed account writes nothing that outlives the probe (ADR 0026). The token never rides an
  outbox row: a payload retained in plaintext must never carry a live credential.

- **The words of an email are presentation, and presentation lives in the adapter ring.** The
  consumer asks a port — `IVerificationEmailComposer.Compose(culture, username, link)` — for the
  finished subject and body, the same shape as the store handing back a finished link: the use
  case names an output boundary, and the thing that fills it lives with the other interface
  adapters. The adapter sits in `Shared.Infrastructure/Email/` beside `SmtpEmailSender` — the
  content half of the email mechanics whose home `OnlyTheInfrastructure_SpeaksSmtp` already
  fixed — pins `CurrentUICulture` from the event's code, and reads
  `IStringLocalizer<NotificationResources>`, a new resource family in three languages. The
  dependency rule tightens accordingly: the domain and the application layer never reference
  `TrainingHub.Translations`, and inside the infrastructure only the `Email` namespace may — a
  repository, an interceptor, the outbox processor will never compose prose, because a persisted
  fact does not vary with the reader's language. The existing emails stay English; localizing
  them is a named deferral, since their events carry no culture to localize with.

- **Two emails, because two facts with two audiences.** The welcome remains the Catalog
  context's fact, sent to the trainer's contact address — its one sentence promising immediate
  publication amended, since it stopped being true. The verification email is Identity's fact,
  sent to the account's address, and says what latest-only means: only the newest link works.

- **Resend is authenticated, windowed, and always answers the same.** `POST
  /Auth/resend-verification` requires `TrainerPolicy` — deliberately not `ActiveTrainerPolicy`,
  because verifying an address returns nothing a suspension withholds (ADR 0085's principle), so
  the pinned set of writes a suspension keeps gains its second member. It answers `202`
  unconditionally; an already-confirmed account's resend mints nothing and mails nothing, and
  the response declines to say so. A fixed window of five requests per fifteen minutes,
  partitioned by the caller's own claim, bounds the mail volume — and protects the owner from a
  hostile session churning their pending link, since every permit is also a revocation.
  `POST /Auth/verify-email` takes the token in the body — never the query string, which is
  logged — anonymously and unwindowed: redemption costs one indexed read, and guessing 256 bits
  is not a plan.

- **Every dead link earns the same sentence.** Unknown token, spent token, superseded token, a
  lost race — one fixed answer: *"This verification link is invalid or has already been used. If
  you already verified your address, just sign in."* One sentence because the distinctions would
  require tombstones — a record of consumed credentials, the exact row that
  consumption-by-deletion exists to never hold — and because the second half of the sentence
  covers the one honest confusion a scanner-burned or double-clicked link can cause.

- **The browser's door is the BFF's, and the page never spends on load.** `/bff/verify-email`
  stands beside the reset endpoints, anonymous and status-only. The page shows an explicit
  confirm button — mail scanners `GET` every URL they see, and a link that redeems on load is a
  link the scanner burns before the human arrives. On success, a signed-in session refreshes its
  standing and the banner dies without a sign-out; the standing itself fails open, because a
  failed read must not nag an innocent. The unverified trainer sees an informational banner with
  the resend, and the catalog's create door disabled — ADR 0057's courtesy, refusal mirrored in
  the UI before it is earned over the wire.

- **Everyone who registered before this record is grandfathered.** The migration that creates
  the credential table also sets `EmailConfirmed = 1` for every existing account, and the
  honest cost goes on the record: after the backfill, `true` means *verified or predates
  verification*, indistinguishably. The alternative — freezing the whole existing catalog's
  trainers out of creation on deploy day — punishes every early account for a rule that did not
  exist when they registered. Rolling back drops the table and leaves the flags, which is
  documented and harmless: the flags are then dead again, as they were before.

## Consequences

- A database leak still forges nothing — digests redeem nothing, and the outbox rows carry
  identifiers, never credentials. The residual risks are ADR 0084's, minus the account-takeover
  stake and plus the archived-mailbox redemption the no-expiry deviation accepts above.

- The domain gained its fourth fact port and two error codes, and every test that arranges a
  training-owning trainer now arranges a verified one. The shared TestKit's helper flips the
  flag through a service scope — the aging precedent, promoted — so the honest emailed click is
  proven in exactly one suite instead of re-proven by every flow that merely needs a caller the
  create door admits.

- Registration now commits two facts and the worker sends two emails per registration, to two
  different addresses when the contact address differs from the account's.

- The reachability argument is load-bearing and its breaking condition is recorded: an
  email-change flow that un-confirms accounts re-opens every check this record declined to
  write. That future record inherits the list.

## Alternatives considered

- **The framework's own confirmation tokens** (`GenerateEmailConfirmationTokenAsync` mailed
  directly). Rejected for ADR 0084's reason, unchanged: stateless ciphertext against a
  DataProtection key ring this repository never persists — dead on restart, invalid across
  hosts, and a persisted key ring would be a master secret worse than the problem.

- **A distinct "already used" message.** Rejected because telling a spent link apart from a
  fabricated one requires remembering spent links — tombstones — and the consumption-by-deletion
  design exists precisely so no such memory can leak. The one sentence's second half carries the
  only useful distinction for free.

- **One combined welcome-and-verify email.** Rejected because the two facts have different
  owners and different addresses: the welcome is Catalog's, to the contact address a trainer
  chose to publish; the proof is Identity's, to the account's own mailbox. Combining them sends
  a credential to the wrong audience whenever the two addresses differ.

- **Full check parity across every domain write** (publish, edit, the transfer's donor).
  Rejected as unreachable: with verification never revoked and everyone grandfathered, an
  unverified owner cannot exist, and each of those checks could only be exercised by a test
  fabricating a state the model cannot produce. The invariant and its breaking condition are
  recorded instead.

- **An expiry on the verification link.** Considered and declined as product policy — a trainer
  who registers on Friday should not find a dead link on Monday — with the counter-argument
  recorded in the deviation above rather than smoothed over.

- **Requiring a confirmed email at sign-in** (`RequireConfirmedEmail = true`). Rejected as the
  inversion of the business rule: the unverified account must reach its space, its settings and
  the resend button — locking the front gate to guard one door would strand exactly the people
  the flow exists to help. A rule keeps the one-liner out.

## Verification

- `EmailVerificationRules` holds the structure: the credential is three columns and no string,
  the store mints from `RandomNumberGenerator` and spends with one guarded `ExecuteDeleteAsync`,
  the mapping keys the account and uniquely indexes the digest, the Identity options never
  demand a confirmed email, and `VerifiedTrainerPolicy` guards exactly the create action on each
  host. The localization fences are `LocalizationRules`': no inner circle references
  `TrainingHub.Translations`, and only the infrastructure's `Email` corner reads it.
- `EmailVerificationTest` in the shared TestKit proves the flow whole on both hosts, through
  real SQL Server and real SMTP: the courtesy `403` and the domain's refusal before the click,
  the emailed token opening the create door after it, the replay answering the one sentence,
  latest-only across a resend, the window's `429` on the sixth ask, the silent `202` for an
  already-verified account, and the French invitation under a French registration.
- `EmailVerificationTokenStoreTests` proves the store against SQLite;
  `VerificationEmailComposerTests` proves the composer's three languages and its culture
  restore; the BFF and bUnit suites hold the door, the banner, and the page's three branches.
