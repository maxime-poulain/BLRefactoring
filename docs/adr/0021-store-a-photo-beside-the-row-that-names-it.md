# 0021 — Store a photo beside the row that names it, and never overwrite in place

- **Status:** Accepted
- **Date:** 2026-08-03

## Context

A trainer profile has carried a name, a contact address and a bio since the beginning. It has never
carried a portrait, and the stated destination for this codebase is a public catalogue of trainers —
a page whose entire job is to show people. The photo is not an attachment on a private record; it is
the most-requested byte range in the product-to-be.

That changes what the decision is about. Storing a few megabytes somewhere is not hard. What is hard
is that the bytes and the row naming them live in two systems that do not commit together, and that
the thing being built is a read-heavy public surface where caching is the difference between a CDN
and a bill.

Two smaller facts framed the choice.

**MinIO archived its community repository on 25 April 2026.** The README says `THIS REPOSITORY IS NO
LONGER MAINTAINED`, there are no published binaries from it, and the administration console had
already been removed from the community edition in May 2025. It is the reflexive answer to
"S3-compatible object storage for local development" and it is no longer an answer at all.

**`SixLabors.ImageSharp` v4 requires a licence key at build time**, and the sample key in its own
documentation expires on 4 September 2026. ADR 0019 and ADR 0020 were about removing the class of
failure where a build goes red without anybody pushing anything. Adding an image library that does
exactly that, three weeks after removing the last one, would be difficult to defend.

## Decision

### The bytes go in an object store, reached through an interface that knows only keys

`IObjectStore` lives in the kernel and has three operations: `PutAsync`, `GetAsync`, `DeleteAsync`.
It speaks of an `ObjectKey` and a `StoredObject` and nothing else — no bucket, no endpoint, no URL.
`ITrainerPhotoStore` sits on top of it beside `ITrainerRepository`, translating a trainer and a
photo into a key, and the key layout — `trainers/{trainerId}/{photoId}` — is the infrastructure's
alone.

The abstraction is generic rather than named `ITrainerPhotoStorage`, because the catalogue that
motivates this will want images of trainings too, and an interface named after one aggregate would
be either copied or renamed the day that happens.

### There is no `Replace`, and that is the load-bearing decision

Object storage is not transactional with SQL Server. One of the two has to be written first, and
that choice is the entire safety argument:

1. write the new bytes under a **fresh** key — a new `photoId`, minted by `TrainerPhoto.Create`;
2. commit the row that names them;
3. delete what was displaced.

A crash after step 1 leaves an object nothing references. A crash after step 2 leaves the old object
nothing references. **No ordering of failures leaves a committed row naming bytes that are gone.**
The reverse order — overwrite in place, or delete first — makes exactly that outcome reachable, and
it is the one outcome a user sees as a broken page.

A `Replace` operation would read as a single atomic step, would be implemented as an overwrite, and
would put that ordering out of reach of everyone who called it. So the interface does not offer one,
and `ObjectStorageRules.TheObjectStore_OffersNoWayToOverwriteInPlace` holds it to the three.

The same reasoning is why the aggregate raises no domain event here. An event would need a handler
(ADR 0002), handlers run inside the transaction the aggregate is being saved in, and deleting a blob
inside a transaction that may still roll back is the worst available moment.

### The server is SeaweedFS; the client is `AWSSDK.S3`; the protocol is what matters

SeaweedFS is Apache 2.0, actively maintained, and runs as one container. But the replaceability this
buys comes from the **client**, not the server: written against `AWSSDK.S3` with `ServiceURL` and
`ForcePathStyle`, the same adapter reaches Amazon S3, Cloudflare R2, Backblaze B2, Wasabi, Garage or
MinIO's commercial AIStor by changing four configuration values and no code.

`OnlyTheInfrastructure_KnowsTheObjectStore` holds that claim to something checkable: exactly one
project in the solution has ever heard of `AWSSDK`.

**No cloud target is chosen.** The repository stays runnable with `docker compose up` and no
account anywhere, which is what lets a reader try it. When a deployed catalogue exists, **Cloudflare
R2** is the intended target: S3-compatible, so the adapter above is already the production one; a
permanent free tier of 10 GB, 1M writes and 10M reads a month; and zero egress fees, which is the
dominant cost line for a public page full of images.

*Azure Blob Storage with Azurite* was the serious alternative and is rejected: the SDK is
first-party and the local emulator is maintained by Microsoft, but its API is not S3, so production
would run through a **second adapter that local development never exercises** — and its free tier
lasts twelve months and bills egress.

### What counts as a photo is the aggregate's rule, read off the bytes

`TrainerPhoto.Create` takes the uploaded bytes and the declared media type, and checks the second
against the first. PNG, JPEG and WebP are recognised by their signatures; anything else is refused,
SVG explicitly so, because it executes script and these images are going on a public page. A file
extension and a `Content-Type` header are both things the caller writes, so neither is evidence.

