# 0063 — Strip the metadata before the bytes are stored, and publish only what was stripped

- **Status:** Accepted — amended by [0070](0070-open-a-trainers-public-page.md): the identifier this record would not hand out is handed out on purpose, now that a person has a page to be — the directory ADR 0055 withdrew stays withdrawn
- **Amends:** [0009](0009-hold-the-access-token-in-the-bff-instead-of-the-browser.md), [0021](0021-store-a-photo-beside-the-row-that-names-it.md), [0062](0062-let-the-proxy-forward-one-family-of-paths-without-a-token.md)
- **Date:** 2026-08-10

## Context

ADR 0021 stored a portrait and named a debt in the same breath: *"a photo taken on a phone carries
GPS coordinates, and a public catalogue would publish them."* It deferred stripping them because
there was no public catalogue to publish them on, and it said what that deferral would cost —
*"the only decision here that gets more expensive with time"*, because every portrait uploaded
before the stripping exists is one nobody can prove anything about afterwards.

ADR 0062 built the catalogue. It stopped one step short on purpose, and wrote the step down:
*"Stripping it is the precondition for publishing a portrait, and the detail contract gains the
reference on the day that exists, not before."* This record is that day.

Three things were found on the way, and each changes what the answer had to be.

**EXIF orientation is metadata that decides which way up an image is.** A camera writes the sensor's
raw pixels and a tag saying how to rotate them. Stripping the tag without applying it turns a
portrait on its side — the correction is silent, and it happens to every photo taken by holding a
phone normally. So "strip the metadata" cannot mean "drop the metadata": it means read the one piece
that is not merely descriptive, apply it to the pixels, and then keep none of it.

**The BFF's forgery guard refused this application's own images.** `UseBff` answers 403 to any
`/api/**` that does not carry `X-Requested-With: TrainingHub.Blazor`, before authentication.
`BffContract` documents why that works: *"A cross-site form, image or navigation cannot set a custom
header."* That sentence is true and has a second half nobody had written down — **our** images cannot
set one either. `Profile.razor` renders `api/Trainer/{id}/photo?v={photoId}` inside a `<MudImage>`,
which is an `<img>`, which is issued by the browser and carries no header the page chose. **Every
portrait this front end has ever tried to display was answered 403 before reaching the API.** Nothing
saw it, because nothing renders a real page against a real BFF: the BFF suite proves the opposite
fact, that a call without the header is refused.

**Sanitising before validating answers away two of the aggregate's own rules.** This one was found
while writing the record rather than before, and it is the reason the decision below has three steps
instead of two. A sanitiser re-encodes into the format it is told, so a JPEG uploaded as
`image/png` comes back a genuine PNG and `Trainer.PhotoContentMismatch` has nothing left to refuse;
and it bounds the longest side, so a photograph past `TrainerPhoto.MaxSizeInBytes` comes back well
under it and `Trainer.PhotoTooLarge` never fires again. Both rules are about *the upload*. Asking
them of the bytes that come back is asking them of something else.

**The `immutable` on the authenticated photo endpoint was a promise its address could not keep.**
`GET /Trainer/{id}/photo` does not name the photo, so its bytes change when its owner uploads a new
one — and `immutable` tells a browser the body for this URL will never change, so it stops asking for
a year. The `?v={photoId}` in `Profile.razor` is what has been papering over that, and its comment
called the arrangement *"the whole trick"* rather than a workaround.

## Decision

**Metadata is stripped when the bytes arrive, the domain records that it was, and only a portrait
carrying that record is published.**

- **A port beside the use cases, `IPhotoSanitiser`,** answering `Result<SanitisedPhoto>` from bytes
  and a declared media type. It is an application-layer port rather than a domain one: what it does
  is decode and re-encode an image, which is a technical capability the domain has no vocabulary for.
- **One adapter, `SkiaSharpPhotoSanitiser`,** and SkiaSharp rather than ImageSharp because ImageSharp's
  licence turned commercial at 3.0 and this is a showcase repository. It reads the encoded origin
  through `SKCodec` — `SKBitmap.Decode` discards it silently — applies all eight orientations as a
  matrix, bounds the longest side to 1024 pixels, and re-encodes. What comes out has no EXIF segment,
  no XMP, no colour profile and no thumbnail, because it was written from pixels.
