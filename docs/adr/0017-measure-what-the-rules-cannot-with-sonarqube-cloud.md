# 0017 — Measure what the rules cannot, with SonarQube Cloud

- **Status:** Accepted — amended by [0018](0018-fail-on-the-gate-where-failing-stops-something.md)
- **Date:** 2026-08-02

## Context

This repository already fails a build for a great many reasons. The solution has to compile, the
unit suite has to pass, eighty-eight architecture rules have to hold, the committed HTTP client has
to match the API, and — on agent branches and nightly — an integration suite has to pass against a
real SQL Server. ADR 0013 made every record answer to a test, so a decision that stops being true
turns a run red.

What none of that does is *measure*. Every one of those checks answers yes or no about a rule
somebody wrote down. Three things follow from that, and all three were true of this repository when
this record was written:

**No coverage figure exists.** `dotnet test` is invoked without a collector in either workflow, and
no project references one. Nobody can say which of the twenty-two thousand lines the suites reach,
which means nobody can say whether a new file arrived tested.

**Nothing looks for a security problem.** `appsettings.Development.json` carries a JWT signing key
and a database password in the tree. That is a deliberate convenience for a sample, and it is also
exactly the shape of finding that a scanner exists to raise — as are the ones nobody put there on
purpose.

**Nothing measures duplication**, on a repository whose entire subject is two stacks that must not
drift. The architecture rules check where code lives and what it may reference; none of them can
notice that two handlers have become the same forty lines.

There is a fourth gap, and it is the one this record deliberately does *not* close. `.editorconfig`
sets seventy-three analyzer rules to `severity = warning`. No project sets `TreatWarningsAsErrors`
and neither workflow passes `-warnaserror`, so every one of those warnings is emitted and ignored.
The repository already owns a configured ruleset it does not enforce.

## Decision

**SonarQube Cloud, not SonarQube Server.** The repository is public and MIT-licensed, which makes
the cloud offering free for it. The server edition would mean a host, a database, upgrades and
backups, in exchange for nothing this project needs — for a repository whose point is to be read,
self-hosting an analyser is a liability with no reader-facing benefit.

**In a workflow of its own, not as steps inside `ci.yml`.** That workflow holds `contents: write`
for the one step that commits the regenerated client, and it is the fast answer a pull request waits
on. Wrapping an analyser around its build would hand that write permission to a third-party tool and
lengthen the loop `ci.yml` exists to keep short. The price is one extra build per run; the price of
merging them is paid on every push.

**Coverage is collected, in OpenCover format, by both suites.** `coverlet.collector` is referenced
by every project that runs tests, and the analysis workflow runs the whole suite — integration tests
included. This revisits, in this workflow only, the decision that keeps integration tests off the
pull-request path: `ci.yml` stays exactly as fast and as unconditional as it was, and the slower
signal arrives beside it rather than inside it. The reason is that a coverage figure produced
without them would be false. Nearly every assertion in this repository crosses routing, model
binding, authentication and a real database; a gate reading the unit suite alone would fail pull
requests whose code is covered by tests it never ran.

**The generated client is excluded.** Two thousand four hundred machine-written lines would
otherwise decide the duplication and maintainability figures for the whole project.

**The gate is waited on, and blocks nothing until somebody turns it on.** `sonar.qualitygate.wait`
makes the job itself go red rather than leaving the verdict on a website, so the result is visible
where every other check is. Making it a required check in branch protection is a separate,
deliberate act, taken once the first analysis of `master` is green — a gate switched on before any
baseline exists blocks every pull request on a number nobody has seen.

**A missing token skips the analysis rather than failing it.** Until the repository is connected to
SonarQube Cloud, the analysis job is skipped by a guard job that reads the secret. A red check for
an absent secret says nothing about the code, and a check that is red for reasons unrelated to the
diff is a check people learn to ignore.

## Consequences

- A coverage figure exists for the first time, and it is honest about which suites produced it.
- Security hotspots, cognitive complexity and duplication are measured — the three things the rule
  suite structurally cannot see, because each is a matter of degree rather than a yes or no.
- Pull requests are decorated by the Sonar GitHub App: one summary comment, edited in place, and a
  check carrying the gate's verdict.
- It earned its keep on the first run. The analysis failed the gate on a *C security rating on new
  code*, and the finding was in `sonar.yml` itself: `permissions` declared once for the workflow
  rather than per job, so every job added later would inherit a grant written for somebody else.
  Both workflows now declare theirs on the job, and the guard job — which checks out nothing and
  calls nothing — holds `permissions: {}`. Eighty-eight architecture rules had nothing to say about
  it, which is the point of this record.
- Two architecture rules defend the arrangement: every project that runs tests collects coverage,
  and the workflow analyses both the pull request and the branch it targets while excluding
  generated code. Both guard failures that are *green* — a missing collector reports silence, which
  the gate reads as uncovered, and a lost exclusion measures NSwag's output as if somebody wrote it.

Against that:

- **A third-party service now has an opinion on this codebase, and it has not read the records.**
  Sonar will raise findings that ADRs 0001 to 0016 deliberately decided against — its generic C#
  rules know nothing about why `EntityId` compiles a factory, or why the kernel carries
  `Mediator.Abstractions`. Triaging those, and marking them *won't fix* with a reason, is recurring
  work this pipeline did not have.
- **A large part of what it reports, the SDK already reported.** Many Sonar C# rules restate a
  Roslyn analyzer that has been running on every build all along, and being ignored.
- **The pull-request path grew a slower check**, including a SQL Server container. `ci.yml` still
  answers in about a minute, so the fast signal survives; the slow one is additional, not
  substitutionary.
- **One more build per run**, which is the cost of not touching `ci.yml`.

## Alternatives considered

**Enforce the seventy-three rules `.editorconfig` already declares**, by setting
`TreatWarningsAsErrors` or passing `-warnaserror` in CI. Cheaper than this record by a wide margin,
needs no service, no token and no network, and acts on a ruleset somebody already chose. It is not
an alternative so much as the step that should have come first, and it is left out of this record
only because nobody knows how many warnings the build currently emits — turning it on blind would
either change nothing or paint the pipeline red, and finding out which is a piece of work with its
own record. **Recommended as the next one.**

**SonarQube Server, self-hosted.** Rejected above: cost with no benefit for a public repository
whose readers are not going to be given a VPN.

**Analyse inside `ci.yml`.** One build instead of two, and one workflow instead of three. Rejected
because it puts a third-party analyser inside the only job holding `contents: write`, and because it
adds minutes to the check every pull request waits on.

**Collect coverage from the unit suite only**, keeping the analysis fast and leaving integration
tests where they are. Rejected because the resulting figure would be worse than none: the suites
that cross the HTTP boundary are where this project's assertions live, and a gate that ignores them
would demand unit tests for code already proven to work — pressure toward tests written to move a
number.

**Skip coverage entirely and gate on issues alone.** Rejected because Sonar's default gate reads a
missing coverage report as zero percent on new code, so the gate would fail every pull request until
coverage existed anyway. Choosing not to measure would not have avoided the decision, only hidden
it.
