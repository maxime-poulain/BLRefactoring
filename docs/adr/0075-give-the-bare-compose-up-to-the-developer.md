# 0075 — Give the bare compose up to the developer

- **Status:** Accepted
- **Amends:** [0065](0065-ship-every-host-as-an-image-and-build-them-in-the-pipeline.md)
- **Date:** 2026-08-11

## Context

ADR 0065 gave every host an image and made `docker compose up` start the stack whole: six
containers, three of them built from this repository's own Dockerfiles. That sentence serves the
reader who wants to see the system run, and it is the wrong daily command for the person who works
on it. A developer iterating on a host runs it from an IDE or `dotnet run` — that is where the
debugger, the hot reload and the fast feedback are — and what they need from Docker is only what
the host cannot be: SQL Server, the object store and the mail server. For them, the bare command
paid for three image builds to obtain three containers they were about to ignore.

The README already told the two workflows apart, but by a list: `docker compose up -d sqlserver
seaweedfs mailpit`. A list in prose is a copy of the compose file's own knowledge, and a service
added to one is invisible to the other. The configuration side needed nothing at all: every host's
`appsettings.Development.json` already points at `localhost` and the published ports, so a host
started on the machine has always been able to reach the dependencies in the containers.

## Decision

**The three host services move behind a compose profile named `full`. The bare `docker compose up`
becomes the developer's command: it starts the three dependencies alone, builds nothing, and
`--wait` makes it answer only when they are healthy. The stack whole remains one flag away.**

- `docker compose up -d --wait` — SQL Server, SeaweedFS and Mailpit, nothing built, nothing else
  started. `scripts/start-dependencies.sh` says exactly this and prints where each dependency
  listens; it names no service, so it cannot drift from the compose file.
- `docker compose --profile full up -d --build` — the six containers of ADR 0065, unchanged:
  the same healthchecks, the same startup order, the same TLS certificate requirement for the
  BFF. A service without a profile always starts, so activating the profile starts the
  dependencies too.
- Profiles rather than a second compose file or an override, because the alternative is a copy:
  a `docker-compose.dependencies.yml` restates three services that already exist, and the two
  files answer differently the day one is edited. One file, one dictionary, two questions.
- The pipeline is untouched: CI builds the three images with `docker build` directly (ADR 0065,
  ADR 0068) and never runs compose, so the profile changes nothing it does.

## Consequences

- The clone-to-working loop shrinks to two commands: `./scripts/start-dependencies.sh`, then
  `dotnet run` on whichever host is being worked on. No image build, no TLS certificate — the
  certificate is the BFF container's need, and the BFF now runs in a container only under the
  full profile.
- The sentence ADR 0065 taught — `docker compose up` starts the stack whole — moves behind
  `--profile full`. The README and CLAUDE.md teach the new pair; the record itself is amended,
  not rewritten.
- A host service that loses its profile line rejoins the default startup silently, which is why
  the rule below exists.

## Verification

- `EveryHost_StaysBehindTheFullProfile` (HostingRules) — every service built from a host's
  Dockerfile carries `profiles: ["full"]`, so the bare command cannot grow an application
  container back; proved by mutation before it was trusted.
- `EveryHost_ShipsAsAnImage` (HostingRules, ADR 0065) — unchanged and still green: the profile
  moves the services, it does not remove them.
- The workflow itself, run: the bare command starts exactly three containers and builds nothing,
  a host started with `dotnet run` answers `/health/ready` with every dependency probe green,
  and the full profile still brings up all six containers.