- **Three steps in this order, on both stacks: vet, strip, describe.** `TrainerPhoto.Vet` judges the
  upload and answers the media type its bytes really are; `IPhotoSanitiser` strips them; and
  `TrainerPhoto.Create` describes what will actually be stored. The split is what keeps the two
  rules above alive — they are questions about what a caller sent, so they are asked of what the
  caller sent — while the media type and byte count the aggregate records still describe the bytes
  that were kept rather than a file nobody has. `Create` re-reads the signature anyway, for
  ADR 0046's reason: it is what anything reaching the aggregate goes through, and it does not assume
  the layer above asked first.
- **`TrainerPhoto.SanitisedOnUtc`, nullable, and `MayBePublished` reading it.** `Create` always
  stamps it, so the domain can no longer mint an unstripped photo: a null can only have come out of a
  row written before this record. The absence is a fact about history, not a state this code can
  produce.
- **Nothing is backfilled.** The migration adds a nullable column and every existing row keeps its
  null. A portrait stored before this record was never stripped, nothing here can prove otherwise,
  and writing a date would be inventing evidence. Its owner uploading it again is what makes it
  publishable, and costs them one action.
- **The public portrait is `GET /Catalogue/trainings/{trainingId:guid}/photo/{photoId:guid}`,** on
  both hosts, on the existing `CatalogueController`. Two properties fall out of that address and both
  are the reason for it:
  - **It names a training and a photo, and never a person.** A visitor followed a catalogue entry, so
    the training is what they have; a trainer's identifier is not something a catalogue hands out.
  - **It carries the photo's identity, so `immutable` is true by construction.** A replacement mints
    a new identity, so *this* URL's bytes genuinely never change.
- **Four ways to answer 404, and they are one answer:** no such training, none on offer, a photo that
  is not the owner's current one, and a portrait carrying no stamp. A visitor is owed no more than
  that (ADR 0055).
- **`ICatalogueDetailQuery` gains the read**, in the adapter that already holds the sharing of
  authority ADR 0062 decided: the index says whether the training is on offer, the write model says
  which photo its owner has. So `TheCatalogueDetail_TakesItsVisibilityFromTheIndex` covers both reads
  without a line being added to it.
- **The guard admits a safe same-origin read.** `UseBff` now passes a request that carries the
  application's header **or** is a `GET`/`HEAD` the browser itself attests came from this origin via
  `Sec-Fetch-Site: same-origin`. That header is set by the browser and its name is on the forbidden
  list, so script cannot forge it. **The relaxation is reads only**: a write without the header is
  still 403, which is the case the guard was written for.
- **`TrainerPhotoControllerExtensions` splits into `PhotoFile` and `ImmutablePhotoFile`,** named
  rather than one helper taking a flag. The authenticated endpoint drops `immutable` and keeps
  `max-age` with an `ETag`; the public one keeps `immutable`. Which promise an endpoint makes about
  its own bytes is a property of its address, and a caller passing `true` is a caller who has had to
  think about it — one had not.
- **`ITrainerPhotoStore.FetchAsync` narrows to `(TrainerId, PhotoId)`.** The key is
  `trainers/{trainerId}/{photoId}`, and a reader may have no aggregate at all: the catalogue's read
  side projects columns, and materialising a value object to compute a key out of two of them would
  be work done to satisfy a signature. `StoreAsync` keeps the value object — it needs the media type.

## Consequences

- **ADR 0021's deferral is discharged, and its sentence about the cost was right.** Every portrait
  uploaded between that record and this one is unpublishable until its owner replaces it. That is the
  interest on the debt, paid in one action per trainer rather than in a backfill that would have
  lied.
- **ADR 0062's precondition is met, and its last bullet is now history rather than a plan.** The
  detail contract gains `TrainerPhotoId`, which is the reference that record said would arrive "on
  the day that exists".
