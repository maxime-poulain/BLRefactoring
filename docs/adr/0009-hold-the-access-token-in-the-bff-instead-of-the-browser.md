# 0009 — Hold the access token in the BFF instead of the browser

- **Status:** Accepted
- **Date:** 2026-08-01

## Context

Until this record, the Blazor WebAssembly application authenticated itself. It posted credentials to
the API's `/Auth/login`, received a JWT, and wrote it to `localStorage` through `IJSRuntime`. A
`DelegatingHandler` read it back on every call and attached it as a bearer token; an
`AuthenticationStateProvider` decoded its payload in the page to decide what to render. The browser
called `https://localhost:7249` directly, cross-origin, which is why the API carries a CORS policy
naming the front end's origin.

That is the arrangement every SPA tutorial teaches, and it has one property that is hard to defend
once stated plainly: **a portable credential sits in reach of any script running in the page**.
`localStorage` is readable by every script on the origin — the application's own, a dependency's, an
analytics snippet's, and anything an XSS lands. What it reads is not a session tied to a browser; it
is a bearer token, valid from anywhere, for its whole lifetime, replayable by whoever holds it.
Exfiltrating it is one `fetch` call, and nothing after that involves the victim's browser at all.

The token also had a second life it should not have had: the front end parsed it. Claims rendered in
the interface came from a document the page could read and — more to the point — could have been
handed by anything. Nothing was validating a signature client-side, because client-side signature
validation is theatre.

There was a third, quieter cost. Because the browser called the API directly, the API had to publish
CORS for the front end's origin, and the front end had to be told the API's address in a
configuration file it downloaded at startup. Two things to keep in step across environments, both of
which fail as a browser-console error rather than as a startup failure.

The conversation that produced this record started elsewhere — with generated HTTP clients — and
arrived here: "de toute façon on est dans un scénario BFF". That was true of the intent and false of
the code.

## Decision

**The Blazor host becomes a real backend for frontend.** It terminates authentication, keeps the
API's access token server-side, and forwards to the API on the browser's behalf.

Concretely:

- **Cookie authentication on the host.** `__Host-bff`, `HttpOnly`, `Secure`, `SameSite=Strict`. The
  challenge and forbid events answer `401` and `403` rather than redirecting: the caller is a fetch
  client, and a redirect to a login page that exists only in WebAssembly routing is noise.
- **The token lives inside that cookie**, via `AuthenticationProperties.StoreTokens`. The cookie is
  encrypted and signed by data protection, so this is not "the token in a cookie" in the sense that
  usually deserves criticism — the browser holds an opaque blob it cannot read, decode, or reuse
  anywhere else.
- **YARP forwards `/api/{**catch-all}`** to the API, stripping the prefix and attaching
  `Authorization: Bearer …` from the cookie in a request transform. The route requires an
  authenticated user, so an anonymous call is refused here rather than one network hop later.
- **Three endpoints the front end talks to directly**: `POST /bff/login`, `POST /bff/logout`,
  `GET /bff/user`. Sign-in is the one thing that cannot be forwarded — the API answers with the
  token itself, and handing that response to the browser would undo the whole arrangement. So the
  host calls the API, keeps the token, and returns a cookie and no body.
- **Identity comes from `/bff/user`.** `BffAuthenticationStateProvider` asks the host who the caller
  is instead of decoding anything. The answer is cached for the application's lifetime and dropped
  on sign-in and sign-out, so an `AuthorizeView` in every page does not mean a request in every
  page.
- **`X-Requested-With: BLRefactoring.Blazor` on every call**, added by a `DelegatingHandler` and
  required by both the `/bff` group and the `/api` prefix. See the trade-offs below for why this is
  the forgery mitigation.
- **The cookie expires with the token it carries.** `ExpiresUtc` is taken from the JWT's own `exp`
  and sliding expiration is off. The default — fourteen days, sliding — would have left users
  apparently signed in while every forwarded call came back `401`.

