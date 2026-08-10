# 0062 — Let the proxy forward one family of paths without a token

- **Status:** Accepted — amended by [0063](0063-strip-the-metadata-before-the-bytes-are-stored.md): the precondition it named is met, and the portrait is published at an address carrying the photo's identity; amended by [0070](0070-open-a-trainers-public-page.md): the detail port gains the profile's reads — an offering trainer's page and portrait, visibility from the index as ever
- **Date:** 2026-08-09

## Context

ADR 0059 built a search index — two tables, nine consumers that maintain it after every commit — and
gave it an anonymous endpoint, `GET /Catalogue/trainings`. Its own consequences say what was left
undone: *"Nothing renders this."* The strategic design says it at more length, in
`bounded-contexts.md`: *"What is missing is no longer a store: ADR 0059 built one and gave it a title
search. What is missing is the experience above it."*

Two things stood between that endpoint and a page, and neither was where one would look first.

**The proxy refused the one endpoint that refuses nobody.** The BFF forwards a single YARP route,
`/api/{**catch-all}`, carrying `AuthorizationPolicy = "default"`. The transform that attaches the
access token is not the obstacle — it adds no header when there is no session, and never fails. The
refusal is the route's policy, and it applies to `/api/Catalogue/trainings` exactly as it applies to
`/api/Trainer/me`. The API's one open door opened onto a corridor with a lock on it.

**Nothing anonymous could be read by identifier.** The index holds `TrainingId`, `TrainerId`,
`Title`, `IsPublished`, `IsTrainerHidden`, and nothing else — no description, no topics, no name.
`GET /Training/{id}` is scoped to its owner inside the query itself, on both stacks. So a page built
on what existed would have shown a visitor a list of titles and two GUIDs.

And a fact that decides the shape of the answer: **no integration event carries a trainer's rename.**
`TrainerNameChangedDomainEvent` has one consumer and it writes an audit line. A name stored in the
index would be a name that nothing refreshes — correct on the day it was written, and wrong from the
next edit of the trainer's profile until something unrelated happened to one of their trainings.

## Decision

**The proxy forwards one family of paths without a token, the API serves it from the one controller
base that can refuse nobody, and an architecture rule holds the two together.**

- **A second YARP route, ordered first.** `/api/Catalogue/{**catch-all}` with YARP's `anonymous`
  policy, `Order = 0`; the catch-all keeps `default` at `Order = 1`. Both matches are catch-alls, so
  the order is written down rather than left to route precedence — a framework's tie-breaking rule
  is not a place to keep a security decision.
- **The forgery guard is untouched, and it is the safety argument.** The `X-Requested-With` check
  runs on everything under `/api`, before authentication, and is unchanged. An open route is not an
  open proxy: a third-party page still cannot make this host forward anything.
- **The token transform is untouched too.** A signed-in visitor's catalogue call still carries their
  token; the API ignores it. What changed is that a call *without* one is no longer stopped at the
  proxy.
- **A public reading of one training**, `GET /Catalogue/trainings/{id:guid}`, on both hosts, and its
  design is the half of this record worth arguing:
  - **Visibility comes from the index.** An entry exists if and only if the training is on offer —
    published, not withheld, its owner in good standing — because that is what the nine consumers of
    ADR 0056 compose into it. The adapter asks whether an entry exists and never why.
  - **Content comes from the write model.** Description, prerequisites, acquired skills, topics —
    and the trainer's name with them, read at the moment of the request. A copy in the index would go
    stale on the next edit; the name would go stale on a rename no fact carries at all.
  - **Two statements, in that order**, in one adapter. A single query joining both tables would read
    the same rows and would also make it possible to write a visibility predicate here, which is the
    one thing this adapter must never do.
- **A port of its own, `ICatalogueDetailQuery`**, rather than a method on `ITrainingSearchQuery`.
  ADR 0059's rule — *"the index is the answer or there is no index"* — is about **searching**, and a
  read by identifier is not a search. The rule's population is files that name `ITrainingSearchQuery`
  *and* call `SearchAsync(`, so this adapter falls outside it by construction rather than by
  exemption: **no existing rule is weakened.** The shape is new, and a new rule holds it.
- **One answer for "no such training" and for "not on offer".** Both are 404. Telling an anonymous
  caller that a training exists and has been taken down is the administration's read (ADR 0055).
- **Two screens, neither behind `[Authorize]`.** `/catalogue` and `/catalogue/{id}`. An endpoint with
  no page is the shape ADR 0059 left behind, and repeating it would have been the same omission with
  a second endpoint attached.
- **The photo stays behind the token, and the reason is named rather than deferred.**
  `GET /Trainer/{id}/photo` has no `[AllowAnonymous]`, and ADR 0021 already says why it should not
  get one: *"a photo taken on a phone carries GPS coordinates, and a public catalogue would publish
  them."* Nothing here strips EXIF. **Stripping it is the precondition for publishing a portrait**,
  and the detail contract gains the reference on the day that exists, not before.

## Consequences

- **ADR 0059 is not amended, and that is a claim rather than an omission.** It decided the search
  path — an index, a query surface, an anonymous endpoint — and every sentence of it stays true. A
  read by identifier is a different question of a different place, so this record cites 0059 to show
  where the line runs instead of claiming an amendment that is not one.
- **Catalogue Discovery stays *Announced* on the map.** What the map leaves open about it is whether
  discovery gets a store of its own, and a page over the same database does not settle that. The
  prose that said the experience was missing is what changes, in both documents.
