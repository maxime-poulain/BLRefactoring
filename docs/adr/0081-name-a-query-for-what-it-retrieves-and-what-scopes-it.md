# 0081 — Name a query for what it retrieves and what scopes it

- **Status:** Accepted — amended by [0086](0086-say-current-when-the-caller-is-the-criterion.md): the command half gains the clause the caller-scoped commands were hiding — a message whose criterion is its caller says Current
- **Amends:** [0048](0048-qualify-a-contract-before-naming-what-it-is.md)
- **Date:** 2026-08-15

## Context

ADR 0048 settled which vocabulary a type belongs to: an API assembly's contracts are `*HttpRequest`
and `*HttpResponse`, an application assembly's inputs are `*Request` and its outputs `*Dto`, and the
CQRS stack keeps its bare `*Command` and `*Query`. That record said where a message lives. It said
nothing about what a message should be called, and on the query half it shows.

The commands read well already, because a command has nowhere to hide: `TransferTrainingCommand`,
`SuspendTrainerCommand`, `RemoveTrainerPhotoCommand`. A verb, the thing acted on, done. Nobody has
to open the class.

The queries do not, and they fail in three different ways.

- **A name that says who is asking rather than what is asked.** `GetMyTrainingsQuery` — *my* is the
  caller, not the criterion, and the caller is not a value the message carries. The scoping is real:
  the handler resolves the identity through `ICurrentUserService`. The name simply does not say so.
- **A name that says which screen it serves.** `GetAdministeredTrainingsQuery` and
  `GetAdministeredTrainersQuery` were named for the administration that reads them. What they
  actually do is narrow a listing by a status — `Withheld`, `Suspended` — with a term and a page.
  *Administered* is not a criterion; it is where the answer is displayed, and a name that follows a
  screen has to be renamed whenever a second screen asks the same question.
- **A name that says neither.** `GetOfferedPortraitQuery` and `GetTrainerPortraitQuery` carry two
  identifiers apiece and name neither, so which one identifies the bytes and which one authorizes
  the read is a question only the file answers.

None of that is an accident of care. It is the absence of a convention: a reader arriving at
`Features/Trainings/` sees a folder per use case and one class per folder, and has no way to
predict what the class is called or what it will carry. The command half has that predictability
and the query half does not.

**And nothing enforces the `*Query` suffix either.** Every rule in the suite that finds a message
finds it structurally — `typeof(IQuery).IsAssignableFrom(type)` — never by its name. A query called
`TrainingLookup` would dispatch, validate, log, page and answer exactly like its neighbors, and no
build would notice. That is the same situation `TheLayeredApplication_NamesItsServicesInFull` was
written to end for `*ApplicationService`, and it is fixed the same way.

## Decision

**A query is named for what it retrieves and for what scopes it.** Three clauses, and a query
satisfies all three:

1. **It starts with a retrieval verb** drawn from a small declared set — `Get`, `Search`,
   `Retrieve`, `List`, `Find` — the way a command starts with the verb of what it does.
2. **It says what is being retrieved**, in the domain's own words: a trainer profile, an offered
   training, a portrait.
3. **It ends with its criterion, as `ByX`, whenever it has one.**
   `GetTrainerProfileByTrainerIdQuery`, `GetTrainingsByStatusQuery`,
   `GetTrainingsByCurrentTrainerQuery`. A query that narrows by nothing takes no `By`, because there
   is nothing to name.

And it ends in `Query`, which until now was true of every one of them by habit rather than by rule.

**The measure of a name is that a reader need not open the file.** That is what the three clauses
are for, and it is also how the questions they leave open are settled. Two of them are settled here.

**`ById` only when the `Id` is the identifier of the thing the name has just retrieved.**
`GetTrainingByIdQuery` and `GetOfferedTrainingByIdQuery` fetch a training by the training's own
identifier, and spelling it `ByTrainingId` would repeat the noun standing beside it. A profile has
no identifier of its own — the value is a `TrainerId` — so `GetTrainerProfileByIdQuery` would name
an identifier that does not exist, and it sat beside `GetTrainerPhotoByTrainerIdQuery`, which is the
same shape spelled the other way. Two things belonging to a trainer, fetched by the trainer's
identifier, named differently: exactly the ambiguity the clauses above exist to remove. Hence
`GetTrainerProfileByTrainerIdQuery`.

**The criterion is named even when the message does not carry it.** `GetTrainingsByCurrentTrainerQuery`
declares nothing but its paging: the trainer is resolved in the handler through
`ICurrentUserService`, on purpose, because a parameter is something a call site could fill wrongly.
So this is the one query whose scoping appears nowhere in the type — which is the argument for
spelling it in the name rather than against it. Elsewhere a reader who opens
`GetTrainingsByStatusQuery` finds a `Status`; here, if the name does not say it, nothing does.
It is `CurrentTrainer` rather than `CurrentUser` because `ICurrentUserService` carries both and
distinguishes them deliberately — authentication knows about accounts, the domain knows about
trainers — and a training belongs to the trainer.

