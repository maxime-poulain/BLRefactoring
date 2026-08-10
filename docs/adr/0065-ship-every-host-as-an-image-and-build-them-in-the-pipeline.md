# 0065 — Ship every host as an image, and build them in the pipeline

- **Status:** Accepted — amended by [0067](0067-cache-the-image-layers-without-taking-a-dependency.md): the layer cache it turned down is taken, the objection having been the dependency rather than the caching; amended by [0068](0068-remove-the-image-layer-cache.md): the cache is removed on measurement, and building without one stands again
- **Amends:** [0038](0038-derive-every-counted-claim-from-the-code.md)
- **Date:** 2026-08-10

## Context

This repository runs three processes. Two APIs publish the same operations from two application
styles — a rule requires it — and a backend for frontend holds the session and serves the
WebAssembly client. One of the three had an image.

`docker compose up` therefore started SQL Server, the object store, the mail server and the layered
API. Somebody who had just cloned this repository could see half the argument running and had to
find out, from a section of the README rather than from the failure, that the other host and the
whole front end were started some other way. A repository whose point is that two stacks serve one
domain shipped one of them.

Nothing was watching either half of that. ADR 0038 said so, in as many words, when it made counted
prose executable:

> `generator.nswag` joins the configuration files the suite reads. `docker-compose.yaml`, the
> Dockerfile and `.config/dotnet-tools.json` are still read by nothing; that is recorded here as
> known, not as decided.

And the README carried the consequence, as a warning rather than as a check:

> nothing in CI builds that image, so treat a `docker build` as the check rather than the guarantee.
> It went unbuildable once already: the restore stage stopped copying two files it needs, and the
> README said this sentence throughout.

That is a measured failure, not a hypothetical one. The image was broken, the pipeline was green,
and the sentence admitting it had been true for so long that it had stopped being read.

## Decision

**Every host this repository runs ships as an image, `docker compose up` starts the stack whole, and
the pipeline builds all three.**

- **A host is a project declaring the web SDK**, and the rules derive the list from that rather than
  restating it. `Microsoft.NET.Sdk.Web` says a project is a process somebody starts, which is what
  makes an image the right unit for it. The WebAssembly client has a `Program.cs` like the others and
  declares `Microsoft.NET.Sdk.BlazorWebAssembly` instead: a browser downloads it from the BFF, so it
  ships *inside* that host's image rather than beside it.
- **The BFF serves TLS inside its container, and it had no choice.** Its session cookie is
  `__Host-bff` with `Secure` always on, so a browser refuses to store it over plain HTTP. Served
  without TLS, that container renders every page and signs nobody in — silently, which is the same
  failure the README already warns about for `dotnet run`. The certificate is the developer's own,
  exported with `dotnet dev-certs https -ep` and mounted read-only; the machine's browser trusts that
  root already, so nothing has to be accepted by hand.
- **Only the hop a browser makes is encrypted.** The two APIs speak plain HTTP and the BFF reaches
  them that way, inside the compose network, with no cookie and no browser involved. Encrypting that
  hop would mean a certificate per service and a trust store in three images, to protect traffic that
  never leaves a bridge network.
- **The CQRS host starts after the layered one, and the three infrastructure containers are not the
  reason.** In `Development` both hosts apply their migrations at startup (ADR 0003) against one
  database. Starting them together is precisely the race on DDL that record names as the reason not
  to migrate from the process serving requests. Ordered, the migrations run once and the second host
  finds nothing pending.
- **The pipeline builds the three images and pushes none of them.** What goes wrong in a Dockerfile
  of this shape — a restore stage that no longer copies what it needs — fails at build time or never,
  so building is the whole of the check. No registry, no tag, no deployment target: ADR 0021 declined
  to choose one and this record does not reopen that.
- **Three rules read what nothing read before.** `EveryHost_ShipsAsAnImage` holds the compose file
  and the Dockerfiles together; `EveryImage_IsBuiltByThePipeline` holds the workflow to them; and
  `NoSourceFolder_IsHiddenFromTheBuildContext` reads `.dockerignore` the way Docker does, so a folder
  of source cannot go missing from an image. That is the half of ADR 0038 this record amends: those
  files are read now.

## Consequences

- **`docker compose up -d` now starts six containers rather than four**, and two of them are the
  application. The command's cost goes up — three .NET images to build on a cold cache rather than
  one — and what it produces is the whole application instead of a part of it.
- **A prerequisite appears that a clone cannot satisfy by itself.** The BFF container will not start
  without a certificate at `docker/https/traininghub.pfx`, and that file is one command away and
  never versioned. The failure is loud rather than silent — Kestrel refuses to bind — which is the
  right way round for a missing secret, and the README names the command.
- **The certificate joins the family `appsettings.Local.json` belongs to.** A PKCS#12 file carries a
  private key, so `.gitignore` and `.dockerignore` both exclude it, exactly as ADR 0035 has them
  exclude the local overrides. It is mounted, never copied: an image with a developer's key baked in
  would be the thing this line exists to prevent.
