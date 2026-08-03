# 0022 — Name the repository after the domain it serves

- **Status:** Accepted
- **Date:** 2026-08-03

## Context

The repository was called `BLRefactoring` — *business-logic refactoring*. That was an accurate name
for what it was on the first day: an exercise in moving logic out of the wrong place. It stopped
being accurate a long time before this record, and the gap became a problem rather than an
inelegance once the destination was stated out loud: a public catalogue where people search
trainings, read trainer profiles and browse by topic.

A name that describes the *activity that produced* a codebase rather than the *thing it is* costs
something specific. It appears in every namespace, so every file opens by announcing the wrong
subject. It appears in the assembly names, so a stack trace says it. And it is the first word a
reader sees, which makes the README's opening line an act of correction.

The name also had to be chosen against a constraint that ruled out the obvious answers. `Training`
and `Trainer` are types in the domain model. A root namespace of `Training` would make the
identifier `Training` mean a namespace in some scopes and an aggregate in others — legal, and
unreadable.

## Decision

### The repository, its solution, its assemblies and its namespace root are `TrainingHub`

`TrainingHub` names the product rather than an aggregate of it, which is the same reasoning ADR 0021
used to prefer `IObjectStore` over `ITrainerPhotoStorage`: a name taken from one part of the model
is a name that has to be changed the day a second part needs the same thing. A catalogue that will
hold trainings, trainers, topics and eventually search belongs to none of them in particular.

`Hub` carries the read side that is coming — aggregation, browsing, search — where `Center` names a
place, `Portal` names an intranet, and `Platform`, `Suite` and `Solution` add length without adding
meaning.

Rejected on the same criterion: `CourseHub`, `CoursePlatform` and `LearningPlatform`, each of which
introduces *course* or *learning* at the root while every type in the model says `Training`. One
concept spelled two ways at two altitudes is the split that ubiquitous language exists to prevent,
and the root of a namespace tree is the worst place to start it. `TrainingCatalog` was the closest
alternative and lost because the catalogue is one view of the product: it says nothing about
authoring a training, owning one, or signing in.

### `AssemblyName` and `RootNamespace` stay unset, and that is what made the rename mechanical

The SDK derives both from `$(MSBuildProjectName)` — the project *file* name, not its folder — and
every project here is named in full (`src/DDD/Api/TrainingHub.DDD.Api.csproj` sits in a folder
called `Api`). `NoProject_OverridesItsRootNamespaceOrAssemblyName` has held that since ADR 0013's
generation of rules, and this rename is the first time the property was worth its keep: renaming
twenty-five project files renamed twenty-five assemblies and twenty-five namespace roots, and
`EveryNamespace_AgreesWithItsFolder` then had something to check the result against.

Declaring the two properties explicitly was considered and rejected. It buys one thing — an assembly
name that survives a file rename — and costs a second source of truth restated twenty-five times.
Its failure mode is precisely the operation being performed here: rename the file, forget the
property, and the assembly quietly keeps a name nothing else uses.

### The former name survives in one place, and it is not ours to change

The SonarCloud project key is `maxime-poulain_BLRefactoring`, and it stays that way.

A SonarCloud project key is **immutable**: it is set at import, and the only way past it is to
delete the project and create another. That discards every measurement ever taken — the coverage
history, the quality-gate baseline, the record of what the new-code condition was compared with.
ADR 0017 and ADR 0018 exist to make that gate mean something; spending its history to make a string
match would be paying in exactly the currency those records are about. The binding between the
project and the repository is by key rather than by name, so it survives the rename untouched.

The GitHub repository itself was renamed to `maxime-poulain/TrainingHub`, and the badge URLs were
rewritten to match. GitHub redirects the old paths indefinitely, so neither form breaks; the new one
is written because a badge is also a link a reader follows.

`NothingInTheRepository_StillCarriesTheFormerName` therefore permits the name in one position only —
immediately after `maxime-poulain_` — and nowhere else. An exception narrow enough to state in a
single clause is a decision; a list of them is a leak.

### The records were renamed, and this is not a rewrite

`docs/adr/README.md` says a merged record is never rewritten. The identifiers inside ADR 0001–0021
were nonetheless renamed, and the distinction is worth stating because it looks like a violation.