The front end no longer knows the API's address. It calls its own origin; the host decides what sits
behind `/api`. `Api:BaseAddress` moved from `wwwroot/appsettings.Development.json`, downloaded by
the browser, to the host's `appsettings.Development.json`, which never leaves the server.

`JwtTokenService`, `JwtTokenHandler` and `JwtAuthenticationStateProvider` are deleted. There is no
token in the browser to service.

The generated HTTP clients are untouched. Their paths are relative, so `/Trainer` becomes
`/api/Trainer` by changing one `BaseAddress` — the proxy is invisible to generated code, which is
the argument for putting it at the origin rather than rewriting URLs. ADR 0008's auto-commit loop
continues to work unchanged.

## Consequences

- **An XSS no longer yields a token.** It yields the ability to make calls from the victim's browser
  while the page is open — bounded to this origin, this session, and observable in the host's logs.
  That is a materially smaller prize than a credential the attacker can take away and replay from
  their own machine for the token's full lifetime.
- **Sign-out actually ends the session**, because the credential is on the server side of the
  boundary. Clearing `localStorage` never invalidated anything; a copy taken beforehand kept working
  until `exp`.
- **One origin.** No CORS in this application's path, no preflight, no API address in the browser's
  configuration, and no way to point the front end at the wrong backend. The API keeps its CORS
  policy for callers that are not this front end.
- **A place to put things that had nowhere to go.** Token refresh, per-user rate limits, response
  shaping for the interface, aggregating two API calls into one — all of these now have a host that
  is allowed to know about the front end. That is the part of "BFF" that is not about security.
- **The claims the page renders are the claims the host read**, from a token it had just received
  over a trusted channel. The page decodes nothing.

Against that:

- **Cross-site request forgery comes back.** It is the one attack bearer tokens were immune to,
  because a token has to be attached deliberately and a cookie does not: the browser sends it on any
  request to this origin, including one another site provoked. The mitigation is two-layered —
  `SameSite=Strict`, which stops the ordinary cross-site cases outright, and a required custom
  header, which no cross-site form, image or navigation can set and which a cross-origin script can
  only attempt through a preflight this host never approves. The header is declared once, in
  `BffContract`, and used by both sides; two copies drifting apart would not fail a build, it would
  silently stop refusing forged requests.
- **The host is now on the critical path**, and stateful in a way it was not. Every API call goes
  through a process that has to be up, and the data protection keys that decrypt the cookie have to
  be shared across instances — otherwise a scaled-out deployment signs users out at random when a
  request lands on the wrong node. In development this is invisible, which is exactly why it is
  written down here.
- **A hop of latency** on every call, and a proxy configuration to maintain — currently in memory,
  in code, which is honest for one route and would want `AddReverseProxy().LoadFromConfig` at three.
- **`SameSite=Strict` has a user-visible edge**: following a link into the application from another
  site arrives unauthenticated on the first request. Nothing here relies on deep links from
  elsewhere, so this is a cost with no current payer. `Lax` is the retreat if that changes.
- **The BFF must run over HTTPS.** `Secure` and the `__Host-` prefix are not negotiable, so the
  cookie is simply not set over plain HTTP. The `http` launch profile has been removed rather than
  left as a way to spend an afternoon debugging a login that silently does nothing.
- **Only the layered host is fronted.** The CQRS host is not behind the proxy, because the front end
  does not call it. A second cluster and route is the extension point, not a rewrite.

## Alternatives considered

**Keep the token in `localStorage`.** Zero work, and by far the most common arrangement in the wild.
Rejected on the single fact that motivated this record: it puts a replayable credential where every
script on the origin can read it. Every other property of the design was acceptable; this one is
not, and no amount of care in the application's own code fixes it, because the risk lives in the
dependencies.

**Keep the token in memory only, never persisted.** A genuine improvement over `localStorage` — a
variable in a module is not enumerable by a script that does not already have a reference — and it
costs almost nothing. It loses on two counts. The session dies on every refresh and every new tab,
which is a real interface regression, and it still does not survive XSS: script running in the page
can call the same code that holds the token, or simply call the API through the application's own
client. It reduces casual exposure without changing the outcome of the attack that matters.

