# 0019 — Enforce the ruleset this repository already declared

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

[ADR 0017](0017-measure-what-the-rules-cannot-with-sonarqube-cloud.md) named a gap in itself and
declined to close it:

> There is a fourth gap, and it is the one this record deliberately does *not* close. `.editorconfig`
> sets seventy-three analyzer rules to `severity = warning`. No project sets `TreatWarningsAsErrors`
> and neither workflow passes `-warnaserror`, so every one of those warnings is emitted and ignored.
> **The repository already owns a configured ruleset it does not enforce.**

and, among its alternatives:

> **Recommended as the next one.** […] it is left out of this record only because **nobody knows how
> many warnings the build currently emits** — turning it on blind would either change nothing or
> paint the pipeline red, and finding out which is a piece of work with its own record.

The README made the matter worse by asserting the outcome: *"The build is kept free of warnings.
Analyzer severities are set high on purpose, so a warning means something to look at rather than
noise to scroll past."* Nothing verified that sentence, and nothing made it true.

This repository's argument is that a recorded decision fails the build. Ninety-three architecture
rules keep that promise. The analyzer configuration did not, and it is the file every reader opens.

### What the census found

Counting had to happen in CI, since turning the properties on blind was the thing ADR 0017 refused
to do. A temporary workflow rebuilt the solution four times with `--no-incremental` and counted
warnings per rule. It is deleted; its numbers are the reason this record exists.

| Configuration | Distinct warnings |
|---|---|
| As it stood | **0** |
| `+ EnforceCodeStyleInBuild` | **10** — `IDE0051`×5, `IDE0011`×4, `IDE2000`×1 |
| `+ GenerateDocumentationFile` | **959** — `CS1591`×911, `IDE0005`×28, `CS1573`×8, the ten above, `CS1574`×1, `CS1570`×1 |
| `+ AnalysisLevel=10.0` | **0** |

Four facts came out of it, none of which was known beforehand:

**The seventy-three rules were two groups in opposite states.** The ~49 `CA*` rules had been running
on every build all along and emitting nothing — the build was already clean. The ~24 `IDE*` rules
were not running at all, because `EnforceCodeStyleInBuild` was unset, so they existed in an editor
and nowhere else. ADR 0017 treated them as one population.

**Turning this on was never going to paint the pipeline red.** The fear that deferred the work was
unfounded, and only measuring could say so.

**`IDE0005` still cannot report at build time without documentation generation** — zero occurrences
without it, twenty-eight with. That constraint is old, has been repeatedly reported as a bug, and
survives on the .NET 10 SDK.

**`CA2016` was not in force.** It was declared twice in the same section, seventy-eight lines apart,
`warning` then `suggestion`. The later key wins, so *"Forward the `CancellationToken` parameter"* —
the subject of an entire batch of work in this repository — had been quietly demoted by a
copy-paste. The effective count was 72 warnings, not 73; ADR 0017 counted lines.

## Decision

**`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`, in `Directory.Build.props`.** Every severity
in `.editorconfig` becomes a rule. In the props file rather than in the workflows, so a warning
fails the build an editor runs too — a contributor learns at once instead of twenty minutes later,
and the truth lives in one file rather than three YAML ones.

**The properties, never `dotnet build -warnaserror`.** The CLI switch also promotes what the MSBuild
engine emits — assembly-version conflicts, restore-graph noise — none of which is code quality, and
any of which would fail a build for a reason nobody chose. The property leaves `MSB*` alone.

**Every rule is enforced or demoted with its reason. There is no third category.** A
`WarningsNotAsErrors` list of rules waiting to be fixed would rebuild, under a new name, exactly the
"declared but not applied" state this record ends.

Two rules are demoted, both in `.editorconfig`, both with the argument written beside them:

- **`IDE0051`** (unused private member) is wrong here structurally. Every `EntityId<T>` subclass
  declares a private constructor that `EntityId<T>.BuildFactory` reaches through
  `GetConstructor(…, NonPublic)` and a compiled expression tree. Roslyn sees a member nobody calls;
  the runtime sees the only way an identifier is ever constructed. Acting on the warning would make
  `TrainerId.Create` throw. Five occurrences today, and one more for every identifier ever added.
- **`CS1591`** (missing XML comment on a public member) is the price of `GenerateDocumentationFile`,
  which is switched on only because `IDE0005` needs it. Requiring a doc comment on nine hundred and
  eleven public members is a decision about how this codebase is written, not about whether its
  analyzers are enforced. The compiler rules that find real documentation defects stay on, and each
  caught something the moment this was switched on: `CS1573`, `CS1574`, `CS1570`.

**`NU19xx` is exempt permanently, and that is not debt.** NuGet audit findings are not rules this
repository chose and then ignored; they are advisories about the dependency graph. Promoting them
means a CVE published against any transitive package turns all three workflows red with nobody
having pushed anything — and `Directory.Packages.props` already pins a package to close a GHSA, so
the hazard is live. They stay loud in the log and stop being a gate.