- **A development certificate is not a production model, and nothing here pretends otherwise.** It is
  trusted because one machine trusts it. A deployed system terminates TLS somewhere else entirely,
  and choosing where is a decision this repository has not taken.
- **The build job grew by three image builds, and its budget moved from twenty minutes to thirty.**
  No layer cache and no third-party action: every `uses:` here is first-party, and a cross-run Docker
  cache costs a builder action plus a cache backend to save a few minutes on a job with room. That is
  a trade this record makes rather than a detail it omits, and it is the first thing to revisit if
  the job starts running long.
- **The images are steps of the existing job rather than a job of their own**, and ADR 0047 is why. A
  separate job would need a job-level `if` to honor the delegation that stops a commit being built
  twice — and a job skipped that way still posts a check GitHub reads as passing, which is the exact
  green failure that record was written about. `NoDelegatedBuild_IsTakenOnTrust` would have refused
  it.
- **The two API Dockerfiles are twins, and deliberately so.** They differ in the projects they copy
  and in nothing else, comments included. A jumper that keeps the shape and drops the reasons — why
  the two `Directory.*.props` come first, why `curl` is installed, why `logs/` is created before the
  user drops — is the one that breaks next, so the reasons are repeated rather than summarized.
- **The pipeline step found a real defect on its first run, and it was the one this record predicts.**
  `.dockerignore` excludes `**/Release`, correct for `bin/Release` and `obj/Release` and wrong for a
  use case — and one folder per use case (ADR 0052) means the CQRS stack has
  `Features/Trainings/Release/`. Two source files were silently absent from the build context, so the
  image failed to compile on a type that did not exist, from a Dockerfile that reads correctly.
  `.gitignore` already carried the narrow re-inclusion that fixes it, learned the same way when the
  same collision dropped four source files from a commit; `.dockerignore` never learned it, because
  nothing had ever built the image that would notice. The exception is now written in both files and
  held by a rule.
- **`docker compose up` is still not run by anything automatic.** The pipeline proves the three
  images build; that six containers reach `healthy` together, and that a browser can sign in through
  the BFF, is checked by a person running the stack. Named here rather than left to be assumed.

## Alternatives considered

**Containerize the two APIs and leave the BFF out.** The smallest change that ends the parity
complaint, and it avoids the certificate ceremony entirely. It also leaves the one container a human
being actually looks at outside the stack, so `docker compose up` still does not produce a running
application — which is the thing that was wrong. The absence would have needed its own record either
way, and a record explaining why the front end is missing is worse than a mounted certificate.

**Serve the BFF over plain HTTP and accept that sign-in does not work.** No certificate, no
prerequisite, and the catalog pages would render — the anonymous ones, at least. It fails on honesty:
a container that shows pages and silently refuses to authenticate is a demonstration that lies, and
somebody would spend an afternoon on it before finding the cookie attribute that explains everything.

**Put a TLS terminator — Caddy, nginx — in front of the three.** What a deployed system does, and it
would move the certificate out of the application's configuration. It adds a service, its
configuration file and its own certificate story to a repository that has deliberately chosen no
deployment target (ADR 0021), to solve a problem one mounted file solves. The day a target is chosen,
this is what replaces the mount.

**Give the CQRS host its own database instead of ordering the two.** It ends the migration race
without `depends_on`, and both hosts would start in parallel. It also ends the demonstration: the two
stacks exist to serve *one* domain, and a reader who registers a trainer through one host and cannot
find them through the other has been shown two applications rather than two ways of writing one.

**Cache the Docker layers with `docker/build-push-action` and the GitHub Actions cache.** It is the
standard answer and it would save most of the added minutes. It also introduces the first
third-party actions into a workflow set that has only ever used `actions/*`, for a saving the job's
budget does not need yet. Recorded as the first thing to change if that stops being true.

## Verification

- **The first two rules watched failing before the Dockerfiles existed**, each naming exactly what
  was missing, and each half failing on its own: the layered host passed the Dockerfile half and
  failed the pipeline half in the same run.
- **The third was written from a failure rather than against a hypothesis.** The pipeline reported
  the missing use-case folder as two compiler errors inside a Docker build; the rule was then written
  to reproduce that in the test suite, watched failing on the same folder, and made green by the
  `.dockerignore` exception. That order matters: the rule is proven by a defect that happened, not by
  a fixture written to break it.
- **The derivation was checked against the tree rather than assumed**: four `Program.cs` files exist
  under `src/`, and the web SDK selects the three that are hosts.
- **The BFF image's one uncertain claim was run rather than believed.** That a single
  `dotnet publish` produces both applications, with no `wasm-tools` workload, is the sentence in that
  Dockerfile a reader would most reasonably doubt — so the publish was run outside Docker, on the
  same command the image issues. It produced the host assembly and a hundred and ninety-eight
  WebAssembly framework files.
- **Clean Release build from deleted `bin/` and `obj/`, zero warnings**, and every suite that runs
  without Docker is green.
- **The three `docker build` commands and `docker compose up` did not run here** — this environment
  has no Docker. They are the manual control this record is about, named rather than claimed, and the
  pipeline step is what turns them into a check that runs on every commit.
- **The two integration suites need Docker and did not run here** either.