- **ADR 0009's forgery guard is narrowed, and its intention is unchanged.** It was written to stop a
  third-party page provoking an authenticated call; it also stopped this application's own images,
  which was never the intention and never noticed. With `SameSite=Strict` the cookie does not travel
  cross-site at all, so for a **safe** read the header was adding nothing the cookie policy did not
  already give. For a write it adds everything, and there it stands.
- **A browser too old to send `Sec-Fetch-*` loses images and no function.** It falls through to the
  header, which the application still sets on every call it makes itself.
- **The catalogue's detail page renders a face.** `/catalogue/{id}` shows the portrait when
  `TrainerPhotoId` is there and a name alone when it is not — which covers "no photo" and "a photo
  nothing can prove was stripped" identically, a distinction a visitor has no use for.
- **The detail's projection applies the same predicate as the portrait endpoint.** Publishing an
  address the endpoint would answer 404 renders a broken image, which is a worse answer than no
  image.
- **`SanitisedOnUtc` is written as a column comparison rather than through `MayBePublished`.** The
  domain's predicate is a computed property on a value object inside a complex property, which EF has
  nothing to translate. ADR 0028 asks a specification to be one expression answering both in memory
  and as query criteria; this one cannot be, so the limit is stated where it bites rather than dressed
  up as something the domain owns.
- **An image codec now runs on a request thread, on bytes a stranger uploaded.** That is the largest
  attack surface this product has, and it is the reason `OnlyTheInfrastructure_DecodesAnImage` exists:
  one place decoding one is a place that can be reviewed.
- **`PhotoTest` sends real images now, and one of its facts changed what it is about.**
  `UploadThenRead_GivesBackTheSameBytes` was true and is now wrong: what comes back has been decoded
  and re-encoded. The correction that suggests itself — assert the bytes come back *different* — is
  wrong too, and CI is what said so: re-encoding a picture this same library produced, carrying no
  metadata, already inside the bound and upright, is deterministic and gives identical bytes back.
  Byte difference is an accident of the fixture. What is a property of the endpoint is that the
  metadata does not survive, so the upload now carries an EXIF description and the fact asserts it
  is gone. The refusal facts still send a signature followed by zeroes on purpose — what they prove
  is refused before anything decodes it.
- **Re-encoding is lossy and deliberate.** A 4000-pixel photograph comes back at 1024 and a JPEG is
  re-compressed at quality 90. What is lost is resolution nobody displays; what is gained is that no
  byte of the original container survives.
- **`SKCodec.EncodedOrigin` is load-bearing and easy to remove.** A future rewrite reaching for
  `SKBitmap.Decode` because it is shorter would silently start publishing sideways portraits.
  `SkiaSharpPhotoSanitiserTests` has the fact that catches it, and it was watched failing.
- **A native library is now on the deployment's critical path.** `SkiaSharp.NativeAssets.Linux.NoDependencies`
  bundles its own font and image codecs rather than requiring `libfontconfig1` on the host, which is
  what makes the container image unchanged.

## Alternatives considered

**Sanitise first and let one factory do everything.** It is what this branch shipped for two commits
and it reads better: one call, one place, bytes in and a photo out. It also silently retired
`Trainer.PhotoContentMismatch` and `Trainer.PhotoTooLarge`, because a rewriter that runs first makes
both questions unanswerable. Nothing failed — the two facts that would have caught it are in an
integration suite, and the unit tests were asking the factory directly.

**Strip on the way out rather than on the way in.** Serve the stored bytes through a sanitiser on
every public read. It keeps the original, which is the argument for it, and it means the coordinates
are in the object store forever — one misconfigured bucket policy away from being public, and one
future endpoint away from being served raw. Stripping on the way in makes the dangerous version not
exist.

**Strip inside `TrainerPhoto.Create`.** It is where the bytes are already being inspected. It would
put an image codec in the domain, give the aggregate a reason to know about pixels, and make the
media type it records a claim about an upload rather than about what was stored.

**Backfill every existing photo by re-encoding it.** One migration, and every portrait becomes
publishable. It is also a claim that these bytes were stripped, made by a process that never saw the
originals as they were uploaded — and if it were run twice, or half-run, nothing would say which rows
it had reached. A null that means "unknown" is worth more than a date that means "we ran something".

