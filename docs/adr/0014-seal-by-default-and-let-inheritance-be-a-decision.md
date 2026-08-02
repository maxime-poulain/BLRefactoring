# 0014 — Seal by default, and let inheritance be a decision

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

A hundred and forty-seven classes in this repository were open, and not one of them was inherited by
anything. Not a single plain class in the solution had a subclass: every base type here is an
interface, a framework type, or one of the kernel's abstract classes.

That is not a repository that chose to be extensible. It is a repository where `sealed` was typed
when somebody happened to think of it. The evidence is the unevenness. `TrainerCreationRequest` is
sealed and `TrainingCreationRequest`, in the same folder, is not. `TrainerApplicationService` is
sealed and its twin `TrainingApplicationService` is not. `BLRefactoring.Shared.Domain`,
`BLRefactoring.Api.TestKit` and `BLRefactoring.Blazor.Bff.Tests` were already at a hundred per cent —
so the convention existed, it just had nothing holding it.

The cost of an open class is not primarily runtime. It is that a reader cannot tell a deliberate
extension point from a type that never got the keyword. When everything is open, `sealed` says
nothing and neither does its absence; the design intent that inheritance is *supposed* to carry
stops being legible. `ErrorCode` was the sharpest case: it advertised itself in its own doc comment
as "the base class that each error code should inherit from" and had been inherited by nothing, ever.

## Decision

**A class that nobody inherits is sealed.** `SealingRules.EveryClassNobodyInherits_IsSealed` fails
the build otherwise. Its companion, `EveryAbstractClass_IsActuallyInherited`, closes the other half:
an abstract class with no hierarchy under it is a base for something that was removed, or never
arrived.

The rule is written against reflection over **every assembly in the solution**, which is the part
worth recording. NetArchTest's `AreNotInheritedByAnyType` — a predicate the eNhanced fork adds and
the original does not have — would be the natural instrument, and it cannot be used. It sees only
the types loaded into the scan it runs in, so applied one assembly at a time it reports a domain
class as uninherited when its only subclass lives in infrastructure, and demands a `sealed` that
does not compile. "Nobody inherits this" is a claim about everything and can only be made by
something that sees everything.

That includes the test projects. A production class whose only subclass is a test double is
inherited, and sealing it would break the build. So `BLRefactoring.Architecture.Tests` references
every project in the solution — which, for a suite whose subject is the shape of the whole solution,
is the right shape to be in.

## Consequences

Sealing was verified as safe before it was applied, because it breaks at run time rather than at
compile time when it breaks at all. All thirty-three Moq usages mock interfaces, never a class. EF
Core's proxy package is absent, `UseLazyLoadingProxies` is never called, and there is not one
`virtual` member in the hand-written source. There is no Castle, NSubstitute or FakeItEasy, no
`Activator.CreateInstance`, and the Mediator source generator resolves handlers from DI and calls
them through their interfaces rather than deriving from them.

The three `Program` declarations are sealed too. Modifiers combine across partial parts, so sealing
the hand-written half seals the type, and `WebApplicationFactory<TEntryPoint>` uses the type as an
assembly marker rather than a base.

`ErrorCode` lost the extension point it advertised. Sealing it made its `protected` constructor a
CS0628 and a CA1047 — a new protected member in a sealed type — which is the compiler pointing out
that the promise had no reader. The constructor is now `private`, and ADR 0015 records what happened
to the type after that.

**The Blazor components are out of scope, and will stay out.** The Razor SDK generates each `.razor`
as a `partial class` deriving from `ComponentBase`, and a partial declaration adding `sealed` would
have to live in a code-behind file that does not otherwise exist. `Solution.Backend` does not list
the Blazor assemblies, so the rule never sees them; anyone widening that scope will need to exclude
them explicitly.

The maintenance cost is real and worth stating: the day a class genuinely needs a subclass, the
build goes red until the `sealed` comes off. That is the rule working — the point is that removing
it is a decision somebody makes, in a diff somebody reads.

## Alternatives considered

**Rely on CA1852.** The analyzer already runs at `warning` in `.editorconfig`, and it is why every
`internal` non-static class was already sealed. Rejected because it only considers internal types:
it is silent on the hundred and thirty-odd public ones, which is where the whole problem was.

**Seal only under `src/`.** Fifty-six classes instead of a hundred and forty-seven, and a much
smaller diff. Rejected because an exemption for tests would be permanent and unwritten, and because
two of the test projects already followed the convention without being asked. A rule that exempts
the tests is not a rule about the codebase; it is a rule about part of it.

**Use `AreNotInheritedByAnyType` per assembly.** The obvious reading of the library, and wrong for
the reason given above. Recorded here because the mistake is easy to make and produces a suite that
is confidently incorrect rather than merely silent.
