# 0018 — Fail on the gate where failing stops something

- **Status:** Accepted
- **Date:** 2026-08-02
- **Amends:** [0017](0017-measure-what-the-rules-cannot-with-sonarqube-cloud.md)

## Context

ADR 0017 decided that the quality gate is waited on rather than reported:

> `sonar.qualitygate.wait` makes the job itself go red rather than leaving the verdict on a website,
> so the result is visible where every other check is.

That reasoning holds on a pull request. It does not survive contact with the default branch, and the
first analysis of `master` showed why within minutes of the record being merged.

The gate failed on `master` — a security hotspot, the JWT signing key that
`appsettings.Development.json` and `docker-compose.yaml` carry in the tree. The analysis itself was
faultless: nine coverage reports imported, a hundred and sixty-seven main files with coverage,
report uploaded. Only the verdict was negative.

The consequence was a red cross on the default branch of a repository whose purpose is to be read.
And that cross **stopped nothing**. The code was already merged; there was nothing left to prevent.
It could not even be cleared by answering the finding, because the check is a snapshot of a run that
has finished — only a further push to `master` repaints it.

So the failure was expressing something no reader could act on, in the place where a project is
judged at a glance. This repository already has an argument about exactly that, written one record
earlier: the guard job of ADR 0017 exists so that a missing token *skips* the analysis instead of
failing it, because "a check that is red for reasons unrelated to the diff is one people learn to
ignore." A red default branch for an already-merged state is the same failure of the same kind, and
0017 shipped it while arguing against it.

## Decision

**The gate is waited on for a pull request, and not for a push to `master`.**

```yaml
/d:sonar.qualitygate.wait=${{ github.event_name == 'pull_request' }}
```

On a pull request, failing is the entire point: something is about to enter `master` and the gate is
what stops it. On `master` the analysis still runs, still uploads, still computes the gate — the job
simply does not fail on the verdict.

**Nothing is hidden by this.** The gate's status remains on the branch in SonarQube Cloud, on its
badge and on its dashboard. What changes is that it stops being expressed as a broken build, which
is what it never was.

## Consequences

- The default branch is green when it builds and its tests pass, and that is all a green cross on
  `master` ever honestly claimed.
- A failing gate on `master` is still visible — on the dashboard and the badge, where a *measure*
  belongs, rather than as a CI failure, where a *breakage* belongs.
- Branch protection is unaffected: the check to require is still `Analyze`, and it still fails on a
  pull request whose gate is red. What it protects is unchanged.
- A rule reads the workflow and fails if the wait becomes unconditional again, in either direction.

Against that:

- **A gate failure on `master` is now quieter.** Somebody has to look at the dashboard, or notice
  the badge, rather than being confronted by a red cross on the commit list. That is the trade: the
  cross was louder, and it was also lying about what it meant.
- **The two events now behave differently**, which is one more thing to know about the pipeline.
  The alternative was a uniform behaviour that was wrong on one of the two.

## Alternatives considered

**Leave it, and keep `master` green by keeping the gate green.** The purist answer, and not wrong:
if the gate fails, fix what it found. Rejected as the general rule because it makes the default
branch's colour depend on a verdict that can change without a commit — a hotspot reviewed in a web
UI, a gate condition edited by an administrator. A build status should be a function of the tree,
and this one would not have been.

**Report the gate on `master` as a warning through `::warning::`.** Keeps a signal in the run log
without failing. Rejected as clutter: the dashboard already says it, better, and a warning nobody is
required to read is worth about as much as a badge nobody looks at — with an extra step in the
workflow to maintain.

**Drop `sonar.qualitygate.wait` entirely and rely on the Sonar app's own check.** The app posts
`SonarCloud Code Analysis`, which already carries the verdict on both events, so the wait is in some
sense redundant. Rejected because that check reports the *gate*; the `Analyze` job also fails when
the *analysis* breaks — a bad token, an unreachable server, a scanner that cannot parse a coverage
report. Those are real failures of this pipeline, and they deserve to fail this pipeline.