**Publish the portrait at `/Catalogue/trainers/{trainerId}/photo`.** Shorter, and it matches the
authenticated endpoint's shape. It also publishes a trainer's identifier to anybody, which is exactly
the read ADR 0055 withdrew, and it makes the address unable to carry `immutable`.

**Keep the guard as it was and give the `<img>` a token in the query string.** It would leave the
guard alone. It puts a credential in a URL, which is logged by every proxy between here and there,
and it is a larger change to the security model than the one this record makes.

**Relax the guard for every same-origin request rather than for safe ones.** Simpler to state, and it
hands back exactly the case the guard exists for: a same-origin `POST` that the application did not
make. The methods are what makes this a correction rather than a hole.

**Keep one `PhotoFile` helper and pass a flag.** Two lines shorter. It is also how the wrong promise
got made in the first place — the second caller inherited a decision the first had made about a
different address.

## Verification

- **`TrainerPhotoTests`**, on `Vet` as well as `Create`, and the two facts that matter are the two
  rules the wrong order had answered away: a photograph one byte past the limit is refused before
  anything can shrink it, and a JPEG declared `image/png` is refused before anything can re-encode
  it into one.
- **`SkiaSharpPhotoSanitiserTests`**, six facts against the real library on this Linux box, because
  "the native assets load" is not something a compiler can say. A JPEG carrying a hand-built APP1
  segment comes back with neither `"Exif"` nor the GPS string in its bytes; an oversized image comes
  back bounded; **an image whose orientation tag transposes its sides comes back with its sides
  swapped**, watched failing with `Expected width to be 50, but found 100` before the matrix was
  written; unreadable bytes answer `Trainer.PhotoUnreadable` rather than throwing.
- **`OnlyTheInfrastructure_DecodesAnImage`**, watched failing with a `SkiaSharp` type used from
  `TrainingHub.Shared.Api`.
- **`NoUnsanitisedPortrait_IsPublished`**, watched failing with the stamp's predicate removed from
  `CatalogueDetailQuery`.
- **The BFF, against the real proxy and the real pipeline**: a same-origin `GET` with
  `Sec-Fetch-Site` and no application header **reaches the API**; the same request from a third-party
  site is 403; a same-origin `POST` without the header is 403; and a request that says nothing at all
  about where it came from is 403. The first of those was watched failing before the guard changed —
  it is the fact that proves the defect this record found.
- **`CatalogueDetailQueryTests`**, against SQLite through the real model: the happy path across an
  index entry, a training row, a flattened value object with a converted identifier and an object
  store keyed by two of its columns; and every way to answer nothing, including **a row written with
  the column `NULL`**, which is the only way to produce a photo from before this record and is
  written with raw SQL because the factory always stamps.
- **The two hosts, over HTTP**, in `CatalogueDetailTest`: a portrait deposited and then read **with no
  token** through an offered training, a photo with no stamp answering 404, and a withheld training
  making its owner's portrait unreachable. And in `PhotoTest`, the one fact that can only be asserted
  here: a JPEG uploaded with an EXIF description comes back without it, which says the pipeline calls
  the sanitiser at all rather than that the sanitiser works.
- **bUnit**: the detail page renders the portrait at the address built from the training and the
  photo — asserted as the address, so a reader can see no person's identifier leaves the page — and
  renders a name and no image when `TrainerPhotoId` is null.
- **`BothHosts_PublishTheSameOperations`** holds: the action is written twice, named identically.
- **One migration, `AddTrainerPhotoSanitisation`**, hand-written along with its designer and the
  snapshot, because `dotnet-ef` is not installed in this environment. `dotnet ef migrations add Probe`
  is named as a manual control and is not claimed to have been run.
- **`EveryMomentTheModelStores_IsStoredAtFullPrecision` caught the column** before this record was
  written: the configuration declared no `HasPrecision(7)` while the snapshot did, which is exactly
  the drift ADR 0005's rule exists to find.
