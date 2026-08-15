# 0048 — Qualify a contract before naming what it is

- **Status:** Accepted — amended by [0081](0081-name-a-query-for-what-it-retrieves-and-what-scopes-it.md): the query half of the CQRS vocabulary gains a convention — a retrieval verb, what is retrieved, and the criterion as ByX
- **Amends:** [0042](0042-close-the-boundarys-vocabulary.md)
- **Date:** 2026-08-07

## Context

ADR 0042 closed the boundary's vocabulary: what a client sends and receives is a `*RequestHttp` or a
`*ResponseHttp`, declared under `Contracts/`, and the application layer keeps `*Request` and `*Dto`.
The separation was the point, and it still is. What this record changes is only where the qualifier
sits.

`CreateTrainerRequestHttp` reads as *create-trainer request, HTTP* — a qualifier stranded after the
noun it qualifies. English puts it in front: an HTTP request, not a request-HTTP. Thirteen types
carry the awkward order, and every reader of this repository meets them first, because they are the
surface.

**The reason this is a record rather than a rename.** The old suffix did work the new one cannot:
`CreateTrainerRequestHttp` does not end in `Request`, so the two vocabularies were separable by
suffix alone. `CreateTrainerHttpRequest` does end in `Request`. Anything that told an HTTP contract
from an application request by reading the end of the name — a rule, or a person skimming — stops
being able to.

That is not hypothetical. `EveryLayeredServiceSignature_SaysWhichBoundaryItIsOn` requires a layered
application service to take a type ending in `Request`. Under the old names, an HTTP contract handed
straight to the application layer failed that rule. Under the new ones it would satisfy it: the guard
would not disappear, it would turn green for the violation it exists to catch. A rename that stopped
at renaming would have traded a reading improvement for a silent hole.

## Decision

**The qualifier comes first, and the rules read the assembly rather than the suffix.**

- **An API assembly's contracts are `*HttpRequest` and `*HttpResponse`**, still declared under
  `*.Api.Contracts` and still shared by both hosts. `CreateTrainingHttpRequest`,
  `TrainerHttpResponse`, `PagedHttpResponse<T>`.
- **An Application assembly's inputs stay `*Request`, its outputs `*Dto`**, with no HTTP qualifier
  anywhere. `TrainerEditionRequest` and `TrainerDto` are untouched, and the CQRS stack keeps its
  `*Command` and `*Query`.
- **The convention is enforced per assembly, not per string.** A rule that asks "does this name end
  in `Request`" can no longer answer which side of the boundary a type is on, so no rule asks that
  alone any more. What an action binds or answers must be named for the boundary; what an inner layer
  declares must not be. The two halves are separate rules because they can fail separately.
- **The wire does not move.** JSON property names come from serialisation, not from type names, so
  every request and response on this API is byte-for-byte what it was. What changes is the *schema
  names* in the OpenAPI document, and with them the classes NSwag generates — which is visible,
  committed, and reviewed like any other change to the published document.

## Consequences

- **Thirteen types, and everything that names them.** The contracts, both hosts' controllers and
  mappings, the shared TestKit facts, both integration suites, the Blazor front and its BFF, and the
  generated client.
- **The generated client is renamed with the schemas.** ADR 0008 makes that automatic and reviewed:
  the script regenerates, CI commits, and the diff is read rather than assumed. Operation identifiers
  are unaffected — they come from controller and action names — so
  `BothHosts_PublishTheSameOperations` sees nothing.
- **One guard is repaired and one is added.** `EveryLayeredServiceSignature_SaysWhichBoundaryItIsOn`
  now excludes `*HttpRequest` explicitly, restoring what the rename would have cost it.
  `NoInnerLayer_DeclaresATypeNamedForTheTransport` is new: the kernel, the domain, both application
  layers and infrastructure may not *declare* a type named for HTTP. It stands beside the older
  `NoInnerLayer_NamesAnHttpContract`, which catches an inner layer that *uses* one in a public
  signature — a type nobody passes around is invisible to that one and caught by this one. Between
  the three, neither `RequestHttp` nor a contract leaking inwards can return without a rule going red.
- **`EveryTypeOnTheBoundary_IsAContract` now carries two records.** It defended ADR 0042's closed
  vocabulary — a published type is a contract, declared under `Contracts/` — and it still does; what
  this record adds is how that contract is named. Both quotes sit on the same rule because breaking
  either breaks it, and the traceability rule reads the attributes rather than counting them.
- **`CLAUDE.md`'s sentence about the suffix becomes false and is rewritten.** It said *"The suffix
  says which boundary a type belongs to"*. The qualifier does now, and the file has to say so — a
  convention document that describes the previous convention is worse than none.
- **Every record before this one keeps the names it was written with.** ADR 0001, 0004, 0008, 0010,
  0015, 0016, 0029, 0042, 0043 and 0046 quote `*RequestHttp` because that is what was decided at the
  time. Rewriting them would make the history agree with the present at the cost of being false about
  the past, which is the one thing `docs/adr/README.md` says a record may never do.

## Alternatives considered

**Keep `*RequestHttp` / `*ResponseHttp`.** The strongest case against changing anything, and it is
not about taste: the suffix made the two vocabularies mechanically separable, and this record spends
two rules buying that property back. Rejected because the property is recoverable and the awkwardness
is not — the names are read far more often than the rules are written, and a convention that has to
be explained every time it is read is paying interest forever.

**Bare `*Request` / `*Response` for the contracts.** The most idiomatic English of the three, and the
worst fit here: `CreateTrainerRequest` would be indistinguishable from the application layer's own
input by *any* means, name or rule. That is precisely what ADR 0042 was written to prevent, three
months after `RegisterRequest` and `LoginResponse` sat unqualified at the bottom of a controller base
and nobody could say which layer they belonged to.

**`*HttpRequest` / `*HttpResponse`.** Chosen. It reads as English, it names the transport before the
role, and it keeps a qualifier the application layer never uses — so the vocabularies remain
distinct, just not by suffix. The cost is that the distinction now lives in the middle of the name
rather than at its end, which is exactly why the rules had to change with it.

**A collision with ASP.NET Core was weighed and is not one.** `Microsoft.AspNetCore.Http.HttpRequest`
and `System.Net.Http.HttpRequestMessage` exist, and nothing here is named either: every contract is
suffixed by what it is for, so no ambiguity reaches the compiler. What remains is a reading cost in
controllers, where `HttpContext.Request` is an `HttpRequest` — small, and the price of naming a thing
after what it is.

## Verification

`EveryTypeOnTheBoundary_IsAContract` takes its population from the actions themselves — what they
bind and what they answer, unwrapped through `Task`, `ActionResult` and the paging envelope — so it
judges the types that really are contracts rather than everything in an assembly. It now requires
`HttpRequest` or `HttpResponse`.

`NoInnerLayer_DeclaresATypeNamedForTheTransport` scans the kernel, the domain, both application
layers and infrastructure for a *declared* type named for HTTP, and names the layer that would be
reaching outward. Watched red by declaring a `TrainerHttpResponse` in the shared application layer.

`EveryLayeredServiceSignature_SaysWhichBoundaryItIsOn` requires a `*Request` that is not an
`*HttpRequest`, and the exclusion was measured rather than reasoned about. A layered service was
given a method taking a `TrainerEditionHttpRequest`: with the clause, the rule goes red on it; with
the clause removed and nothing else changed, the same violation turns the rule **green**. That is the
hole this record predicted, observed — and the only evidence that the repair holds anything.
