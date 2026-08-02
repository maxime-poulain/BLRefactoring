# 0010 — Declare the conditional-request contract in the document

- **Status:** Accepted
- **Date:** 2026-08-01

## Context

Editing a training from the Blazor front end could not work. Not intermittently — never.

`PUT /Training/{trainingId}` requires an `If-Match` carrying the version the caller read, and answers
`428 Precondition Required` without one. The generated client has no `If-Match` parameter: in 2 300
lines of `Clients.Generated.cs` there is not one `request_.Headers.TryAddWithoutValidation(…)`. So
`CreateTraining.razor` sent an unconditional PUT and got a 428 back, every time, presented to the
user as a generic "the request was rejected".

The client was not at fault, and neither was NSwag. The header was read by hand:

```csharp
return EntityTag.TryParse(controller.Request.Headers.IfMatch.FirstOrDefault(), out expectedVersion);
```

`[FromHeader]` appeared nowhere in the repository. ApiExplorer had nothing to reflect, so the header
never entered the OpenAPI document, so no generator could invent it. The same was true in the other
direction: `SetETag` writes the `ETag` inside the method body, and nothing declared that either — a
caller reading the document would have learned it must send a version, and had no way to learn where
one comes from.

The generated client did know about the outcomes: `412` and `428` appear in it as
`ApiException<ProblemDetails>` throws, picked up from `[ProducesResponseType]`. It could recognise
the failure it was guaranteed to receive, and could not avoid it.

What makes this worth a record rather than a bug fix is the shape of the failure. Everything worked:
the API was correct, the client compiled, the document was valid, 385 tests were green, and four
integration tests asserted the 428 behaviour precisely — using a raw `HttpClient` in
`ConditionalRequestHelper`, which bypasses the generated client entirely. The only thing that failed
was the one path no test crossed. **What is not in the document does not exist for a client**, and
nothing in the repository was checking what was in the document.

## Decision

**Both halves of the contract are declared, and a test asserts they stay declared.**

- **`[FromHeader(Name = "If-Match")] string? ifMatch`** on the four editing actions —
  `Training.UpdateTraining` and `Trainer.EditCurrent`, on both hosts. The requirement becomes part of
  the signature, which is what it always was in fact, and the document follows with no further
  machinery. `ConcurrencyControllerExtensions.TryGetExpectedVersion` is deleted; the actions call
  `EntityTag.TryParse` themselves.

  **The parameter is nullable, and that is not incidental.** With nullable reference types enabled, a
  non-nullable parameter bound from a header is implicitly required to `[ApiController]`, so a
  request without the header would be answered `400` by model validation instead of the `428` this
  endpoint owes it — silently converting a documented outcome into a generic one.

- **`[ProducesEntityTag]`**, a marker attribute, on the ten actions that publish an `ETag`, read by
  a new `EntityTagTransformer : IOpenApiOperationTransformer` that writes the header onto the `200`.
  ASP.NET Core has no attribute for response headers, so this is the only way to say it. It is
  declarative rather than a convention over verbs because emitting an `ETag` is a per-action choice:
  the reads by identifier publish one, the collection endpoints do not.

- **`wrapResponses: true`, restricted by `wrapResponseMethods`** to the three reads that carry an
  `ETag` — `TrainingClient.GetTrainingById`, `TrainerClient.GetCurrent`, `TrainerClient.GetById`.
  NSwag discards response headers on the success path unless the result is wrapped in
  `SwaggerResponse<T>`, so without this the client can send a version it has no way to obtain.

  Those names are the *generated* ones — NSwag matches on
  `GenerateControllerName(ControllerName) + "." + ActualOperationName`, so the `{controller}Client`
  template applies and the `Async` suffix does not. An entry that matches nothing wraps nothing,
  silently: the setting has no way to tell a typo from a deliberate omission.

- **Both stacks change together.** The client is generated from the layered host only, but the two
  hosts publish the same API and this repository's standing rule is that they do not drift.

## Consequences

- The front end can edit again, which was the point.
- The document now describes the whole exchange: read the resource, keep the `ETag`, send it back as
  `If-Match`. Scalar shows it, and any generator — not only NSwag — can act on it.
- The requirement is visible in the action signature, where a reader looks first. A future action
  that needs a version has an example to copy rather than an extension method to discover.
