# 0047 — Verify the build a pull request delegates

- **Status:** Accepted
- **Date:** 2026-08-07

## Context

`ci.yml` fires on `push` for `master` and `claude/**`, and on `pull_request` for `master`. An agent
branch therefore triggers both events for the same commit once a pull request is open, and PR #67
removed the duplication the cheap way: a job-level `if` that skipped `Build & Test` for a same-repo
`claude/**` pull request, because *"its commit was already built when it was pushed"*.

That sentence is a claim about a run the workflow never looked at, and the comment carrying it
described the mechanism that makes the claim dangerous, without drawing the conclusion:

> The job is skipped rather than the workflow, so the check still appears on the pull request — as
> skipped, **which GitHub counts as passing for a required check**, beside the successful one the
> push run posted on the same commit.

"Beside the successful one" is the assumption. When there is no successful one, the skipped check
passes alone.

**This is not hypothetical, and it was measured on this repository.** On the night of 2026-08-06,
GitHub-hosted runners were unobtainable for several hours. Jobs were created and never served —
`master`'s own `Build & Test` for `472ef98` shows `runner_id: 0`, `runner_name: ""`, created at
17:52:43 and cancelled at 18:07:44 after fifteen minutes in the queue. The same happened to the
Integration Tests and to Sonar's guard job on an agent branch. Had a pull request been open on that
branch, its `Build & Test` would have been a green skip over a build that was cancelled before it
started.

The same file already records having been caught by the neighbouring version of this defect: the
first draft of the condition skipped *every* same-repo pull request, `feature/*` branches fired no
push run, and PR #62 merged built by nobody. That was fixed by narrowing which branches delegate.
This record fixes the half that narrowing could not reach — a branch that delegates correctly, to a
build that did not succeed.

## Decision

**A check is green only for a build that happened. Where a run delegates its build, it waits for
that build and adopts its verdict.**

- **The job always runs.** The job-level `if` is gone. A job skipped by a job-level condition still
  posts its check, and GitHub reads that skip as a pass — so a job that can be skipped is a check
  that can be green for nothing. Every step now carries the condition instead, which is noisier in
  the file and is the point.
- **Delegation is decided in a step, and verified there.** `Was this commit already built?` asks the
  Actions API for the `push` run of this workflow whose `head_sha` is the pull request's head, and
  requires `completed success`. Anything else — a failure, a cancellation, a timeout — fails this
  job, with a message naming what the delegated run actually concluded.
- **It waits for a conclusion, not for a duration.** The two events start together, so at the moment
  the delegation runs the push run is normally still building. Polling ends when that run concludes;
  the verdict adopted is the run's own, never a guess about timing. Fifteen minutes bound the wait,
  under the job's twenty, and reaching that bound is a failure — *"no build has concluded for this
  commit"* is the honest answer, and a re-run adopts the verdict once one exists.
- **Who delegates does not change.** A push run is the primary build. A fork's pull request is the
  only build it will ever get. A same-repo branch the `push` trigger does not name has no push run
  to stand on. Only a same-repo `claude/**` pull request delegates, exactly as before.

## Consequences

- **The optimisation survives and the assumption does not.** One build per commit still, at the cost
  of a runner waiting a couple of minutes on the delegating side rather than a second full build.
- **A red pull request during a runner outage, instead of a green one.** That is the change. During
  the incident above, an open pull request would now fail with *"no push run reached a conclusion"*
  rather than presenting a passing check. Slower to merge, and true.
- **The workflow reads GitHub's own state.** `actions: read` is added to the job for that one call.
  It is the smallest permission that answers the question, and the job's `contents: write` — which
  exists for the client regeneration — is unreachable on the delegating path, since none of the
  steps that write run there.
- **`TheBuild_DoesNotRunTwiceForOneCommit` changes what it matches, not what it claims.** It looked
  for the job-level condition; it now looks for the delegation step. The overlap it guards is still
  resolved, in a different place.
- **The comment that named the defect is replaced by one that names the fix.** A comment describing
  a mechanism nobody had decided to keep is worse than no comment.

## Alternatives considered

**Build in the pull-request run too, and drop the delegation.** The obvious answer, and it was
weighed first: correctness is worth eighty seconds. Rejected because it reopens a defect this file
has already suffered. The two runs land in different concurrency groups, both regenerate the HTTP
client, and both commit it when the API has changed — the loser to a non-fast-forward nobody caused.
Closing that would mean a shared concurrency group and a serialised second build, which costs more
than it saves and changes a write path to fix a read one.

**A separate guard job, leaving the skip in place.** Cleaner YAML: `Build & Test` stays skipped, and
a new job reports whether the delegated build succeeded. Rejected because the check that gates the
merge would still be the skipped one. Making the guard block instead requires editing the repository
ruleset, which is configuration this repository does not hold in its own tree — a fix that only works
if somebody remembers to finish it elsewhere is half a fix.

**Rely on the push run's check winning over the skipped one.** Both runs post a check named
`Build & Test` on the same commit, so possibly the honest one decides. Rejected on principle rather
than on evidence: which of two same-named check runs a required check consults is platform behaviour
this repository has not measured, and the file's own history is a list of assumptions about the
platform that turned out to be wrong. A design that does not need the answer is better than one that
needs it and guesses.

**Leave it.** The window is narrow and needs a runner outage to open. It opened this week.

## Verification

`NoDelegatedBuild_IsTakenOnTrust` holds three things about `ci.yml`: that the build job carries no
job-level `if:` — matched at the job's own indent, so a step's condition does not satisfy it — that
the delegation looks its run up by the commit under review, and that it requires that run to have
concluded successfully. The last is the one that matters: a run that exists and failed is exactly the
case this record was written for.

Watched red first, with a job-level `if:` put back, which is the shape the defect had.
