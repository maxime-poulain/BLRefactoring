# 0083 — Ask the visitor for proof where their address is real

- **Status:** Accepted
- **Amends:** [0082](0082-let-a-visitor-reach-a-trainer-without-learning-their-address.md)
- **Date:** 2026-08-15

## Context

ADR 0082 opened the one anonymous write this product has — a visitor's message to a trainer — and
bounded it three ways: a honeypot that tells scripts from people without teaching them the
difference, a window around the recipient on the API, and a window around the sender on the BFF.
All three are passive. The honeypot catches the scripts that fill every field; the windows bound
the damage of whatever gets through. None of them ever *asks* the sender anything, so a bot that
skips the honeypot and paces itself under five messages a minute per address is indistinguishable
from a person, forever.

A challenge closes that gap, and the market's answers are all the same shape: a widget in the page
earns a short-lived, single-use token; the server presents that token, with a secret, to the
challenge's issuer; the issuer answers whether it was genuinely earned. What differs is the vendor.
Google's reCAPTCHA sends visitor data to Google and drags a consent conversation into a product
whose whole contact design is built on keeping the visitor's data out of everything (ADR 0082);
**Cloudflare Turnstile** runs the same protocol without cookies, invisibly for most visitors, and
free. The vendor is an implementation detail of this record; the placement is the decision.

## Decision

**The challenge is the BFF's, judged where the visitor's connection ends, and the API never learns
it exists.**

- **The BFF serves the contact path itself, and the proxy forwards no contact path at all.** The
  endpoint stands at the very address the proxied route used to serve —
  `POST /api/Catalog/trainers/{trainerId:guid}/contact` — so the browser's call does not change,
  and the endpoint's template outranks the catalog's catch-all route. That precedence is the
  security property: a proxied twin of the path would be a door around the toll booth, so the
  route table names no contact path, and a rule holds it to that.
- **The token rides a header, never the body.** `X-Turnstile-Token`, declared in `BffContract`
  beside the forgery header it works like. What the BFF forwards to the API after judging the
  token is exactly `ContactTrainerHttpRequest` — the contract the API publishes, unwidened. Both
  hosts, their controllers, the generated client's operations and the TestKit suites are untouched
  by this record, which is the point of the placement: a challenge is a property of the door the
  public walks through, not of the application behind it.
- **The verification is a port with one consumer.** `ITurnstileVerifier`, answered by an adapter
  that presents the secret, the token and the visitor's address to Cloudflare's `siteverify` — the
  one place the secret may travel — and **fails closed**: an unreachable issuer refuses the message
  rather than waving it through, because the window in which a guard is down is exactly the window
  a flood looks for. A missing token is refused without asking anything.
- **The refusal is a 403 problem document, worded for a person.** A refused token is most often a
  visitor whose widget expired between solving and sending, and the page tells them to try again;
  a bot reads the same sentence. The API's own verdicts — 400, 404, 429 — pass through the
  endpoint body and status alike, as registration's do.
- **The key pair is configuration, and it is optional.** Both keys absent switches the challenge
  off: the endpoint forwards without judging, `GET /bff/turnstile` answers a null site key, the
  dialog renders no widget, and the honeypot and both windows keep standing — a machine with no
  pair runs the whole product, which is what lets every environment default to
  `"SiteKey": null, "SecretKey": null` and a developer supply real keys through the git-ignored
  `appsettings.Local.json` (ADR 0035). What startup refuses is half a pair (ADR 0033): one key
  without the other is not a smaller configuration, it is a broken one. When the pair is present,
  the site key is public by design and handed to the browser by `GET /bff/turnstile`, because the
  WebAssembly application carries no configuration of its own; the secret key never leaves the
  host.
- **The per-visitor window moves with the path.** The fixed window ADR 0082 hung on the proxied
  route now bounds the endpoint, unchanged in shape: five messages a minute per remote address, no
  queue, a bare 429.

Records merged before this one keep the sentences they were written with: ADR 0082 describes the
anti-abuse pair it decided, and this record adds the challenge in front of it rather than
rewriting it.

## Consequences

- A script must now solve a Turnstile challenge per message, on top of dodging the honeypot and
  staying under both windows. A person sees, at most, a brief non-interactive widget.
- The BFF gains its first dependency on somebody else's service. Its failure mode is chosen —
  closed — and its blast radius is one endpoint: every read, and every other write, is untouched.
  Sending a message requires the BFF to reach `challenges.cloudflare.com` only while a pair is
  configured; the default configuration ships none, so a machine with no internet — compose
  included — runs the form without the challenge.
- The front end acquires its first external script and its first explicit JS interop beyond
  `localStorage`: the widget is rendered by hand into the dialog, because the loader scanned the
  page long before the dialog existed.
- The token's ride — dialog to page to header — is client plumbing (`TurnstileTokenAccessor`, a
  one-shot deposit, and a `DelegatingHandler` beside the forgery header's), so the generated
  client's signatures stay whole.

## Alternatives considered

- **Verify on the API hosts.** Rejected for symmetry with ADR 0082's windows: the sender's proof
  belongs where the sender is real. It would also have taught two hosts, their contracts and their
  suites about a vendor whose whole involvement is one HTTP call — and the challenge would still
  have to be bypassed-proof at the BFF, which is where the public door is.
- **The token as a body field.** Rejected because it widens the published contract with a field
  the API would have to carry and ignore, and the generated client with it. The header leaves the
  contract exactly as ADR 0082 published it.
- **A YARP middleware judging the token on the proxied route.** Same outcome, worse seams: the
  route table would keep a contact path whose safety depends on middleware ordering, where the
  endpoint makes the judgment and the forwarding one readable unit, testable through the same
  seams as registration.
- **Google reCAPTCHA.** Rejected on the record's own ground: a contact form built so the visitor's
  data reaches nobody should not open by sending that visitor's browser data to an advertising
  company.

## Verification

`TheProxy_ForwardsNoContactPath` holds the route table to naming no contact path, so the one door
stays the judged one. The BFF suite drives the rest over the wire: a genuine token forwarded to
the API — through the named client, never the proxy — with the siteverify call carrying the secret
and the token; a refused token answered 403 with nothing forwarded; a missing token refused
without a call to Cloudflare; the site key served and the secret never; the per-visitor window
still closing on the sixth message. The off state is proven the same way: with no pair configured
a message without a token is forwarded untouched, the site-key endpoint answers a null key, and
half a pair refuses to start. The dialog's suite pins the widget's render with the served key, the
refusal to close without a token while the challenge is on, and the widget's absence when it is
off; the handler's pins the token's one-shot ride.
