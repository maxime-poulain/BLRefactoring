# 0013 — Make every record answer to a test

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

Twelve records describe this architecture, and until now every one of them was a paragraph asking to
be believed. That is not a complaint about the records — it is what a record is. The problem is what
happens next.

ADR 0009 claimed the cookie could not outlive the token it carried. The line that would have made it
true was never written, so the session renewed itself at half life and the claim was false for as
long as the record existed. Nobody noticed, because nothing could. ADR 0006 kept a paragraph about a
package that had already been removed. ADR 0010 counted nine actions publishing an ETag when there
were ten. The audit that found these found them by reading, which is the only instrument the
repository had, and reading does not scale past the attention of whoever happens to be doing it.

The same decay affects the conventions that never became records. The domain knows nothing of
persistence — until a `using` added at four in the afternoon to fix something unrelated. Both hosts
publish the same operations — until one of them is renamed and the other is not, which happened, and
which was caught by a person rather than by a build. A shared test base runs on both hosts — until
one is wired into a single suite, which breaks nothing, passes, and proves half of what it claims.

What all of these have in common is that the failure is silent and the diff looks reasonable.

## Decision

Every architectural decision still in force is defended by a test, or carries a written reason why
it cannot be.

The suite is `tests/TrainingHub.Architecture.Tests`. It states around fifty rules, drawn from the
records and from the README's own claims, over four mechanisms: dependency predicates on compiled
assemblies through NetArchTest, reflection over types and members, MSBuild over the project files,
and a scan of the source tree. It runs in the fast CI job with every other unit test.

Three things make it a suite rather than a collection of assertions.

**Each rule names the record it defends, and quotes it.** An `[ArchitectureRule]` attribute carries
the record number and the decision in the record's own words. A failure then reads *ADR 0012 says:
the kernel knows Mediator, and that is a recorded trade covering exactly eight types* — and prints
the offending types, why the dependency search failed, and the file each is declared in. The number
and the sentence are written once, at the rule, which is the only place that cannot drift from them.

**A record defended by nothing fails the build.** A coverage rule reads `docs/adr/` on one side and
this assembly's attributes on the other. A new record with no rule turns CI red, and stays red until
someone either writes the rule or writes down why there cannot be one. Those exemptions live in
`UnguardedRecords`, in this project rather than in the records, because this repository does not
rewrite a merged record — and because an exemption kept on this side can itself be checked. It is:
an entry naming a record that does not exist fails, and so does an entry for a record some rule has
since started defending. Records marked `Superseded` or `Proposed` need no entry; the rule reads
their own status line.

**Every rule proves it had something to look at.** Before asserting anything, a rule asserts that it
selected at least one type, file or member. This is the assertion that matters most over a decade.
An architecture suite almost never rots by a rule becoming wrong; it rots by a rule matching nothing
— a namespace renamed, a folder moved, a suffix dropped — after which the predicate is applied to
the empty set, satisfies every condition ever written, and passes forever. The same reasoning makes
`SourceTree` refuse a repository root that holds no solution file: a wrong root does not fail a file
scan, it empties one.

## Consequences

Three records are exempt today, each for the same underlying reason: what makes them true is
behaviour, not shape. ADR 0003 turns on a runtime branch over `IHostEnvironment`; ADR 0005 is about
a value surviving a round trip to SQL Server; ADR 0009 is about what does and does not travel over
the wire. All three are already asserted by tests that run a host, and the ledger says so and points
at them.

Some rules had to be written against the code as it is rather than as the prose describes it, and
that is a feature of the exercise rather than a compromise in it. The README says the domain does
not depend on the messaging library. Written the obvious way, that rule fails: every domain event
implements `IDomainEvent`, the kernel declares `IDomainEvent` as an `INotification`, and a dependency
search resolves interface hierarchies. The transitive reading is not wrong — the domain does reach
Mediator through the kernel — it is simply not the decision anyone took, which is that the coupling
stops at the kernel and covers eight named types. Only the interfaces a type declares itself can
tell those two apart. Writing the rule is what made the distinction explicit; the prose had been
eliding it.

Three deviations were corrected so that a rule could be stated without an exception list, in the
commit that precedes the suite: two API types stopped injecting a domain repository to read one
field, three value objects moved from a public `init` to a private one, and one event handler was
sealed like its five siblings. A rule with an undocumented exception is not a rule, and an exception
list is where the exception goes to become permanent.

The cost is maintenance, and it is real. A rule that becomes wrong is a red build for something
nobody broke, and the honest answer when that happens is often to fix the rule — as one already was,
in the commit that introduced it.

## Alternatives considered

**Leave the records as prose and rely on review.** What the repository did until now. Review caught
the operation-identifier divergence and the missing `SlidingExpiration`, eventually, in an audit that
read everything at once. Rejected because that is not a mechanism, it is an occasion, and the gap
between occasions is where the drift lives.

**Assert the conventions in the existing test suites.** Cheaper, and it is how `TopicTests` already
checks that no topic is missing from the set. Rejected for the rules that span layers: no existing
project references both API hosts, and the operation-parity test in the shared kit says in its own
comment that it can only see the host it runs against. That comment is the argument for a project
that sees all of them.

**Use ArchUnitNET instead of NetArchTest.** More expressive, actively maintained, and it can express
member-level predicates the original NetArchTest cannot. Rejected because its released line is still
`0.x` with a `2.x` branch in draft, and because it brings a second assertion vocabulary alongside the
one ADR 0007 mandates everywhere. `NetArchTest.eNhancedEdition` was taken over `NetArchTest.Rules`
for the opposite reason: the original has shipped nothing since May 2021 and carries a Mono.Cecil
that predates every metadata format change since, which is not a thing to point at .NET 10
assemblies and hope.

**One test class per record.** Rejected. The mapping is many-to-many in both directions: ADR 0006
implies rules about package references, about response declarations and about operation identifiers,
which belong in three different places; and roughly a third of the rules defend README prose that is
no record at all. Filing by record puts unrelated mechanisms in one file and leaves a third of the
suite in an `OtherRules.cs`, which is where rules go to die. Classes are organised by theme, and
traceability is carried by the attribute — which is why the attribute exists.

**Record the exemptions in the records themselves.** Rejected on this repository's own convention: a
merged record is not rewritten. It also puts the exemption out of reach of the check that keeps it
honest.
