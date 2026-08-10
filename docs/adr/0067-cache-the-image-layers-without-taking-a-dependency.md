# 0067 — Cache the image layers without taking a dependency

- **Status:** Accepted
- **Amends:** [0065](0065-ship-every-host-as-an-image-and-build-them-in-the-pipeline.md)
- **Date:** 2026-08-10

## Context

ADR 0065 made the pipeline build three images on every commit, and turned down caching their layers
in the same breath:

> **Cache the Docker layers with `docker/build-push-action` and the GitHub Actions cache.** It is the
> standard answer and it would save most of the added minutes. It also introduces the first
> third-party actions into a workflow set that has only ever used `actions/*`, for a saving the
> job's budget does not need yet. Recorded as the first thing to change if that stops being true.

**That trigger has not fired, and this record says so before doing anything else.** Measured on the
runs that followed, rather than estimated:

| | |
|---|---|
| The `Images` step, three images, cold | **2 min 07 s** |
| The whole `Build & Test` job | **4 min 06 s** |
| The job's budget | 30 min |

Twenty-six minutes of headroom. On the argument ADR 0065 actually made, nothing has changed and
nothing needs to.

What changed is the other half of that sentence — the *price*. It named two third-party actions as
the cost of caching, and there was a way to pay neither. `buildx` ships with the Docker CLI the
runner already has, so a builder is a line of shell rather than an action; `type=local` writes its
cache to a directory, and carrying a directory between runs is what `actions/cache` does — the
first-party action this job already uses for NuGet packages. The standard answer costs two
dependencies. This one costs none.

So the decision is not *the budget forced it*. It is that an option ADR 0065 did not consider makes
the trade a different trade.

## Decision

**The image build imports and exports a layer cache, and every `uses:` in this repository stays
first-party.**

- **A container-driver builder, created in a `run` step.** The default `docker` driver exports no
  cache at all, so `--cache-to` against it is a flag that is accepted and does nothing — a caching
  step that caches nothing, and reports success.
- **The cache is keyed on what decides the restore stage** — the Dockerfiles, the project files and
  the two `Directory.*.props` — and not on the source, which changes every commit and would make
  every key a miss. A prefix fallback keeps a near miss useful rather than starting from nothing.
- **One directory per image.** Three images sharing one would overwrite each other's cache, and the
  step would go quietly back to building everything from scratch: green, and no faster than before.
- **The cache is exported beside the one it was imported from, then swapped in.** `type=local`
  appends rather than replaces, so a directory that is both source and destination grows every run
  until restoring it costs more than the build it exists to save.
- **`TheImageBuild_KeepsItsLayerCacheBetweenRuns` holds all four**, and the last is the reason it is
  a rule rather than a comment: every one of these failures is green.

## Consequences

- **The saving is not measured here, and this record will not claim one.** This environment has no
  Docker, so what is written above is a change whose effect the pipeline reports rather than a number
  somebody verified. The baseline is recorded — 2 min 07 s cold, 4 min 06 s for the job — so the run
  that introduces the cache is also the run that measures it, and the next reader can compare rather
  than take a sentence on trust.
- **The three images already shared layers within a run**, through the builder's own cache, which is
  why the cold figure is 2 minutes and not 6. What crosses runs now is the base image pulls and the
  restore stage; what will never cross is the publish stage, because the source changed — that is
  what a commit is.
- **A cache is a thing that can be stale, and this one is keyed to admit it.** A change to any
  Dockerfile, any project file or either `Directory.*.props` mints a new key. A change to source
  alone reuses the old one, which is exactly the case the cache exists for.
- **`actions/cache` now carries two caches for this job**, NuGet packages and image layers. They
  share an eviction budget with every other cache in the repository, and GitHub evicts least
  recently used at ten gigabytes. Named because a cache that is silently evicted looks precisely
  like a cache that is silently useless.
- **If this ever needs to be undone**, the way back is the workflow: delete two steps' worth of
  flags and the rule goes red, which is the point of the rule. The images build the same either way.
- **The first-party constraint is now load-bearing, and worth stating as such.** It was an
  observation in ADR 0065 — *every `uses:` here is `actions/*`* — and choosing a shell builder over
  the standard action to keep it makes it a decision. A future step that wants a third-party action
  is not forbidden by this record; it is asked to say why the first-party route does not work.

## Alternatives considered

**Leave it alone, since the measurement says the budget is fine.** The honest reading of ADR 0065,
and it was the recommendation until the dependency-free route turned up. Two minutes on a job with
twenty-six to spare is not a problem, and the strongest argument for this change is not speed — it is
that the reason for *not* doing it evaporated. Had the route still cost two third-party actions, this
record would say *no*.

**`docker/setup-buildx-action` plus `docker/build-push-action` with `type=gha`.** The standard
answer, better documented than this one, and the least likely to break when buildx changes. It costs
what ADR 0065 said it costs: the first third-party actions in a workflow set that has none, and a
trust surface to watch on a repository whose pipeline commits to its own branch. The saving is
similar; the price is not.

**Key the cache on the commit, with a prefix fallback.** Every run would export a fresh entry and
restore the previous one, which is the busiest and most correct form. It also mints a cache entry per
commit against a ten-gigabyte eviction budget, so the NuGet cache would be evicted by image layers.
Keying on the files that decide the restore stage produces far fewer entries and misses only where a
miss was inevitable.

**Cache the base images instead, with `docker save` and `actions/cache`.** Cheaper to reason about —
two tarballs, no builder, no driver question — and it would cover the pulls, which are a real part of
the two minutes. It covers nothing else: the restore stage, which is the part this repository's own
history broke, would run every time.

## Verification

- **The rule watched failing on each of its four conditions in turn**, restored between each: the
  export written into the directory it imported from, the import removed, the export removed, and
  `actions/cache` pointed at another directory. Four separate messages, none of which is a rewording
  of another.
- **The baseline was measured on real runs before the change**, not estimated: `Images` 2 min 07 s on
  the master run for ADR 0065, the whole job 4 min 06 s.
- **Clean Release build from deleted `bin/` and `obj/`, zero warnings**, and every suite that runs
  without Docker is green.
- **No `docker build` ran here.** This environment has no Docker, so the workflow change is checked
  by the pipeline and by nothing else — which is the same standing ADR 0065's images have, and is
  named rather than glossed.
- **The two integration suites need Docker and did not run here.**
