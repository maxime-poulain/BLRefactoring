# CLAUDE.md

This repository is a showcase project: it exists to demonstrate professional engineering practice,
not to maximise feature delivery. Architectural consistency, readability and long-term evolvability
outrank shipping speed. Understand the existing design before changing it.

## Read first, in this order

1. `README.md` — the architecture, the domain model, the conventions.
2. `docs/adr/README.md` — the index of 35 architecture decision records.
3. The records relevant to what you are touching.
4. `tests/TrainingHub.Architecture.Tests/Rules/` — the same decisions as 126 executable rules. Often
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
  the decision, and the domain names no service (ADR 0030).
- Each aggregate owns the error codes it raises, prefixed with its own name — `Trainer.PhotoTooLarge`
  (ADR 0015). `ErrorCodes.Validation` belongs to the FluentValidation pipeline alone (ADR 0016).

## CQRS

- A command answers a **bare `Result`** — never `Result<T>`. What changed is read back with a query:
  a write that hands back what it wrote is a read in disguise (`EveryCommand_AnswersWithABareResult`).
- A query never changes state, and answers a `*Dto` — never an aggregate, entity or value object.
- Command handlers live in the application layer, query handlers in infrastructure.
- One validator per command, beside it. One folder per use case.

## HTTP boundary

- The published contracts are `*RequestHttp` and `*ResponseHttp`, under `Shared.Api/Contracts/`. No
  controller names a command, a query or an application DTO, and no inner layer names a contract.
- Application-layer read models carry the `Dto` suffix.
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
- File-scoped namespaces, `var`, Allman braces, and a hundred and sixty analyzer severities, all
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

Commits are imperative one-liners, squash-merged from a pull request. An AI-assisted commit keeps
its `Co-Authored-By` trailer — always — but never carries a Claude session reference: no
`Claude-Session` trailer, no session URL, not in the message and not in anything committed. Check
the message and the staged diff for one before every commit. If you see a better design that no
accepted record forbids, propose it before implementing it.