That convention protects the *reasoning*: what was open at the time, what it would have cost, why
the loser lost. None of that is touched by the name of a project. What is touched is whether a
reader can follow the record — an ADR from 2026 pointing at `BLRefactoring.Shared.Domain` names a
project that no longer exists anywhere, and the reader has to work out that it is the same thing
under a different name before they can read the argument at all. Leaving the old names would
preserve the letter of the convention by damaging the thing it protects.

The alternative — a note in each of the twenty-one records mapping old names to new — was rejected
as twenty-one copies of one fact, which is the shape of thing this repository deletes on sight.

## Consequences

**The rename is verified by the build rather than by reading.** 1372 occurrences across 393 files
and 49 renamed paths is past what review catches. What catches it instead is already here:
`EveryNamespace_AgreesWithItsFolder` compares every declaration with its path,
`TheDiagram_DescribesExactlyTheEdgesTheProjectsDeclare` compares the README's graph edge by edge
with the project references, and the compiler runs with `TreatWarningsAsErrors`. A half-finished
rename does not produce a stale document; it produces a red build.

**The database is now called `TrainingHub`.** The connection strings in both hosts' development
settings and in `docker-compose.yaml` name it, and an existing local `BLRefactoring` database is not
migrated to it — it is simply no longer addressed. Migrations run on startup in Development
(ADR 0003), so the new database is created on the next run. Nothing is dropped: the old one stays on
disk until somebody removes it.

**The JWT issuer and audience changed with it.** They are configuration values compared as strings
at validation time, so every token issued under the previous pair stops validating the moment the
new settings load. In development that is a sign-in; there is no deployment where it is more.

**The generated HTTP client was regenerated, not edited.** ADR 0008 makes the API the source of
truth and has CI commit the difference, so editing `Clients.Generated.cs` by hand would have been
undone on the next push. The generator's own configuration carries the namespace, and the regenerated
file matched the swept one exactly — which is the check that the sweep did not invent anything the
generator would disagree with.

**One string is now written in two places on purpose.** The rule holds the former name in order to
search for it, and this record holds it in order to say what was renamed. Both are listed by path in
the rule itself rather than detected by pattern, so the exemption is reviewable: a third file
claiming the same excuse has to be added by hand, in a diff.

**The quality gate fails on the pull request that carries this rename, and the number it reports is
not about the rename.** SonarQube Cloud answered `73.1% Coverage on New Code (required ≥ 80%)`. The
diff says that cannot be a fact about what this change wrote: read with rename detection on, it adds
1172 lines of C#, of which 658 are `using` directives, 328 are namespace declarations, 60 are
comments and 66 of the remaining 126 are EF migration snapshot lines that `sonar.exclusions` drops.
What is left is the architecture suite's own new code, which the architecture suite executes. A set
like that reads as fully covered or it reads as nothing at all.

Read with rename detection *off*, the same diff is 33 911 insertions across 735 files — the whole
repository. That is the set the gate measured, so "coverage on new code" collapsed into "coverage of
this codebase", and the condition stopped describing the change under review. Nothing about how much
of this code is tested moved by a single line.

This is recorded rather than argued away, because the consequence is specific and bounded: the gate
does not wait on a push to master (ADR 0018 — it only blocks where blocking stops something), and
the next pull request's baseline already contains the renamed files, so its new-code set is a
new-code set again. The failure belongs to this one change and to the mechanism that computes the
diff, not to the quality bar.

**A rule that reads every file reads a tool's scratch too, and this one found out on CI.** The first
run failed on `.sonarqube/out/26/output-cs/Context/Source.pb` — SonarScanner caches the analysis
context as protobuf, and the SonarCloud project key is part of it. The finding was correct about the
bytes and wrong about the subject: that file says what SonarCloud calls this project, which is
exactly the one thing this rename does not change. `SourceTree.AllFiles` therefore skips a named set
of workspace artefacts — Git's store, the test runner's reports, the editor's cache, the scanner's —
on the ground that a file inside any of them describes the state of a tool rather than anything this
repository says. The list is written out rather than derived from `.gitignore`, because an exclusion
that a rule computes for itself is one nobody reviews.