The refusals carry codes the aggregate owns — `Trainer.PhotoFormatNotSupported`,
`Trainer.PhotoContentMismatch`, `Trainer.PhotoTooLarge`, `Trainer.PhotoEmpty` — and not
`ErrorCodes.Validation`, which ADR 0016 reserves for the pipeline and which means "rejected before
the domain saw it".

**Decoding the image is deliberately deferred.** Decoding would bound the dimensions and, more
importantly, strip EXIF: a photo taken on a phone carries GPS coordinates, and a public catalogue
would publish them. The library that would do it is not ImageSharp, for the licence-expiry reason
above; it is **SkiaSharp**, under MIT. This is recorded rather than left to be discovered, and it is
the one decision here that gets more expensive with time — retrofitting EXIF stripping means
reprocessing photos already stored.

### Three endpoints, and the read is shaped for the catalogue

`GET /Trainer/{id:guid}/photo` serves the bytes with a strong ETag cut from the photo's identity and
`Cache-Control: public, max-age=31536000, immutable`. That cache is honest precisely because a
replacement mints a new identity: the bytes under any one tag genuinely never change. A CDN can sit
in front of that route later without a line moving, and the endpoint becomes public by adding
`[AllowAnonymous]` and nothing else.

`PUT` and `DELETE` are on `/Trainer/me/photo`, self-service like the rest of the profile. `PUT`
covers publishing and replacing — there is no third thing to do to a photo, and PUT's idempotence
matters for a five-megabyte body on a connection that may drop: a retry after a timeout costs an
orphaned object, never a wrong answer.

No `If-Match` is required. Nothing is being edited against a version the caller read; the request
means "this is my photo now", and last-write-wins is the intended semantics. A lost race on commit
answers **409**, not 412, because no precondition was asked for and so none can have failed.

## Consequences

**Orphaned objects are now a thing that exists.** Every crash point in the sequence above leaves
bytes nothing references, and so does every failed cleanup delete. This is the cost that was chosen
in exchange for never having a broken reference, and it is the right way round — rubbish is
collectable on a schedule, a profile pointing at nothing is a user-visible fault. No collector is
written yet; at the volume of one portrait per trainer, the storage is not worth a cron job until
somebody can measure it.

**The size limit is enforced twice, and only one of the two can name itself.**
`[RequestSizeLimit]` stops an oversized body before it is buffered, which is the property worth
having; the aggregate's own check catches anything that gets past it and answers with
`Trainer.PhotoTooLarge` in the problem document.

What this record first claimed about the transport half was wrong, and it is corrected here rather
than quietly. A handler was written to turn the framework's abort into `413` in this API's problem
shape. The integration suite established that nothing ever calls it: a body-read failure inside
model binding does not reach an exception handler, because MVC folds it into model state and
answers `400` with an unbound file. The handler is deleted and the `413` both hosts advertised is
withdrawn with it — an action that declares a status it cannot produce is worse than one that
declares fewer.

**The store has to be told who may write to it, and the failure if it is not is silent.** Started
without `-s3.config`, SeaweedFS is not permissive: it accepts anonymous requests and refuses signed
ones outright, and every AWS SDK signs everything. The container reports itself healthy, a bucket
can still be created because that is metadata, and every upload answers 500 with the reason in the
server's log rather than the client's hand. Both the compose stack and the integration fixture
declare an identity — which is also closer to a hosted bucket than the alternative, since requests
are signed and the signature is verified.

**The SDK frames uploads in a way a compatible store need not understand.** Version 4 of
`AWSSDK.S3` computes a CRC32 for every upload by default and sends it with `Content-Encoding:
aws-chunked`. Amazon's own S3 reads that framing; SeaweedFS stored the markers as though they were
part of the image. Both checksum settings are therefore set to `WHEN_REQUIRED`. This is worth
recording because it is the single most common way the SDK fails against a non-AWS endpoint, and
therefore the most likely thing to be re-encountered by whoever points this adapter at a different
provider — the very move this record claims is free.

**The integration suite now needs two containers.** A real S3 server rather than a fake, because
what is worth proving about this — bytes come back byte for byte, a missing key answers rather than
throws, a replaced photo really stops being there — is exactly what a fake would assume. They start
in parallel, so the suite pays the slower of the two rather than the sum.

**`Trainer.AttachPhoto` and `RemovePhoto` return nothing.** They were drafted returning the
displaced photo, which is what the caller needs in order to delete it, and
`NoAggregate_ReturnsData` rejected that on the spot: an aggregate answers whether a change was
allowed and is not a way of reading through to state. Callers read `Photo` before the call instead.
The rule was right and the first draft was wrong.
