# 0042 — Close the boundary's vocabulary

- **Status:** Accepted
- **Date:** 2026-08-06

## Context

The README states the convention without qualification: *"The published contracts are
`*RequestHttp` and `*ResponseHttp`, under `Shared.Api/Contracts/`. No controller names a command, a
query or an application DTO, and no inner layer names a contract."* `HttpBoundaryRules` holds one
half of it — `EveryHttpContract_LivesInAContractsNamespace` — and the rule reads:

```csharp
.Where(type => type.Name.EndsWith("RequestHttp", StringComparison.Ordinal)
               || type.Name.EndsWith("ResponseHttp", StringComparison.Ordinal))
```

The population is *types already named as contracts*. A type that is a contract and is not named
like one is not in it. The convention is therefore checked in the direction that cannot be violated,
and unchecked in the direction that can.

Three types have been sitting in that gap since before the suite existed. `RegisterRequest`,
`LoginRequest` and `LoginResponse` are declared at the bottom of
`src/TrainingHub.Shared.Api/Controllers/AuthControllerBase.cs`, outside `Contracts/`, without the
suffix. They are not internal helpers: they are what `POST /Auth/register` and `POST /Auth/login`
bind and answer, they appear in `Clients.Generated.cs`, and the Blazor front consumes them through
`TrainingHub.GeneratedClients`. They are as published as any contract in this repository, and they
escaped the rule precisely by breaking the convention it defends.

That is the failure ADR 0038 named for numbers and ADR 0041 named for lists, in a third form: a
guard whose population is defined by the thing it is meant to detect.

## Decision

**The boundary's vocabulary is closed: every type an action binds or answers is a contract — named
with its suffix, declared under `Contracts/`.**

- **Both directions are held.** The existing rule keeps asking that a type named as a contract live
  in `Contracts/`. `EveryTypeOnTheBoundary_IsAContract` asks the converse, and it takes its
  population from the actions themselves — the parameter types a controller method binds and the
  type it answers — so a type joins the population by being on the boundary, not by being named as
  though it were.
- **The three auth types move.** They become `RegisterRequestHttp`, `LoginRequestHttp` and
  `LoginResponseHttp`, under `Contracts/Auth/`, one file each, like every other contract.
- **This is a breaking change, and it is taken deliberately.** The wire shape is unchanged — the
  JSON a client sends and receives is identical — but the generated client's type names move with
  the schema names. `./scripts/generate-clients.sh` regenerates them and the front follows. A
  showcase repository that keeps a convention in its README and three exceptions in its code is
  worse off than one that renames three types once.

**What the boundary's own types are, and are not.** The rule looks at what an action binds and
answers, which deliberately excludes what a contract is *made of*: `PaginationRequestHttp` and
`PagedResponseHttp<T>` are contracts by the same convention and are named so already, while the
`*Mappings` static classes under `Contracts/` translate contracts and are not themselves bound by
anything. `IFormFile` and the framework's own types are not ours to rename, and are excluded by
being declared outside the solution.

## Consequences

- Three types are renamed and moved; seventeen files name them, and the generated client changes.
  That is the cost, paid once, of the convention meaning what it says.
- The auth contracts gain what living under `Contracts/` implies: a file of their own, XML
  documentation as published contracts, and — the part ADR 0043 depends on — a natural home for the
  data annotations that declare their shape. `RegisterRequest` bounded nothing, which is why the
  CQRS validator's `NotEmpty()` was the only gate on the registration path.
- A new endpoint cannot quietly introduce a fourth type beside its controller. The rule fails on it
  the first time it is bound.
- `BothHosts_PublishTheSameOperations` and the OpenAPI document are unaffected: no operation, route,
  status or wire field changes.

## Alternatives considered

**Leave them and record the exception.** Cheapest, and this repository does record deliberate
exceptions — `UnguardedRecords` exists for exactly that. Rejected because these three are not an
exception to the convention, they are unreviewed history: nothing decided that auth contracts live
beside their controller, and an exemption would freeze an accident into a decision.

**Rename without moving.** `RegisterRequestHttp` in `AuthControllerBase.cs` would satisfy the
existing rule immediately. Rejected: it satisfies the rule by satisfying its population filter,
which is the defect this record is about, and it leaves a published contract in a file about
request handling.

**Widen the existing rule instead of adding one.** The two directions could share a method.
Rejected: they fail for different reasons and should say so differently — one says "you named it a
contract, put it on the boundary", the other says "you put it on the boundary, name it a contract"
— and a rule whose message has to cover both explains neither.

## Verification

`EveryTypeOnTheBoundary_IsAContract` reflects over every controller action in both hosts and the
shared base, collects the types they bind and answer, and was red first on
`RegisterRequest`, `LoginRequest` and `LoginResponse`. It was then broken on purpose and watched to
fail: a contract renamed out of its suffix, and one moved out of `Contracts/`.
