# 0049 — Measure duplication where repetition is a defect

- **Status:** Accepted — amended by [0079](0079-build-the-development-catalog-with-the-domain.md): a written corpus joins the two hosts under the duplication exemption, named in a registry that carries its argument
- **Amends:** [0017](0017-measure-what-the-rules-cannot-with-sonarqube-cloud.md)
- **Date:** 2026-08-07

## Context

ADR 0017 brought SonarQube Cloud in to measure "the things a rule cannot state as yes or no", and
named duplication among them. ADR 0018 made the gate fail a pull request, where failing stops
something. Both hold. What neither anticipated is a repetition this repository *requires*.

The rename of ADR 0048 failed the gate at **10.9% duplication on new code**, against a 3% condition.
It introduced no repetition: it changed type names on lines that were already identical, which is
enough to turn old duplication into *new* duplication.

**Where it is, measured rather than assumed.** Reproducing the copy-paste detection locally — ten-line
windows, comments dropped since they are not tokenised — and intersecting the duplicated blocks with
the pull request's changed lines gives 14.4% against the reported 10.9%: the same figure through a
cruder instrument, and an unambiguous location. Every duplicated new line is in one of four files.

| duplicated / new | file | matched against |
| --- | --- | --- |
| 6 / 8 | `src/DDD/Api/Controller/TrainerController.cs` | its `DDDWithCqrs` twin |
| 6 / 8 | `src/DDDWithCqrs/Api/Controller/TrainerController.cs` | its `DDD` twin |
| 1 / 8 | `src/DDD/Api/Controller/TrainingController.cs` | its `DDDWithCqrs` twin |
| 1 / 9 | `src/DDDWithCqrs/Api/Controller/TrainingController.cs` | its `DDD` twin |

And what is identical is the endpoint's *published declaration*: the routing attribute, the
`[ProducesResponseType]` run, the action signature.

```csharp
[HttpPut("me")]
[ProducesEntityTag]
[ProducesResponseType(typeof(TrainerHttpResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
public async Task<ActionResult<TrainerHttpResponse>> EditCurrentAsync(
    [FromBody] EditTrainerHttpRequest request,
    [FromHeader(Name = "If-Match")] string? ifMatch,
    CancellationToken cancellationToken = default)
```

That block is byte-identical across the two hosts because two rules require it to be.
`BothHosts_PublishTheSameOperations` (ADR 0008) holds that a client generated from either host fits
both; `BothHosts_AnswerEachOperationWithTheSameShape` (ADR 0029) compares the `status → type`
sequences host against host and fails when they differ. The repetition is not tolerated here — it is
enforced, by the rules that make this repository's central claim true.

**So the gate condition cannot be satisfied without breaking them**, and that is the whole of the
matter. `CLAUDE.md` already rules on the general case:

> Where SonarQube and this repository's ruleset disagree, the ruleset wins. […] Act on a finding when
> it names a real defect; never to make a style rule stop reporting.

This record is the rare converse of the sentence it is quoting, and it has to be read carefully to
avoid becoming its violation. The finding is not wrong about the text. It is wrong about what the
text *is* — not code somebody repeated, but one API rendered twice on purpose. Answering it by
deleting a copy would delete the demonstration; answering it by silence would be the move the
sentence forbids. So it is answered by naming the exemption, bounding it, and pinning it with a rule.

**Test code is not involved.** Every project under `tests/` declares `IsTestProject`, and the
detector skips test files, so the figure is about main code alone.

## Decision

**The duplication measure exempts the two host API projects, and nothing else.**

```
/d:sonar.cpd.exclusions="src/DDD/Api/**,src/DDDWithCqrs/Api/**" \
```

- **`sonar.cpd.exclusions`, not `sonar.exclusions`.** The distinction is the decision. The first
  removes a file from the copy-paste detector; the second removes it from the analysis. Bugs,
  hotspots and coverage on the host controllers keep being read — they are the busiest files in the
  repository, and a duplication figure is no reason to stop looking at them.