**Paging is not a criterion.** `PageRequest` says how much of an answer is wanted, not which
answer; `GetPoisonedMessagesQuery` carries one and is complete without a `By`.

**A query that fetches has a criterion; a query that searches is one.** That is the whole of the
`Search` exception, and it is a property of the verb rather than a pass granted to a class: the
catalog's search narrows by a term, a set of shelves and an order, all optional and none of them
*the* question — `SearchCatalogByTermAndTopicsAndOrderQuery` is where naming them all leads. So a
query opening with a verb that names its own narrowing is complete without a `ByX`, and today that
set holds exactly `Search`.

**Where a query carries two identifiers, `ByX` names the one that identifies what is returned**;
the other is an access condition. Both portrait queries carry a photo and a place to look from, and
the photo is what identifies the bytes — which is exactly why ADR 0063 makes those addresses
cacheable forever. So `GetOfferedPortraitByPhotoIdQuery` and `GetTrainerPortraitByPhotoIdQuery`,
with the training or the trainer remaining the path that authorizes the read.

**This governs CQRS messages.** A read *port* — `ITrainerAccountQuery`, `ICatalogDetailQuery`,
`ITrainingSearchQuery` — is not a message. It is a named question an outer layer asks an adapter,
and ADR 0028 and ADR 0055 chose those names deliberately: a port is named for the question it
answers, not for the shape of a call. Applying this record to them would rename a vocabulary two
other records own.

**With one carve-out, and it is about a word rather than about a layer.** Every use case exists in
both stacks, so `GetMyTrainingsQuery` had a twin: `ITrainingApplicationService.GetMineAsync`. Fixing
*my* on one side and leaving *mine* on the other would leave the two halves of a single use case
disagreeing about who the caller is — worse than either spelling held consistently. So the layered
method becomes `GetByCurrentTrainerAsync`, and the notion travels with the record that renamed it.

The rest of the layered read vocabulary is deliberately not touched, and the reason is a real
difference rather than a lack of appetite: **a method is read with its receiver, a message is read
alone.** `trainingApplicationService.GetByIdAsync(id)` already says which aggregate and which
identifier, because the interface standing to its left says the first half; a `GetByIdQuery`
arriving at a dispatcher has no such left-hand side. That is why clause 2 binds a message harder
than it binds a method, and why `GetAdministeredPageAsync` and `GetFacetsAsync` — which carry the
same defects this record names — are left for a decision of their own rather than swept in here.
The rule sees none of them: its population is `IQuery`.

**`Search` is a retrieval verb, and `SearchCatalogQuery` keeps its name.** Not an exemption granted
to avoid a rename: seeking along an inverted index is a different act from fetching by identifier,
the Search Indexing context publishes exactly that word (ADR 0059), and a name that said `Get` would
describe the wrong operation.

**Records merged before this one keep the names they were written with.** ADR 0062 names
`GetOfferedTrainingQueryHandlerTests` and `GetOfferedTrainingQueryValidatorTests` in its
verification section, because that is what those files were called on the day it was accepted. A
merged record is never rewritten — so the rule that defends this decision reads code and never
`docs/adr/`. `README.md` is not a record, and is updated.

## Consequences

- **Nine queries are renamed; four already conformed.** `GetTrainerByIdQuery`,
  `GetTrainingByIdQuery`, `GetPoisonedMessagesQuery` and `SearchCatalogQuery` are untouched. The
  rest move:

  | Was | Scoped by | Is |
  |---|---|---|
  | `GetCatalogTopicsQuery` | `Term` | `GetCatalogTopicsByTermQuery` |
  | `GetTrainerProfileQuery` | `TrainerId` | `GetTrainerProfileByTrainerIdQuery` |
  | `GetOfferedTrainingQuery` | `TrainingId` | `GetOfferedTrainingByIdQuery` |
  | `GetTrainerPhotoQuery` | `TrainerId` | `GetTrainerPhotoByTrainerIdQuery` |
  | `GetOfferedPortraitQuery` | `TrainingId` + `PhotoId` | `GetOfferedPortraitByPhotoIdQuery` |
  | `GetTrainerPortraitQuery` | `TrainerId` + `PhotoId` | `GetTrainerPortraitByPhotoIdQuery` |
  | `GetMyTrainingsQuery` | the caller's token | `GetTrainingsByCurrentTrainerQuery` |
  | `GetAdministeredTrainingsQuery` | `Status`, a term, a page | `GetTrainingsByStatusQuery` |
  | `GetAdministeredTrainersQuery` | `Status`, a term, a page | `GetTrainersByStatusQuery` |

