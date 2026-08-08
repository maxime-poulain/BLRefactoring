# CLAUDE.md

This repository is a showcase project: it exists to demonstrate professional engineering practice,
not to maximise feature delivery. Architectural consistency, readability and long-term evolvability
outrank shipping speed. Understand the existing design before changing it.

## Read first, in this order

1. `README.md` — the architecture, the domain model, the conventions.
2. `docs/adr/README.md` — the index of 57 architecture decision records.
3. The records relevant to what you are touching.
4. `tests/TrainingHub.Architecture.Tests/` — the same decisions as 169 executable rules. Often
   faster than reading prose: each rule names the record it defends and quotes it.
5. The existing implementation.

ADRs are the source of truth. If the implementation contradicts an accepted record, that is a defect
— unless a later record explicitly supersedes it.

## Commands

```bash
dotnet build TrainingHub.slnx --configuration Release          # zero warnings, or it is a failure
dotnet test  TrainingHub.slnx --filter "FullyQualifiedName!~IntegrationTests"   # no Docker needed
dotnet test  TrainingHub.slnx                                  # everything; needs Docker
./scripts/generate-clients.sh                                  # after any change to the API surface
docker compose up -d                                           # SQL Server + SeaweedFS + Mailpit
```

## Traps that cost a CI round-trip

- **An incremental build skips analysers.** A local "0 warnings" on a project MSBuild considers up
  to date proves nothing. Before trusting it: delete every `bin/` and `obj/`, then rebuild in
  Release. An unused `using` (IDE0005) is an *error* here, and that is how one reaches CI.
