# 0068 — Remove the image layer cache

- **Status:** Accepted
- **Supersedes:** [0067](0067-cache-the-image-layers-without-taking-a-dependency.md)
- **Amends:** [0065](0065-ship-every-host-as-an-image-and-build-them-in-the-pipeline.md)
- **Date:** 2026-08-10

## Context

ADR 0067 added a layer cache to the image build — a container-driver builder, `type=local`
import and export, one directory per image, carried between runs by `actions/cache` — because a
dependency-free route had appeared for a saving the standard answer priced at two third-party
actions. It was honest about one thing above all: *"The saving is not measured here, and this
record will not claim one. … the run that introduces the cache is also the run that measures it,
and the next reader can compare rather than take a sentence on trust."*

The runs happened. Measured on the same pipeline and the same runner class, not estimated:

| Run | `Images` step | Whole job |
|---|---|---|
| No cache at all — ADR 0065's baseline | 2 min 07 s | 4 min 06 s |
| Cold — cache written, nothing to restore | 4 min 35 s | 6 min 53 s |
| Warm — a full cache restored, then re-exported | 4 min 45 s, plus 55 s restoring and 28 s saving | 8 min 09 s |

The warm run is the one the bet was about, and it refused it. The restore was real — fifty-five
seconds of downloading and unpacking, where a miss takes one — and `Images` still gained nothing
over the cold run, at more than twice the uncached baseline. The job doubled: 4 min 06 s to
8 min 09 s, every commit.

The why is structural rather than accidental, and ADR 0067 predicted half of it: *"what will never
cross is the publish stage, because the source changed — that is what a commit is."* The publish
stages rebuild on every commit, so the reusable work is the base image pulls and the restore
stage — and what those save is smaller than what `mode=max` costs every run to export, download
and re-save. The overhead is per-run and unconditional; the saving is bounded by the small share
of the build that survives a commit. Neither side of that inequality moves until the Dockerfiles
change shape.

## Decision

**The image build carries no layer cache.** The `Cache the image layers` step, the
container-driver builder and the `--cache-from`/`--cache-to` flags leave the workflow; the
`Images` step goes back to three plain builds — which is ADR 0065's decision, untouched by this
record.

`TheImageBuild_KeepsItsLayerCacheBetweenRuns` retires with the record it defended.
**`TheImageBuild_CarriesNoLayerCache` replaces it**: the workflow names no cache flag on the image
build and carries no image-layer cache directory, so the mechanism cannot return quietly —
bringing it back means superseding this record, carrying a warm measurement that says the trade
changed.

## Consequences

- **The job returns to its baseline** — about four minutes, twenty-six of headroom — the state
  ADR 0065 measured and was content with.
- **ADR 0065's decision stands again in its original form**, which is why this record amends it:
  its status had been annotated by 0067 as *the layer cache it turned down is taken*, and that
  annotation is now history rather than the present. The three images are still built on every
  commit; only the caching between runs is gone.
- **`actions/cache` carries one cache for this job again** — NuGet packages — and the ten-gigabyte
  eviction budget stops being shared with image layers big enough to evict it.
- **What was tried stays on the record, not erased.** ADR 0067 remains the honest account of a
  mechanism built carefully — measured cold before it began, keyed on the files that decide the
  restore stage, exported beside its import — that its own measurement then refused. The sequence
  *decide, measure, undo on the number* is cheaper to read than to relive, which is why the record
  is superseded rather than deleted.
- **A future cache is not forbidden; it is priced.** If the Dockerfiles ever gain an expensive
  stage that survives commits — a heavy native build, a tool restore measured in minutes — the
  arithmetic changes, and a successor record can say so. What it must bring is a warm measurement,
  because this attempt had everything else and fell exactly there.

## Alternatives considered

**`mode=min` instead of removal.** It exports only the layers of the final image — the small
publish output — and omits the build stage, which is precisely the part worth reusing. It shrinks
the export overhead by shrinking the saving with it, and the saving is what was already too small.

**A registry-backed cache (`type=registry`).** It moves the transfer cost to a registry rather
than removing it, and ADR 0065 pushes to no registry — standing one up to carry a cache would be
the tail wagging the dog.

**Keep it and wait.** The overhead is per-run and the saving is bounded by what survives a commit;
neither moves until the Dockerfiles change shape. Waiting costs four minutes on every commit for
as long as nobody re-measures, and the trigger that would justify re-measuring is already written
one bullet up.

## Verification

- **`TheImageBuild_CarriesNoLayerCache` was written before the removal and watched failing** on
  the workflow that still carried the cache, then went green when the cache left — the same
  red-first ritual every rule here passes.
- **The three figures above are read from real runs, not estimated**: the ADR 0065 baseline run,
  the run that wrote the cache cold, and the first run to restore it warm.
- **The run for this change confirms the return to baseline**: `Images` back near 2 min, within
  the noise a shared runner allows.
- **No Docker ran here** — the workflow change is checked by the pipeline, the same standing every
  image change in this repository has had since ADR 0065.