- **Whole projects rather than the controllers alone.** Each host `Api` project holds five files —
  `Program.cs`, `Mappings/HttpToApplicationMappings.cs` and three controllers — and every one is the
  second copy of its twin. The `Program.cs` pair already shares eleven blocks and would fail this
  gate on the next change touching both composition roots. The exemption states what is true of the
  projects rather than what was inconvenient this week.
- **The 3% condition stays** for everything else, unchanged, including the shared API layer where the
  contracts actually live.
- **A rule holds the list to the hosts**, derives it from the solution rather than restating it, and
  fails if it grows.

## Consequences

- **Duplication inside a single host project stops being measured.** Five files each; none duplicate
  one another today, and that was checked rather than hoped. It is the price, and it is the reason
  the exemption is bounded by a rule instead of a comment.
- **The exemption lives in the tree.** It is a line in a workflow, reviewed in a diff like anything
  else — which is ADR 0018's own argument, that a build status should be a function of the tree
  rather than of something an administrator can edit in a web interface.
- **A third host cannot arrive quietly.** The rule reads `Solution.Hosts`, so adding one and
  forgetting the exemption fails the architecture suite rather than the gate, months later, on
  somebody else's pull request.
- **A future finding cannot be filed under this record.** Any path in `sonar.cpd.exclusions` that is
  not a host of this API fails the rule. Without that clause this record would be a place to put
  inconvenient numbers, which is exactly what it is written to not be.

## Alternatives considered

**Share the declaration through a base controller.** The only alternative that removes the
repetition rather than reclassifying it, and it would work: MVC reads attributes inherited from a
base class, so an abstract base carrying the route and the `[ProducesResponseType]` run would leave
each host one override. Rejected because it makes the two hosts share their HTTP surface *by
construction*. The repository exists to show one domain under two application styles, with every
endpoint written twice and rules proving the two agree; a shared base deletes both the demonstration
and the thing the rules were proving. That is a large architectural decision, and it would be a
strange one to take because a detector counted lines.

**Relax the condition in the SonarQube Cloud quality gate.** One number in a web interface, and the
gate stops failing. Rejected on ADR 0018's ground: it would make a check depend on a setting that can
change without a commit, invisible to anybody reading the repository, and it would relax the
condition for *all* code rather than exempt the code that has a reason.

**Add the host controllers to `sonar.exclusions`.** The setting already present, one comma away.
Rejected because it would stop measuring bugs, hotspots and coverage on the files every request in
this API passes through — trading the analysis for a duplication figure, which is a far worse deal
than it looks when writing the comma.

**Leave it, and fail the gate on every change touching both hosts.** The purist answer, and it has a
real cost this repository has already priced twice: ADR 0017's guard job and ADR 0018's conditional
wait both turn on the same sentence — a check that is red for reasons unrelated to the diff is one
people learn to ignore. A gate that fails on most pull requests here, for repetition two rules
require, teaches exactly that.

## Verification

`TheDuplicationMeasure_ExcludesTheHostsWrittenTwiceAndNothingElse` reads the workflow and holds three
things that fail separately. Each was watched red on its own before the setting was added, and green
after:

1. **Every host is covered** — with the setting absent, both hosts are named as missing. The list
   comes from `Solution.Hosts` matched against the project files, per ADR 0041, so it cannot drift
   from the solution.
2. **It is the duplication measure that is relaxed** — adding `src/DDD/Api/**` to `sonar.exclusions`
   fails the rule, naming the host and what would stop being read.
3. **The exemption does not grow** — adding `src/TrainingHub.Shared.Api/**` to
   `sonar.cpd.exclusions` fails the rule, naming the path and calling it what it would be.

The gate itself is the last proof, and it is checked rather than predicted: the four files carrying
every duplicated new line all sit inside the exemption.