- `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on. XML documentation is required on
  every public member (CS1591).
- The integration suites need Docker. Without it, run the filtered command above and say plainly
  which suites did not run — never report a suite green that never started.
- Both API hosts must publish the same operations. Every endpoint is written twice, in
  `src/DDD/Api/` and `src/DDDWithCqrs/Api/` (`BothHosts_PublishTheSameOperations`).
- The README's mermaid graph is compared edge by edge with the project references. Changing a
  `ProjectReference` means updating the diagram in the same commit.
- Every NuGet version lives in `Directory.Packages.props`; a `PackageReference` never carries
  `Version`. A new entry carries the comment that file's convention requires.
- Never set `RootNamespace` or `AssemblyName`. A namespace is the csproj file name followed by the
  folders (`EveryNamespace_AgreesWithItsFolder`).
- `src/TrainingHub.GeneratedClients/Clients.Generated.cs` is generated. Regenerate it; never edit it.

## One domain, two application styles

`src/TrainingHub.Shared.*` holds the domain, the kernel, persistence and the HTTP boundary. Two
stacks consume it and every use case exists in both:

- `src/DDD` — application services. Controllers inject `ITrainerApplicationService` and friends.
- `src/DDDWithCqrs` — commands and queries, dispatched through `ICommandDispatcher` /
  `IQueryDispatcher`. Controllers name no command or query; `HttpToApplicationMappings` builds them.

Both hosts page their lists with the kernel's `PageRequest`/`PagedResult` over the same total
order (ADR 0001, ADR 0029): the CQRS handler projects columns, the layered service asks the
repository a named question and maps the aggregates.

## Domain

- Business rules live in the domain. Aggregates accept value objects and typed identifiers, never a
  `string`, a `Guid`, or an object shaped like an HTTP request.
- Constructors are private; a value object is built through a factory that can refuse, returning
  `Result<T>`. Classes are sealed unless inheritance is a decision (ADR 0014).
- `Result` exposes no `IsSuccess` and no `Value`. Use `Match`, `MatchAsync`, `Bind`, `Switch`.
- An aggregate answers whether a change was allowed; it is not a way of reading state
  (`NoAggregate_ReturnsData`). The one pinned exception is a boolean question wearing a domain
  specification (`Training.IsOwnedBy`, ADR 0028).
- A specification names a business rule, or it does not exist: declared in the domain beside its
  aggregate, one expression answering both in memory and as a query criteria, never a query DSL —
  repositories expose named questions, and the CQRS readers never touch one (ADR 0028).
- A rule the aggregate cannot settle alone comes to it through a port declared beside it
  (`IUniquenessTitleChecker`, `ITrainingCounter`): the port answers the fact, the factory makes
  the decision, and the domain names no service to decide in an aggregate's place (ADR 0030).
  A decision with **no** home at all is a *recorded* domain service — named `*DomainService` in
  full, never a bare `*Service`, static, stateless, ports as parameters, pinned by rule
  (`TrainingTransferDomainService`, ADR 0036).
- Each aggregate owns the error codes it raises, prefixed with its own name — `Trainer.PhotoTooLarge`
  (ADR 0015). `ErrorCodes.Validation` belongs to the FluentValidation pipeline alone (ADR 0016).

## CQRS

- A command answers a **bare `Result`** — never `Result<T>`. What changed is read back with a query:
  a write that hands back what it wrote is a read in disguise (`EveryCommand_AnswersWithABareResult`).
- A query never changes state, and answers a `*Dto` — never an aggregate, entity or value object.
- Command handlers live in the application layer, query handlers in infrastructure.
- One validator per command, beside it. One folder per use case.
- **Every identifier a command or query carries is refused empty by its own validator**, even
  where the HTTP contract already refuses it (ADR 0046). The two answer different callers: the
  contract answers a request, the validator answers anything that reaches a dispatcher. The
  application layer never assumes the boundary checked first
  (`EveryIdentifierAMessageCarries_IsRefusedEmptyByItsValidator`).

## HTTP boundary

- The published contracts are `*HttpRequest` and `*HttpResponse`, under `Shared.Api/Contracts/`. No
  controller names a command, a query or an application DTO, and no inner layer names a contract.
- **The qualifier says which boundary a type belongs to, and the two must never be confused.** The
  layered stack's application services take a `*Request` and answer a `*Dto`; the API's published
  contracts are `*HttpRequest` and `*HttpResponse`. `EditTrainerHttpRequest` is what a client sends;
  `TrainerEditionRequest` is what the application layer accepts, after the mapping.
- **Both end in `Request`, so the suffix no longer places a type and no rule may ask it to**
  (ADR 0048). What an action binds or answers is named for the transport and lives under
  `Contracts/`; what an inner layer declares never is. That is checked per assembly rather than per
  string, in two halves that can fail separately — a layered signature that takes an `*HttpRequest`
  (`EveryLayeredServiceSignature_SaysWhichBoundaryItIsOn`), and an inner layer that declares one
  (`NoInnerLayer_DeclaresATypeNamedForTheTransport`).
- The CQRS stack names its inputs differently on purpose — a `*Command` or a `*Query`, one folder
  per use case — and answers the same `*Dto`. The `*Request` half is the layered stack's; the `Dto`
  half is shared by both.
- Every failure leaves as RFC 7807 Problem Details, with domain codes under `domainErrors`
  (ADR 0004, ADR 0012).
- Every action declares the statuses it can answer; every route identifier is constrained
  (`{id:guid}`). A creation answers 201 with the address of what was created (ADR 0011).

## C# style

The build enforces the style, so match what is there rather than normalising it:

- **Both member forms are deliberate**: a block where there is a guard clause, an arrow where the
  member is one expression. `.editorconfig` declines to pick a side and says why — do not convert
  one into the other. Properties, indexers and accessors *must* use expression bodies (IDE0025-0027
  are errors).
- **Primary constructors for injected dependencies** — controllers, handlers, adapters. Ordinary
  constructors elsewhere; IDE0290 is a suggestion on purpose, so do not convert the four that remain.
- File-scoped namespaces, `var`, Allman braces, and a hundred and sixty-one analyzer severities, all
  enforced at build time.
- **Where SonarQube and this repository's ruleset disagree, the ruleset wins.** A Sonar finding is
  never on its own a reason to rewrite code `.editorconfig` deliberately allows: the quality profile
  is somebody else's list, while every severity here was chosen for this codebase and every demotion
  carries the argument for it (`EveryDemotedRule_SaysWhyItWasDemoted`). Act on a finding when it
  names a real defect; never to make a style rule stop reporting. Examples written in this file
  follow the same rule — a complete member rather than a fragment.

## Tests

- Behaviour changed → unit tests.
- API behaviour changed → an integration test in `tests/TrainingHub.Api.TestKit/`, so both suites run
  it rather than one.
- An architectural rule changed → the rule, carrying
  `[ArchitectureRule("<adr>", "the decision in the record's own words")]`.
- Assertions are AwesomeAssertions; `Assert.*` is refused by a rule (ADR 0007).
- Before trusting a new rule, break the thing it defends and watch it fail. A rule that has never
  failed has never been shown to check anything.

## Documentation

- A new architectural decision → a new record in `docs/adr/`, a row in `docs/adr/README.md`, and a
  rule that defends it. ADR 0013 makes that last part mandatory.
- A merged record is never rewritten. A decision that changes gets a new record superseding it.
- Update the README when a convention, a workflow or the project graph changes.

## Before calling it done

- Clean Release build, zero warnings.
- Every suite you could run passes; name the ones you could not run and why.
- No dead code, no comment describing something that is no longer true.
- Documentation and implementation agree.

A commit message is a short, descriptive imperative title in the Linux-kernel style — the title
says the change directly, never through a Conventional Commits prefix (`feat:`, `fix:`, `chore:`…
are banned) — a blank line, then a body: the main changes and their motivation, whenever that
adds value. Less detailed than the pull request's description, but enough that someone reading
only the git history understands what was done and why. Squash-merged from a pull request. An
AI-assisted commit keeps its `Co-Authored-By` trailer — always — but never carries a Claude
session reference: no `Claude-Session` trailer, no session URL, not in the message and not in
anything committed. Check the message and the staged diff for one before every commit. If you see
a better design that no accepted record forbids, propose it before implementing it.

**A commit carries exactly one co-author, and it is Claude.** Never a second `Co-Authored-By`
trailer, whatever the reason offered for adding one. A co-authorship is a claim about who wrote the
code, and the only honest one here names the assistant that helped write it.

The trailer is not the only place a second name appears, and the other one is easy to introduce by
accident. **The author and the committer must be the same identity** — the repository's own, the one
the rest of the history uses. When the two differ GitHub prints both beside the co-author, which
reads as a co-authorship nobody agreed to. `git commit --amend` is where this happens: it inherits
the original author but takes the committer from `git config`, so a squashing amend has to set both
on purpose rather than let one of them drift:

```bash
GIT_COMMITTER_NAME="<the author's name>" GIT_COMMITTER_EMAIL="<the author's address>" \
  git commit --amend --no-edit --author="<the author's name> <the author's address>"