- **The ninth rename was found by the rule, not by the survey that preceded it.** The list started
  at eight, with `GetCatalogTopicsQuery` set aside as a query that narrows by nothing. That was true
  when it was written and stopped being true at ADR 0080, which gave the facets a term so their
  counts would answer the search the visitor had typed. Nobody noticed, because until this record
  nothing was looking. It is the argument for the executable half in miniature: a convention kept by
  reading is a convention that decays at exactly the commits that are hardest to review.
- **One layered method moves with its twin.** `ITrainingApplicationService.GetMineAsync` becomes
  `GetByCurrentTrainerAsync`, interface and implementation alike, so the two stacks name one use
  case the same way. Its HTTP action keeps the name it had: `operationId` is `Controller_Action`,
  `OpenApiDocumentTest` pins `Training_GetMine`, and the generated client and the Blazor page are
  built from it — renaming the action would move the published document, which this record does not
  do. The controller's *call* changes; nothing a client can see does.
- **A handler and a validator move with the message they answer.**
  `EveryMessage_HasAHandlerNamedAfterIt` requires `{message}Handler` exactly, and
  `EveryQueryTakingAnIdentifier_HasAValidator` keeps the pair together, so neither is a separate
  decision.
- **Three use-case folders are renamed rather than added.** `Trainings/GetMine/` becomes
  `Trainings/GetByCurrentTrainer/`, and the two `GetAdministered/` folders become `GetByStatus/` under their
  own areas. Renames, so `EveryUseCase_HasItsOwnFolder` still finds one folder per use case and the
  count in `docs/strategic-design/` does not move.
- **Nothing on the HTTP surface moves.** Operation identifiers are built from controller and action
  names, so no schema, no route and no line of `Clients.Generated.cs` changes. The regeneration is
  run and the empty diff is the proof, the way ADR 0008 asks.
- **`CLAUDE.md` gains the convention.** A record a future session never reads is a record that
  decays; the `## CQRS` section states the three clauses, the port exclusion and the rule that
  enforces them.

## Alternatives considered

**Leave the names alone and write the convention for new queries only.** Cheapest, and it produces
the worst possible state: a codebase where the convention holds for whatever was added last. Half a
convention is not a weaker convention, it is a rule a reader cannot use to predict anything.
Rejected.

**`ByX` only where the criterion is ambiguous.** Tempting for `GetTrainerPhotoQuery`, where a photo
plainly belongs to a trainer and `ByTrainerId` looks like noise. Rejected because *ambiguous* is a
judgment, and a convention that turns on one is a convention with an argument in it every time. The
cost of always naming the criterion is a few longer names; the cost of naming it sometimes is that
its absence stops meaning anything.

**Name the query after the read model instead** — `TrainerProfileQuery`, `CatalogTopicsQuery` —
dropping the verb on the grounds that a query only ever retrieves. Rejected because it breaks the
symmetry with the command half, which is where the readability of this stack comes from: a folder
holding `SuspendTrainerCommand` beside `TrainerProfileQuery` reads as two different kinds of thing
written by two different people.

**Extend the convention to the read ports.** Considered and refused above: they are ports, they are
named for the question they answer by two earlier records, and renaming `ICatalogDetailQuery` to
`ICatalogDetailByIdQuery` would make a port describe a call signature. Their `Query` suffix is what
they are — a query surface — not a message name.

## Verification

`EveryQuery_IsNamedForWhatItRetrieves` takes its population by reflection over both CQRS assemblies,
selecting what implements `IQuery`, so a query added to a folder no glob covers is still judged. It
asserts the three clauses separately, each with its own sentence:

- the name begins with one of the declared retrieval verbs, held in a `private static readonly`
  array so widening the set is a visible edit rather than a regex nobody reads;
- the name ends in `Query`, which nothing checked before this record;
- a query declaring any property other than its `PageRequest` names a criterion — its name contains
  `By`.

Watched red three ways, restoring between each: `GetTrainerProfileByTrainerIdQuery` renamed to
`TrainerProfileByIdQuery` (no verb), to `GetTrainerProfileById` (no suffix), and
`GetTrainingsByStatusQuery` renamed to `GetAdministeredTrainingsQuery` (a criterion carried and not
named) — the last being the clause this record exists for, and the one that would have caught
`GetMyTrainingsQuery`.

The rename itself is behavior-preserving, so the proof that nothing but names moved is that every
other suite passes unchanged apart from its own renames.