**Three builds opt out, each because it is not the gate:**

- **`scripts/generate-clients.sh`** compiles nine of the twenty-six projects, in Debug, *before* the
  build in all three workflows. A warning in `Shared.Domain` would fail a step called *"Regenerate
  the HTTP client"*, and `set -e` would swallow the script's own diagnostics on the way out. The
  real gate is the Release build of the whole solution fifty lines later, which catches the same
  warning in the same run.
- **The build inside `sonarscanner begin`** (`sonar.yml`) receives `SonarAnalyzer.CSharp` and a
  generated ruleset whose quality-profile rules are warnings. Promoted, every Sonar finding becomes
  a compilation error and the workflow dies before publishing the verdict it exists for. The gate
  there is the quality gate — [ADR 0018](0018-fail-on-the-gate-where-failing-stops-something.md).
- **`BLRefactoring.GeneratedClients`**, one project holding one machine-written file that CI
  regenerates and commits itself ([ADR 0008](0008-generate-the-http-client-from-a-script-and-verify-it-in-ci.md)).
  Its `<auto-generated>` header stops the analyzers but not the compiler, which is why NSwag ships
  fifteen `#pragma warning disable` directives. A regeneration emitting anything outside that list
  would break the build with no commit behind it, and the remedy would be hand-editing generated
  code.

**`AnalysisLevel` is pinned.** It otherwise follows the target framework, so the three `net9.0`
projects would analyse at a different level from the other twenty-three and the effective ruleset
would differ inside one solution. Measured first: aligning them costs nothing.

## Consequences

- The seventy-three severities in `.editorconfig` are now what they always claimed to be. The
  README's sentence is true because something keeps it true, rather than by habit.
- Forty-eight defects were fixed to get there — twenty-eight unnecessary `using` directives, four
  missing brace pairs, a double blank line, eight parameters missing from XML comments that
  documented their siblings, one `cref` resolving to nothing, and one malformed XML comment. The
  last two were real bugs in documentation nobody knew were there.
- **`CA2016` is in force for the first time.** Removing the duplicate promotes it from suggestion to
  error, and the census confirmed the code already satisfies it.
- **The published API gained documentation nobody asked for, and should have.**
  `GenerateDocumentationFile` was switched on for `IDE0005`, and ASP.NET Core's OpenAPI generator
  reads XML comments: the document now carries operation descriptions, and the regenerated client
  grew two hundred lines of them. It also made one comment's staleness visible — the registration
  remarks still named `/Trainer/{id}`, an address withdrawn in a previous change — which is the
  argument for publishing documentation rather than leaving it beside the code.
- Two architecture rules defend this record, both guarding failures that are *green*: a ruleset
  nothing enforces produces a passing build and a false README, and a diagnostic declared twice
  produces a passing build and a severity nobody chose.

Against that:

- **A contributor's build now fails on a formatting warning**, which is a harsher first experience
  than a warning they could ignore. That is the point, and the count says the cost is ten.
- **The floating SDK remains a way for this to break with no commit behind it.** CI pins only
  `10.0.x`, so a new band could ship analyzers with new default-enabled rules. `AnalysisLevel` being
  explicit bounds that; pinning the SDK in a `global.json` is the remaining step and is not taken
  here, because it is a decision about reproducibility rather than about enforcement.
- **Three opt-outs are three places the rules do not reach.** Each is named above with the reason,
  and none of them is the build that gates a pull request.

## Alternatives considered

**`-warnaserror` in the three workflows instead of the property.** Rejected: it promotes MSBuild's
own warnings along with the analyzers', it leaves a local build silent, and it states the policy in
three files that can drift instead of one that cannot.

**Enforce `TreatWarningsAsErrors` only, leaving `EnforceCodeStyleInBuild` off.** Cheaper by ten
fixes, and it would have left twenty-four rules declared and not applied — the smaller version of
exactly the defect this record closes.

**A `WarningsNotAsErrors` ratchet**, seeded from the census and emptied one rule at a time. The
right answer had the census returned hundreds. It returned ten, so the ratchet would have been
ceremony around a morning's work — and a list of rules-not-yet-enforced living in the build file.

**Skip `GenerateDocumentationFile` and demote `IDE0005`.** Ten fixes instead of forty-eight, and
honest. Rejected because the twenty-eight unnecessary usings are real, `IDE0005` is a rule worth
having, and silencing `CS1591` is a smaller and more defensible statement than silencing the rule
that finds dead code — particularly as switching documentation generation on is what surfaced the
`cref` and the malformed comment.

**Document all nine hundred and eleven public members.** Consistent with how much this repository
writes down, and a different piece of work entirely. Nothing about enforcing analyzers requires it,
and bundling it here would have buried a ten-line decision under a thousand-line diff.
