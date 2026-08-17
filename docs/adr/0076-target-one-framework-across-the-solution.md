# 0076 — Target one framework across the solution

- **Status:** Accepted
- **Amends:** [0037](0037-answer-for-the-hosts-health-at-two-endpoints.md),
  [0019](0019-enforce-the-ruleset-this-repository-already-declared.md)
- **Date:** 2026-08-11

## Context

Twenty-four projects targeted `net10.0`; three targeted `net9.0` — the Blazor host, the WebAssembly
client it serves, and the generated client they both reach for. No record decided that. It was
inherited, asserted in five places and argued in none, and it billed the repository in four
currencies:

- **A package version overridden for one project.** The Components family was pinned centrally to
  `9.0.18` because the pin followed the island; bUnit's `net10.0` assets ask for `10.0.10`, and
  with central transitive pinning on, a mismatch is an error rather than a resolution. So
  `TrainingHub.Blazor.Client.Tests` carried the solution's only `VersionOverride`, and the README
  carried the sentence stating the exception.
- **A rule that could not see the whole solution.** Referencing that suite from the architecture
  tests would have dragged the override into a project reaching the net9.0 graph, so it was left
  out — and `NoClass_IsInheritedWithoutBeingSealed` ran on a view missing two of its own classes.
- **A host that built and then refused to start.** The README asks for one prerequisite, **.NET SDK
  10**. On a machine with exactly that, `dotnet run` on the BFF fails: the runtimeconfig demands
  `Microsoft.NETCore.App 9.0.0`, no `RollForward` is configured anywhere, and roll-forward does not
  cross a major version. So the sentence *"The Blazor front end runs with `dotnet run`"* was false
  for anybody who installed what the README asked for, and the local workflow of
  [ADR 0075](0075-give-the-bare-compose-up-to-the-developer.md) — start the dependencies, run the
  hosts from the IDE — held for the two API hosts and not for the third.
- **A reason attached to the wrong cause.** The BFF's inline health calls, its missing Serilog and
  its runtime image all cited the target framework. Two of those three have a reason of their own.

Nothing in the graph blocked the move. MudBlazor 9.7.0 and bUnit 2.8.6 both ship `net10.0` assets;
the Components 10.0.x packages exist; the Dockerfile's *build* stage was already `sdk:10.0`. The
bUnit suite had in fact been running the 10.x Components against the net9.0 component library for
as long as the override existed, which is the compatibility argument already made and paid for.

## Decision

**Every project of this solution targets `net10.0`. There is no second framework, and the things
the second framework was blamed for are given their real reasons.**

- The three net9.0 projects move together — they had to, since a non-test project could not
  reference one across the divide.
- The Components pins rejoin the rest of the Microsoft family at `10.0.10`, and the solution's
  only `VersionOverride` disappears with the mismatch that required it.
- The architecture suite references the bUnit suite and `Solution.All` is whole: the sealing rules
  see every class this repository writes.
- The BFF's runtime image becomes `aspnet:10.0`, matching the SDK stage that always built it.
- **The BFF keeps its inline liveness pair, for its own reason.** It owns no database, no object
  store and no mail server, so it has nothing to report readiness on; its world is the API, and
  proxying that answer would be a decision of its own. ADR 0037 said the same thing and put the
  framework in front of it. The framework is gone; the reason stands, and it is what the rule now
  states. The BFF gains no reference to `Shared.Api`, so the project graph — and the README diagram
  that mirrors it — is unchanged.
- `AnalysisLevel` is no longer pinned. ADR 0019 pinned it to `10.0` so the three net9.0 projects
  would not analyze at a different level from the rest; with one framework the property is a no-op
  whose comment describes a solution that no longer exists.

## Consequences

- `dotnet run` starts every host of this repository on a machine carrying the one prerequisite the
  README asks for. The local workflow of ADR 0075 is true for all three.
- One framework version to move at the next release rather than two, and one place where a package
  version means what it says.
- The three net9.0 assertions in prose — `Directory.Build.props`, the README's project census and
  its dependency-graph paragraph — become one sentence, and the README's *Central package
  management* bullet loses the exception it had to state.
- The pipeline is untouched: it installs `10.0.x` alone and always did, which is why the two Blazor
  test projects already targeted net10.0 and explained themselves in comments that are now deleted.

## Verification

- `EveryProject_TargetsTheSameFramework` (ProjectGraphRules) replaces
  `OnlyATestProject_SpansTheTwoTargetFrameworks`, whose statement — "the backend is net10.0 and the
  browser pair is net9.0" — this record ends. Proved red before the move, naming all three
  projects, and green after.
- `NoClass_IsInheritedWithoutBeingSealed` runs over two assemblies it could not see before, and
  passes.
- `TheBff_AnswersForItsLiveness` (HealthRules) keeps its assertion unchanged and states its real
  reason: what was pinned is still pinned, for a cause that is now true.
- Purged Release build, zero warnings, with the Blazor projects compiled against net10.0 for the
  first time; every non-Docker suite green.
- The workflow ADR 0075 describes, run end to end on a machine holding only the .NET 10 SDK: the
  BFF starts under `dotnet run` where it previously failed on a missing framework, and answers
  `/health/live`.