**A cookie the browser can read, holding the token.** Named only to reject it explicitly, because it
is the shape people mean when they say "we moved the token to a cookie". Without `HttpOnly` it is
`localStorage` with extra steps and worse ergonomics. The win in this record is not the cookie; it
is that the token is never in the browser.

**Refresh tokens with a short-lived access token in memory.** The other serious answer, and the one
the OAuth working group's browser-based-apps guidance treats as the alternative to a BFF. It
narrows the window an exfiltrated access token is useful for, from an hour to a minute or two. It
lost for a reason specific to this repository and one that is not: the API issues a single JWT with a
single lifetime and no refresh endpoint, so this option is not a front-end change at all — it is an
identity redesign — and even done fully, the refresh token itself then needs somewhere safe to live,
which lands back on this record's question. The BFF answers it once for both.

**Blazor Server, or the interactive-auto render mode.** Removes the problem by removing the
WebAssembly boundary: state lives on the server, and there is no token in the browser because there
is no client to hold one. Rejected as out of proportion. It changes the hosting model, the failure
modes, the scaling story and the offline behaviour of the whole application to solve one thing, and
this repository's point is a WebAssembly front end over an HTTP API.

**Duende.BFF, or another off-the-shelf BFF package.** More complete than what is written here —
session management, back-channel logout, token refresh, silent renewal — and the thing to reach for
in a product. Rejected here because the security-relevant surface is small enough to read in one
sitting (a cookie configuration, a request transform, three endpoints), and a reference repository
that hides its central mechanism behind a package teaches nothing about it. Duende's commercial
licence would also have re-opened, for the same reasons, the argument settled in ADR 0007.

**A dedicated gateway process — YARP standalone, or an ingress-level proxy.** The right shape at
scale, and it separates concerns properly. It lost on the fact that this proxy's job is to attach a
credential from a cookie that the Blazor host issues: putting it elsewhere means sharing the session
between two processes to save the Blazor host a responsibility it is already the natural owner of.
Worth revisiting when a second front end appears.

## Verification

`BLRefactoring.Blazor.Bff.Tests` hosts the real `Program.cs` — pipeline order included — with only
the far side of the proxy replaced by a handler that records what the API was sent. Cookie
authentication, the forgery guard, the authorization on the proxied route and the token transform
are the production ones, so what the suite asserts is what this record claims:

- Signing in answers `204`, a `__Host-bff` cookie marked `HttpOnly`, `Secure` and
  `SameSite=Strict`, and a body that does not contain the token. Neither does the cookie: the ticket
  is encrypted, not a wrapper.
- A forwarded call arrives at the API as `/Trainer/me` — prefix removed — carrying
  `Authorization: Bearer …` that the page never held.
- A call with a valid session but no `X-Requested-With` — the forgery case exactly, since the
  browser attaches the cookie to requests the application did not make — is refused `403` and never
  reaches the API. Without a session it is `401`, also without reaching it.
- The session endpoints are held to the same rule: a sign-out without the header is `403`. That one
  is asserted because it was once wrong — the handler's signature happened to match
  `RequestDelegate`, which bypasses route-handler filters entirely, so a cross-site POST could have
  ended the session.
- After signing out, the same call is `401`. Under the old arrangement a token captured beforehand
  kept working until `exp`.
- `/bff/user` reports `ClaimTypes.Name`, not the `unique_name` the JWT actually carries — the
  translation that keeps `User.Identity.Name` from rendering empty.

What a test cannot show is the absence of storage, so that part is structural: there is no JS
interop left in the front end, and the code that used `localStorage` is deleted. It is still worth
confirming once in a browser that `localStorage` and `document.cookie` hold nothing resembling a
token, and that the network tab shows same-origin calls under `/api` with no preflight.
