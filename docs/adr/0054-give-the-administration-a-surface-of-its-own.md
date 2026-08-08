# 0054 — Give the administration a surface of its own

- **Status:** Accepted
- **Date:** 2026-08-08

**This record was written because building ADR 0051's consequence found a decision that record did
not know it had left open.** ADR 0051 settled *who* the administrator is — an authority over the
same model, granted by a role, refused by a policy — and its consequence list named the two records
that would follow it: what an administrative decision does to a training (0052), and what a
suspended trainer may still do (0053). Neither is this one. Where the administration's endpoints
would *live* looked like a placement question with an obvious answer, and it is not: the obvious
answer does not work, and the reason it does not work is invisible until a request is made.

## Context

ADR 0052 gave the administration four decisions: suspend a trainer, reinstate one, withhold a
training, release it. They act on `Trainer` and `Training`, which already have controllers, so the
natural home is the controller that owns the aggregate:

```
POST /Trainer/{trainerId}/suspend
POST /Training/{trainingId}/withhold
```

**That is unreachable, and nothing says so.** Since ADR 0051, `ApiControllerBase` carries
`[Authorize(Policy = TrainerPolicy.Name)]`, and a policy declared on an action **is added to its
controller's, not chosen instead of it** — ASP.NET Core evaluates every `IAuthorizeData` that
applies to an endpoint and requires all of them. An action carrying `AdministratorPolicy` on a
controller carrying `TrainerPolicy` demands a caller who holds the `Administrator` role *and* whose
token carries a `trainer_id`. No such caller exists: the administrator is nobody's trainer, which is
the central claim of ADR 0051.

The failure mode is the reason this is worth a record rather than a comment. It compiles. It routes.
It publishes an ordinary operation into the OpenAPI document and the generated client. Every call
answers `403`, which is exactly what a caller who is not entitled would see — so the endpoint looks
like it is working correctly at the moment it is working for nobody.

## Decision

**The administrative endpoints live on a base of their own, under a route of their own, grouped by
the authority they exercise rather than by the aggregate they act on.**

- **`AdministrationControllerBase`**, a third shared base beside `ApiControllerBase` and
  `AuthControllerBase`. It carries `[Authorize(Policy = AdministratorPolicy.Name)]`, `[ApiController]`,
  the conventional route, and the `401`/`403` declarations — the same things `ApiControllerBase`
  carries and for the same reasons, with one policy exchanged for another. Abstract, so MVC does not
  discover it as a controller of its own.
- **One controller per host**, `AdministrationController`, publishing all four operations:

  ```
  POST /Administration/trainers/{trainerId}/suspend
  POST /Administration/trainers/{trainerId}/reinstate
  POST /Administration/trainings/{trainingId}/withhold
  POST /Administration/trainings/{trainingId}/release
  ```

  The aggregate is named in the route rather than in the controller, so an address still reads as an
  act on a trainer or on a training.
- **The administration gets a URL space, not a model.** No aggregate of its own, no vocabulary of
  its own, no application service of its own: the four actions drive `Trainer` and `Training`
  through the same application layer as everything else, and the layered host's controller injects
  the two services that already exist. That is ADR 0051's *authority, not a context* made concrete
  at the one layer where a context would have started to appear.
- **The reason travels as a `string` on `*HttpRequest`, and becomes a value object one layer in.**
  The route carries the identifier, the body carries the reason, and nothing carries the actor —
  who is calling is the policy's answer and no message repeats it.

## Consequences

- **A rule that reads the metadata, not the source.** `NoAction_IsBehindBothAuthoritiesAtOnce`
  collects every policy in force on every action — its own and every one inherited from its
  controller — and fails when the administrator's meets one that demands a trainer. Inheritance is
  the whole reason it reads metadata: the offending action would carry one attribute and inherit the
  other, and neither file would look wrong on its own.
- **"One of the two shared bases" becomes three**, in the rule that says so and in the one that
  holds them abstract. The second was renamed to read the suffix rather than a list of two names, so
  a fourth base is watched the day it is written rather than the day somebody remembers.
- **The generated client grows an `AdministrationClient`.** The operations are `Administration_*`
  on both hosts, which is what `BothHosts_PublishTheSameOperations` requires of them.
- **ADR 0051's verification is finally executable end to end.** That record promised a trainer's
  token refused `403` on an administrative endpoint, an anonymous caller `401`, and an
  administrator's token passing — none of which could be asserted while no such endpoint existed.
  All three are now facts on both hosts.
- **`/Administration` is a prefix a future screen, gateway or audit filter can key on**, which a
  suspension living at `/Trainer/{id}/suspend` would not have been. That is a consequence rather
  than a motivation: it is worth naming, and it did not decide anything here.

## Alternatives considered

**Move `TrainerPolicy` off `ApiControllerBase` and onto each of its actions.** This is the option
that keeps `POST /Trainer/{id}/suspend`, and it is the one to reject deliberately, because it undoes
a decision that was made a month ago for a good reason. ADR 0051 put the policy on the base
precisely so that *what is true of one endpoint is true of all of them* — the claim ADR 0011 makes
about every controller base here. Distributing it turns one guarantee into eighteen assertions, and
the day somebody adds an action and forgets the attribute, the trainer surface answers `500` on the
missing `trainer_id` rather than `403`. Trading a structural guarantee for a prettier URL is the
wrong side of that trade.

**A second base with no policy, and the policy on every administrative action.** Half the cost of
the above, and it still spends the guarantee: four actions is few enough to get right today and the
fifth is written by somebody who has not read this record.

**Two administrative controllers, one per aggregate.** The grouping this repository uses everywhere
else, and it collides with itself: `[controller]` would have to yield `Trainer` and `Training`,
which are the names of the existing controllers. Two controllers sharing a name in different
namespaces are legal in MVC and ambiguous everywhere it matters — `CreatedAtAction(controllerName:
"Trainer", …)`, which `AuthControllerBase` already calls, and NSwag's `operationId`, which is
`Controller_Action` and would file administrative operations into the trainer's client class.
Prefixing the class names to avoid the collision (`AdminTrainerController`) buys the aggregate
grouping back at the price of a route reading `/Admin/AdminTrainer`.

**A separate host, or a separate area.** The complete separation, and the one ADR 0051 spent its
whole argument refusing: an administration with a deployment of its own is an administration with a
model of its own soon after. Nothing here needs it — the four use cases are four calls on two
aggregates.

**No grouping at all: four actions spread across the existing controllers, each with the policy.**
The same defect as the first alternative, without even the URL to show for it.

## Verification

- **`NoAction_IsBehindBothAuthoritiesAtOnce`**, watched failing first: moving
  `SuspendTrainerAsync` onto the CQRS host's `TrainerController` with its policy on the action named
  that action, and the same move on the layered host named that one. Both were put back.
- **`EveryController_DerivesFromOneOfTheThreeSharedBases`** and **`EveryControllerBase_IsAbstract`**,
  which now count three rather than two — the second by reading the suffix, so it cannot be the rule
  that is out of date next time.
- **The endpoints themselves, on both hosts**, in `ModerationTest`: the four decisions, their
  refusals, and the claim only an end-to-end run can make — an administrator withholds a training
  and its owner is refused at both doors, then it is released and the owner publishes it freely.
- **Both directions of the policy**, in `AdministratorTest`: a trainer's token `403`, an anonymous
  caller `401`, and an administrator's token passing throughout `ModerationTest`. A requirement
  nobody satisfies refuses everybody, so the pass is as much of the proof as the two refusals.
