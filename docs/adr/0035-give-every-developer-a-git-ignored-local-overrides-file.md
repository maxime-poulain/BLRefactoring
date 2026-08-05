# 0035 — Give every developer a git-ignored local overrides file

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

This repository is public, and the configuration a developer actually needs — a local connection
string, a real SMTP key for a manual send, an API base address — has nowhere honest to live.
The channels on offer today all fall short:

- **`appsettings.Development.json` is committed.** A real credential pasted there for a quick
  test is one `git add` away from being harvested; the README already says so about SMTP keys.
- **User secrets are documented but not wired.** No project carries a `UserSecretsId` and nothing
  calls `AddUserSecrets`; the README's walkthrough works only because `dotnet user-secrets init`
  creates the id as a side effect. Even wired, the mechanism is per-project ceremony around an
  invisible file far from the repository, and it loads only in Development.
- **Environment variables** work everywhere but are awkward for structured keys —
  `Cors__AllowedOrigins__0` is nobody's idea of a configuration file.

One gap is sharper than the rest: `dotnet ef` boots the layered host with no environment set, so
it runs as Production and `appsettings.Development.json` never reaches design time. There is *no*
local channel for the connection string a developer's migrations run against.

## Decision

**Every host loads an optional `appsettings.Local.json` after every other source, and the file
never leaves the machine.**

- **One line per host, right after the builder.** Both API hosts and the Blazor BFF call
  `AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)` immediately after
  `CreateBuilder`, which appends the source at the end of the default chain — after both
  committed JSON files, after user secrets, after environment variables and arguments. The
  developer's file beats everything, deliberately: an override file that can itself be overridden
  is a puzzle, not a tool. The WebAssembly client is out of scope — it has no configuration of
  its own, by an earlier decision, so the BFF host is the only Blazor-side place an override can
  live. No csproj changes: the Web SDK's content glob already copies any project-root JSON.
- **It loads in every environment, on purpose.** Design time is precisely where it earns its
  keep — `dotnet ef` runs as Production and now finally has a local channel — and the
  environments where an override file would be dangerous are exactly the ones where the file
  cannot exist, because:
- **The local overrides file never leaves the machine: git refuses to version it and the Docker
  build context excludes it.** `.gitignore` carries the entry beside the repository's other local
  artifacts, and `.dockerignore` excludes it from the `COPY src/ src/` that builds the image — a
  developer's secrets cannot ride a build into a registry. Inside a container, configuration
  keeps arriving as environment variables, exactly as compose does today.
- **The integration suites are hermetic to it.** The test factories run the hosts from their
  source directories, where a developer's file would sit; both factories therefore remove the
  local source from the configuration before the host is built. Removed rather than out-shouted:
  the factories override the keys the suites own, and out-shouting every key a developer might
  write is a race the fixture cannot win. What the suites prove must not depend on whose machine
  runs them.
- **User secrets remain the documented alternative; the local file is the preferred mechanism.**
  Nothing is removed — the README keeps the user-secrets path — but the walkthroughs now reach
  for `appsettings.Local.json` first.

Two rules defend this record, one sentence each: every host loads appsettings.Local.json last,
so a developer overrides any committed source without editing one; and the local overrides file
never leaves the machine: git refuses to version it and the Docker build context excludes it.

## Consequences

- A developer overrides any key of any host — connection string, SMTP credentials, CORS origins,
  the BFF's API address — in one file that no tooling ever ships, and the same file serves
  `dotnet ef` at design time, which no local channel did before.
- The file wins over environment variables on the developer's machine. Accepted: locally that
  authority is the point, and everywhere else the file does not exist. Containers keep their
  environment-variable authority because the image cannot contain the file.
- A local file with invalid values fails `ValidateOnStart` — on that developer's machine only,
  which is the mechanism working as intended. The OpenAPI-generation build step boots the host
  and reads the file too; same locality, same verdict.
- One cosmetic caveat: the repository-wide former-name scan reads every file on disk and is not
  gitignore-aware, so a local file containing the old repository name in its casing would trip
  that rule locally. Self-inflicted, visible, and not worth machinery.

## Alternatives considered

**Wire user secrets properly and make them the mechanism.** The incumbent-on-paper. Rejected as
primary: per-project ceremony (`init`, `set`, one store per `UserSecretsId`), an invisible file
far from the repository, Development-only — which leaves the design-time gap open. Kept as the
documented alternative for developers who prefer secrets outside the working tree.

**`.env` files.** The convention of other ecosystems; not native to `IConfiguration`, would need
a package or hand-rolled parsing, and duplicates what a JSON source does first-class.

**A committed `appsettings.Local.json.example`.** An artifact that drifts from the real keys the
day someone adds an option. The README's configuration table already is the example, and it is
held to the code by review rather than by hope.

**Gate the file to Development.** Feels safer, does less: it would kill the design-time use — the
one gap nothing else fills — and the all-environments risk is already closed where it matters,
at the ignore files.

## Verification

`EveryHost_LoadsTheLocalOverridesFile` holds the three composition roots to the loading line;
`TheLocalOverridesFile_NeverLeavesTheMachine` holds `.gitignore` and `.dockerignore` to their
entries — the leak-prevention half, which is the half most worth defending. Both were written
before the change and failed on every host and both ignore files; the change turned them green.
The hermetic factories are exercised by every integration suite in CI, where no local file ever
exists, and locally by the fact that a throwaway `appsettings.Local.json` in a host directory
leaves every non-integration suite green and `git status` blind to it.