```

Verify it the way a reader will see it, before pushing: `git log -1 --format='%an <%ae>%n%cn <%ce>'`
prints the same line twice, or the commit is not ready.

**Everything written for Git or for GitHub is in English — the whole artefact, not its title.**
There is no part of one where another language is acceptable, and *the title was in English* does
not satisfy this rule. It covers, exhaustively:

- a commit's **subject line** and its **body**, trailers included;
- a pull request's **title**, the **headings** of its description, and **every word of the
  description itself** — prose, tables, bullet lists, quoted output, the comments inside a code
  block, the technical explanation, the motivation, the verification section, the *what is next*;
- anything else published beside one: an issue or pull-request **comment**, a **review** and its
  inline remarks, a **reply to a reviewer**, a branch name, a release note.

**The conversation is the exception, and the only one.** Answer the user in the language they wrote
to you in — that costs nothing and belongs to them. What separates the two is permanence and
audience: a chat is ephemeral and has one reader, while a description stays attached to the diff for
as long as the diff exists and is read by whoever arrives next. A repository whose prose changes
language according to who happened to ask for the change is one that has to be read twice. Translate
at the boundary rather than writing across it: think in whichever language the discussion is in, and
write the artefact in English.

**Pushing a `claude/*` branch means opening its pull request, without being asked.** This paragraph
is that request, made once and standing: work that is finished is work a reviewer can see, and a
branch sitting on the remote with no pull request is finished work nobody has been told about. So
the last step of the push is `gh pr create` — or the equivalent GitHub tool — against the default
branch, every time, with no confirmation sought.

Three things bound it, and they are what make a standing authorisation safe:

- **One pull request per branch.** If the branch already has an open one, the push updates it —
  including after a force-push — and the description is rewritten to describe what the branch now
  contains. Opening a second is how a review ends up split across two threads.
- **A merged pull request is finished.** Follow-up work restarts the branch from the default branch
  and gets a *new* pull request; it is never stacked onto merged history.
- **Opening is not following.** Do not subscribe to the pull request's activity, poll its checks or
  schedule a check-in unless asked to. Opening it hands the work over; watching it is a separate
  request.

Everything else about the artefact still applies: English throughout, the title and description
written to the conventions below, and a repository template filled in when there is one.

**A pull request Claude Code owns carries one commit, and stays that way.** After every push to a
`claude/*` branch, squash the branch back to a single commit against the base, force-push it, and
then *check* — `git log --oneline origin/master..HEAD` prints one line, or the job is not finished.
The message is rewritten to describe the whole change, never appended to: a squashed history whose
message still narrates the first attempt is worse than the commits it replaced. It obeys the same
conventions as any other — imperative title, no Conventional Commits prefix, blank line, body.

Three conditions, and none of them is optional:

- **Fetch first, and squash everything since the base — not "your" commits.** `ci.yml` commits the
  regenerated client to the pull request's branch with `GITHUB_TOKEN`, and that push triggers no
  workflow, so a squash computed from a stale local view deletes work nothing will redo. Fetch, then
  `git reset --soft $(git merge-base HEAD origin/master)`, so the bot's tree is *inside* the squash
  rather than under it.
- **`--force-with-lease`, never a bare `--force`** — and know what the bare form compares against.
  It checks the remote-tracking ref, which the fetch above has just moved, so on its own it stops
  protecting you at exactly the moment it matters. Name what you verified:
  `--force-with-lease=<branch>:<sha>`.
- **Only a branch Claude Code owns, and only before review.** Never rewrite a branch anyone else
  pushes to. Once a reviewer has commented, stop squashing: a force-push orphans inline comments
  anchored to commits that no longer exist and destroys the *changes since your last review* diff.
  From then on, add commits and squash once, at the end — GitHub's squash-merge would deliver the
  same single commit to `master` either way.