- **The BFF's route table is now read by a test.** `TheProxysAnonymousPaths_AreExactlyTheApisAnonymousControllers`
  builds the routes the host will actually serve and compares their unpoliced prefixes with the
  controllers deriving from `CatalogueControllerBase`. Widening either half alone is a build failure,
  in both directions: an open path in front of guarded endpoints, and an open endpoint the proxy
  will not forward.
- **One BFF fact keeps its assertion and loses its reason.**
  `A_forwarded_call_without_a_session_never_reaches_the_api` justified itself with *"an anonymous
  call would be forwarded without a token and refused anyway, one hop later"*. That stopped being
  true for `/Catalogue`, so the sentence is rewritten while the fact — 401 on `api/Trainer/me` —
  stands.
- **The home page stops claiming everything requires an account.** It said *"Everything past this
  page requires an account"*, which this record makes false. What an account is for is publishing.
- **The prerendering question is reopened and not answered.** `App.razor` argues against prerendering
  partly on the grounds that *"the application is entirely behind authentication, so there is no SEO
  to gain either"*. Two public pages make half that sentence false. The other half — that
  prerendering a page whose data needs the session's cookie buys a flash of empty state — is
  untouched, and it is the half that decided. Naming this here is what keeps it from being noticed
  as a contradiction later.
- **A visitor's catalogue call is the first request in this system with no identity at all.** Nothing
  in the pipeline reads one for these two endpoints: no policy, no `ICurrentUserService`, no standing
  check. The log's identity enricher (ADR 0027) writes nothing for them, which is correct and worth
  expecting when reading those lines.

## Alternatives considered

**Make the whole `/api` prefix anonymous and let the API refuse.** One route instead of two, and the
API is the authority either way. It also turns every mistake in an `[Authorize]` attribute into a
public endpoint instead of a 401, and removes the layer that would have caught it. Defence in depth
is cheap here: the cost of the second route is six lines and an explicit `Order`.

**Serve the catalogue from a different origin, with no proxy in front.** Honest, and it splits the
front end in two: a second host, a second deployment, a second place for the `X-Requested-With`
contract to be forgotten.

**Store the description and the trainer's name in the search index.** One query instead of two, and a
detail page that never touches the write model. It is the option that looks like the read model doing
its job, and it fails on the rename: nothing publishes one, so the index would carry a name until
some unrelated fact about a training happened to refresh it. The description is the same defect with
a shorter fuse.

**Read visibility from the write model, since the adapter is already there for the content.**
`Where(t => t.Status == Published && ...)` — three columns, no second statement. It is a second
definition of "on offer" beside the one the nine consumers of ADR 0056 exist to compose, and the two
would agree until the tenth reason to hide a training was added to one of them.
`TheCatalogueDetail_TakesItsVisibilityFromTheIndex` refuses it.

**Publish the trainer's identifier, as the search row does.** It would keep the two contracts
consistent. The search row's argument is that *"publishing every trainer's name to anybody is
precisely the read this API withdrew"* — a directory of people, obtainable by paging. One name on one
training its owner chose to publish is authorship, not a directory, and a page whose author is a GUID
is a page nobody can use.

**Make the photo anonymous while we are here.** It is one attribute, and it would publish whatever
GPS coordinates the phone that took the portrait wrote into it. ADR 0021 named this before there was
a public page to publish it on; the answer is to strip the metadata first.

## Verification

- **`TheProxysAnonymousPaths_AreExactlyTheApisAnonymousControllers`**, watched failing twice, once in
  each direction: with the anonymous route widened to `/api/{**catch-all}` (both violations at once),
  and with a second controller derived from `CatalogueControllerBase` and no route opened for it.
- **`TheCatalogueDetail_TakesItsVisibilityFromTheIndex`**, watched failing twice as well: with
  `candidate.Status == TrainingStatus.Published` added to the content query, and with the index
  statement replaced by a read of the trainings table.
- **The BFF, against the real proxy and the real pipeline**: an anonymous call to
  `api/Catalogue/trainings` reaches the API **with no `Authorization` header** — the recording
  handler sees the absence — a signed-in visitor's catalogue call carries their token, and the same
  anonymous call **without `X-Requested-With` is still 403**. The fact that a session-less call to
  `api/Trainer/me` is refused stands, with its reason rewritten.
- **`CatalogueDetailQueryTests`**, against SQLite through the real model: an offered training answers
  with its trainer's name, one the index does not hold answers nothing, an unpublished entry and a
  hidden trainer each answer nothing, an unknown identifier answers `null`, and after a rename the
  name answered is today's. The correlated subquery that reads the name has to translate, and this is
  what says it does.
- **Both stacks, at the same port**: `CatalogueApplicationServiceTests` and
  `GetOfferedTrainingQueryHandlerTests` assert the same two things of the layered service and of the
  CQRS handler, because a fact on one host says nothing about the parity ADR 0006 promises.
- **`GetOfferedTrainingQueryValidatorTests`**: the identifier is refused empty by the message's own
  validator as well as by the route constraint (ADR 0046).
- **bUnit**: the listing asks for the first page and lets the server size it, a row links to the
  training's page, a term resets to page one, and — the fact this record is about — the listing
  renders **with no authentication state provided at all**. What that cannot see is an `[Authorize]`
  attribute, which the router honours and a directly rendered component does not; the route's
  anonymity is pinned one layer out, by the proxy's rule.
- **The two hosts, over HTTP**, in `CatalogueDetailTest`: the reading is served without a token, a
  training a moderator has withheld answers 404 to a visitor although it exists, and the trainer's
  name is on the answer.
- **`BothHosts_PublishTheSameOperations`** holds: the action is written twice, named identically.
- **No migration.** Nothing about the schema changes; the detail reads columns that already exist.
