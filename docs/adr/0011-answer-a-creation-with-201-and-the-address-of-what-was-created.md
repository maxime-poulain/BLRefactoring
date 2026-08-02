# 0011 — Answer a creation with 201 and the address of what was created

- **Status:** Accepted, amended (see [Amendment](#amendment--the-address-changed-because-the-old-one-stopped-existing))
- **Date:** 2026-08-01

## Context

`POST /Auth/register` answered `204 No Content`. The argument was recorded in the controller's own
remarks and it was not a careless one:

> 204 rather than an empty 200: there is no representation to return […]. 201 would be the other
> candidate — something was created — but it is meant to carry a `Location`, and the account it
> creates has no address of its own: the trainer behind it is only reachable as `/Trainer/me`,
> which needs a token this response does not give.

It is wrong on its own terms. Registration does not create only an identity account; it creates a
**trainer**, in the same transaction, and a trainer is addressable — `GET /Trainer/{id}`, on both
hosts, is the endpoint the rest of this API already publishes for it. `/Trainer/me` was the wrong
resource to measure against: it is an alias for whoever is calling, not the address of the thing the
request created. And the identifier was never missing — the layered stack reads it off the created
aggregate and the CQRS stack generates it into the command before dispatching, which is exactly how
`POST /Training` already knows what to put in its own `Location`.

The rest of the objection — that the address needs a token this response does not hand out — is
true and irrelevant. `Location` identifies the resource created. It does not promise the caller is
authorised to read it, and this caller is one `POST /Auth/login` away from being so.

Reviewing that one endpoint meant reading all of them — eleven under `ApiControllerBase` on each
host, plus the two on `AuthControllerBase` — which turned up a second class of defect:
the document described responses that could not happen, and omitted ones that always could.

| Endpoint | Was | Now | Why |
|---|---|---|---|
| `POST /Auth/register` | `204` | `201` + `Location: /Trainer/{id}` | It creates a trainer, and the trainer has an address |
| `POST /Auth/register` | `400` on a taken username or email | `409` | The request is well-formed; what it asks for is taken |
| `GET /Trainer/all`, `GET /Training/all` (layered) | declared `400` | — | Nothing to bind, nothing that fails: a response no caller could receive |
| Nine actions returning a bare `NotFound()` | declared `void` or nothing for `404` | `ProblemDetails` | `[ApiController]` maps a bare `NotFound()` to a problem document; the declaration said otherwise |
| `GET /Trainer/{id}` (both hosts) | no `400` declared | `400` | A malformed identifier is answered `400` by model binding |
| Every authenticated action | no `401` declared | `401`, declared once on `ApiControllerBase` | The one answer every one of them is guaranteed to be able to give |
| `403` on the two owner-only writes | declared as carrying `ProblemDetails` | declared as carrying nothing | It carries nothing; `[ApiController]` had typed it by default |

`POST /Training` was already `201 Created` with `CreatedAtAction` on both hosts, and needed nothing.

## Decision

**A creation answers `201`, carries `Location`, and repeats the identifier in the body.**

- **`CreatedAtAction`, not a hand-built URL.** The address is generated from the routing table, so a
  route that moves takes its `Location` with it.
- **The identifier is in the body too.** A caller that wants it should not have to parse a URL to
  find it — and this is the shape `POST /Training` already publishes.
- **`AuthControllerBase` asks each stack for the identifier** rather than deriving it, because the
  two know it by different routes. `CreateTrainerAsync` returns `Result<Guid>` instead of `Result`.
- **The action is named by string across assemblies** — `CreatedAtAction("GetCurrent", "Trainer", …)`
  from a base class in `Shared.Api`, resolved against whichever host is running. (It was `"GetById"`
  when this was written; see the amendment at the end.) That is a real weakness, so an integration
  test on each host follows the published `Location` and asserts it serves the trainer. A rename now
  fails the suite instead of the caller.

**A taken username or email is `409`, not `400`.** The distinction this API already draws for a
duplicate training title: a `400` says the request is malformed, a `409` says the request is fine
and the world does not permit it. Everything else Identity rejects — password policy, malformed
email, illegal characters in a username — stays `400`. Identity reports every broken rule at once,
so a request that is both a duplicate and an illegal password answers `409`: the conflict is the
part the caller cannot discover by re-reading their own request, and the whole list travels in the
body either way.

**Every action declares exactly the statuses it can produce.** Not fewer — an undeclared response is
a surprise to a generated client. Not more — a declared response that cannot happen is a branch
every consumer writes and no server ever takes. Concretely: a `404` from an `[ApiController]` is a
`ProblemDetails` because that is what the framework actually sends; a `400` is declared where a
route parameter can fail to bind or a failure path returns one, and removed where neither is true;
and `401` is declared once on `ApiControllerBase`, where `[Authorize]` already is, because it is a
property of every action underneath it rather than of any one of them.

**A declared status carries what it actually carries, including nothing.** `[ApiController]` makes
`ProblemDetails` the type of every error response whose type is left unstated, which is right for
the failures this API writes and wrong for the two it does not: `401` and `403` come from the
authentication and authorization middleware, with no body at all. The default made the document
promise a problem document there, and the generated client obeyed — reading an empty body and
throwing `"Response was null which was not expected."` in place of the status it was handed.
`[ProducesErrorResponseType(typeof(void))]` on the base controller turns that default off; every
error body this API does send is declared explicitly at its own action, so nothing else depended on
it. The `403` had been wrong this way since it was first declared; declaring the `401` is what made
it worth finding.

## Consequences

- The one response a caller of `/Auth/register` could not previously obtain — the identifier of what
  they had just created — now arrives without a second call.
- The generated client changes: `RegisterAsync` returns `Guid` rather than `Task`, and gains a `409`
  branch. CI regenerates and commits it (ADR 0008); the front end discards the value and keeps
  compiling.
- **This is a breaking change to the published contract.** A caller that asserted `204` on
  registration, or branched on `400` for a taken username, must follow.
- Every action now describes itself accurately, so a client generated from the document handles
  what the API sends rather than what it was once thought to send.

Against that:

- **`Location` points at an address the caller cannot yet read.** Accepted, and argued above: it is
  the address of the resource, not a promise of access to it. A caller that follows it before
  signing in gets a `401`, which the document now declares.
- **A 409 on a taken email is an account-enumeration oracle.** It was already one: the `400` it
  replaces carried `DuplicateEmail` in its body, and still does. Closing that would mean changing
  what registration *says*, not what it *answers*, and it is a different decision from this one.
- **The cross-assembly action name is a string.** Guarded by tests rather than by the compiler,
  which is weaker than it looks in an IDE and exactly as strong as it looks in CI.

## Known divergence, not addressed here

`/Auth/register` and `/Auth/login` are the two endpoints whose error bodies are **not**
`ProblemDetails`: registration answers a bare `IdentityError` array and sign-in a bare JSON string.
ADR 0004 says every error body is a problem document, and these two escaped it. Fixing them is a
change to the *shape* of a response rather than its status, it breaks a different set of callers,
and it raises a question this record has no need to answer — which vocabulary Identity's failures
publish, given `ErrorCode` is a closed smart enum and `PasswordTooShort` is not in it. Left
deliberately, and recorded here so it is not mistaken for an oversight twice.

## Alternatives considered

**Keep `204`.** The position this replaces. Defensible only if registration creates nothing
addressable, which stopped being true the moment `GET /Trainer/{id}` existed.

**`201` with `Location: /Trainer/me`.** Tempting, because it is the endpoint the front end actually
calls next. Rejected: `me` is not the address of the created resource, it is an address whose
meaning depends on who asks. Two callers would get the same `Location` for two different trainers.

**`200` with the trainer's full representation.** Saves the caller a round trip and is what a chatty
API would do. Rejected because the caller cannot use it yet — they hold no token — so it would ship
a representation to be discarded, and it would make registration the only creation in this API whose
response shape differs from `POST /Training`.

**`202 Accepted`.** Correct only if the work were deferred. It is not: the identity user and the
trainer are committed in one transaction before the response is written.

**Declaring `401` on each action.** Eighteen attributes saying the same thing, which is how the
declaration would come to be missing on the nineteenth. The base class already carries `[Authorize]`
for the same reason.

**Making `401` and `403` carry a problem document** instead of declaring that they carry nothing.
It would make ADR 0004 true without exception, and it is the more generous answer to a client. It
also means taking over `JwtBearerEvents.OnChallenge`, which is where `WWW-Authenticate` is composed
— the header a 401 is required to carry — and rewriting it by hand for the sake of a body nobody
reads. An empty 401 is what the framework, the specification and every other API do; the defect was
never the empty body, it was the document claiming otherwise.

## Amendment — the address changed because the old one stopped existing

`Location` on `POST /Auth/register` is now `/Trainer/me`. That is the alternative this record
rejected above, and the rejection was right on its own terms: `me` is an address whose meaning
depends on who asks, so two callers get the same string for two different trainers.

What changed is not the argument but the surface. `GET /Trainer/{id}` served any trainer's name,
contact email and bio to any authenticated caller, enumerable by identifier, and nothing in the
application read it — the front end reads the signed-in trainer's profile and nothing else. It was
withdrawn along with four other reads that returned resources the caller does not own. The address
this record named no longer exists, and a `Location` header pointing at a route that answers `404`
is worse than one whose meaning is relative.

The decision itself survives intact: registration still answers `201`, still with the new trainer's
identifier in the body, and still with the address of what it created. Only the address is
different, and it is now the only address that resource has. The header remains the thing the
decision was about — that a creation says what it created and where — and the identifier in the
body is what a caller distinguishing two trainers uses, exactly as before.

`AuthControllerTests` continues to assert that `Location` serves the created trainer; it follows
the header rather than hard-coding the route, so it asserts the same property at the new address.