- Two assertions in `OpenApiDocumentTest` now fail if either half is dropped. They run against both
  hosts, which is where the previous document defect (ADR 0006) also hid.

Against that:

- **The generated client is asymmetric.** Three methods return `SwaggerResponse<T>` and the rest
  return the payload. The rule is memorable — the ones that publish a header wrap — but it is not
  visible from a call site, and knowing which is which means reading `generator.nswag`. The
  alternative was wrapping everything, which is discussed below.
- **A marker attribute that does nothing at runtime** is a thing a reader can misread as load-bearing.
  It is documented as declarative on its own definition, and the transformer is the only code that
  looks at it.
- **Nothing forces the two to agree.** An action can call `SetETag` and forget `[ProducesEntityTag]`,
  or carry the attribute without emitting anything, and only a reader would notice. A test asserting
  that every action calling `SetETag` carries the attribute would need to inspect method bodies; the
  honest mitigation is that both live on the same line of the same method.

## Alternatives considered

**A `DelegatingHandler` that remembers `ETag`s per URL and replays them as `If-Match`.** The cheapest
option by a distance: no API change, no generator change, no regeneration, and the Blazor page would
not have been touched at all. Rejected because of what it hides. The mechanism deciding whether one
user overwrites another's work would become invisible at every call site, implicit in its timing, and
dependent on ambient state keyed by URL — including across pages that read the same resource for
unrelated reasons. A repository that exists to be read should not bury that.

**Add the parameter with an operation transformer instead of binding it.** Would have put `If-Match`
in the document without touching a single action signature, which is a real advantage: no
`[FromHeader]`, no nullability trap, no risk of changing model binding. Rejected because it leaves
the actual reading of the header where it was — invisible to the framework — and keeps the document
and the code as two things that have to be kept in step by hand. The transformer would describe a
parameter that nothing declares. That is the same class of arrangement that produced this defect.

**Put the version in the response body.** `TrainingResponseHttp` and `TrainerResponseHttp` both state
explicitly that the version travels in the `ETag` and is deliberately absent from the body, so this
reverses a documented decision — but it would have made the version reachable by any client with no
generator configuration at all, which is a serious argument. It lost on what the field would have to
contain: for a client to pass it straight back, it must be the entity tag verbatim, quotes included,
which means shipping `"etag": "\"AAAAAAAAB9E=\""` in JSON. Any other encoding puts entity-tag
formatting rules into every client. The redundancy was acceptable; that field is not.

**`wrapResponses` for every operation.** Uniform, predictable, and arguably what an HTTP client should
be: every response exposes its headers. It lost on cost against benefit — six Blazor call sites to
unwrap today and every future one after that, to carry headers that five of those six operations do
not have and are not going to have. Worth revisiting if a second header ever matters, at which point
the selective list stops being a small exception and starts being a maintained inventory.

## Verification

- The four existing integration tests — `Edit_WithoutIfMatch_Returns428`,
  `Edit_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit`, and their trainer twins — pass unchanged.
  Binding replaces a manual read without moving a single status code.
- `Document_DeclaresTheIfMatchHeader_OnConditionalWrites` and
  `Document_DeclaresTheETagHeader_OnTheReadsThatPublishOne` run against both hosts, and fail if
  either declaration is removed.
- The regenerated client carries an `ifMatch` parameter on both PUTs and returns
  `SwaggerResponse<T>` from the three wrapped reads. CI regenerates and commits it (ADR 0008), so
  this is observed rather than predicted.
- End to end, in a browser: opening a training, editing it and saving answers `200` rather than
  `428`. Opening it in two tabs and saving in both gives the second one a message saying someone
  changed it first — and does not clear what was typed.

## Addendum — the CQRS host's edit now republishes its version

Written with this record rather than after it, because it is the same defect seen from the other
end. `PUT /Training/{id}` on the CQRS host answered a bare `200`: no body, no `ETag`. A caller was
left holding the version the edit had just superseded, so editing twice in a row was a guaranteed
`412` with no way forward but another `GET`.

It was not a property of CQRS. `TrainerController.EditCurrentAsync` on the same host already reads
the updated profile back through the query side and republishes its `ETag`; the training edit was
the odd one out among the four editing endpoints. It now does the same, and
`Edit_TwiceInARow_SucceedsUsingTheVersionTheFirstEditReturned` asserts the sequence the old
behaviour made impossible.
