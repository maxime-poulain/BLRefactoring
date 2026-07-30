# BLRefactoring

A .NET 10 showcase project illustrating modern software architecture patterns applied to a **Training Management** domain. The solution implements the same business logic using two distinct approaches — **DDD** and **DDD + CQRS** — side by side, making it an ideal reference for comparing architectural trade-offs.

A **Blazor WebAssembly** frontend backed by **MudBlazor** provides a functional UI on top of the APIs.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Solution Structure](#solution-structure)
- [Domain Model](#domain-model)
- [Architectural Patterns](#architectural-patterns)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                    Blazor WASM Client                    │
│                  (MudBlazor · JWT Auth)                  │
└──────────────────────┬───────────────────────────────────┘
                       │ HTTP / REST
        ┌──────────────┴──────────────┐
        ▼                             ▼
┌───────────────┐            ┌────────────────┐
│   DDD API     │            │ DDD+CQRS API   │
│  (Controllers)│            │ (Controllers)  │
└───────┬───────┘            └───────┬────────┘
        │                            │
        ▼                            ▼
┌───────────────┐            ┌────────────────┐
│  Application  │            │  Application   │
│  (Services)   │            │ (Commands /    │
│               │            │  Queries /     │
│               │            │  Handlers)     │
└───────┬───────┘            └───────┬────────┘
        │                            │
        └──────────┬─────────────────┘
                   ▼
        ┌─────────────────┐
        │  Shared Domain   │
        │ (Aggregates,     │
        │  Value Objects,  │
        │  Domain Events)  │
        └─────────────────┘
                   │
                   ▼
        ┌─────────────────┐
        │  Shared Infra    │
        │ (EF Core, Repos, │
        │  Identity, JWT)  │
        └────────┬────────┘
                 │
                 ▼
          ┌────────────┐
          │ SQL Server  │
          └────────────┘
```

Both API stacks share the **same domain model** and **same infrastructure layer**, differing only in how the application layer orchestrates use cases.

---

## Solution Structure

```
src/
├── BLRefactoring.Shared/              # Base building blocks (Entity, ValueObject, AggregateRoot, Result)
├── BLRefactoring.Shared.Domain/       # Domain model (Trainer & Training aggregates)
├── BLRefactoring.Shared.Application/  # Shared DTOs, mappers, cross-aggregate event handlers
├── BLRefactoring.Shared.Infrastructure/ # EF Core DbContext, repositories, Identity, JWT, interceptors
│
├── DDD/
│   ├── Domain/                        # (empty — uses Shared.Domain)
│   ├── Application/                   # Application services (classic service layer)
│   ├── Infrastructure/                # (empty — uses Shared.Infrastructure)
│   └── Api/                           # ASP.NET Core API with controllers
│
├── DDDWithCqrs/
│   ├── Domain/                        # (empty — uses Shared.Domain)
│   ├── Application/                   # Commands, Queries, Handlers, Validators, Pipeline Behaviors
│   ├── Infrastructure/                # (empty — uses Shared.Infrastructure)
│   └── Api/                           # ASP.NET Core API with controllers + middleware
│
├── Web/BLRefactoring.Blazor/          # Blazor Server host
├── Web/BLRefactoring.Blazor.Client/   # Blazor WASM client (MudBlazor UI)
└── BLRefactoring.GeneratedClients/    # NSwag-generated API clients

tests/
├── BLRefactoring.Shared.Domain.Tests/       # Domain unit tests (aggregates, value objects, Result)
└── BLRefactoring.DDD.Application.Tests/     # Application service unit tests
```

---

## Domain Model

### Trainer Aggregate

| Concept | Type | Description |
|---|---|---|
| `Trainer` | Aggregate Root | A trainer who can create and manage trainings |
| `TrainerId` | Typed ID | Strongly-typed identifier |
| `Email` | Value Object | Validated email address (local part + domain) |
| `Name` | Value Object | First name / last name |
| `Bio` | Value Object | Trainer biography |

**Domain Events:** `TrainerCreated`, `TrainerDeleted`, `TrainerEmailChanged`, `TrainerNameChanged`

### Training Aggregate

| Concept | Type | Description |
|---|---|---|
| `Training` | Aggregate Root | A training course owned by a trainer |
| `TrainingId` | Typed ID | Strongly-typed identifier |
| `TrainingTitle` | Value Object | Unique title per trainer (enforced via `IUniquenessTitleChecker`) |
| `TrainingDescription` | Value Object | Training description |
| `TrainingPrerequisites` | Value Object | Prerequisites list |
| `AcquiredSkills` | Value Object | Skills gained upon completion |
| `Topic` | Value Object (Smart Enum) | Predefined topics (Programming, Design, Marketing, etc.) |

**Domain Events:** `TrainingCreated`

**Cross-aggregate rule:** When a Trainer is deleted, all their Trainings are automatically deleted via a domain event handler.

---

## Architectural Patterns

### Domain-Driven Design (DDD)

- **Aggregates** with encapsulated business rules and factory methods
- **Value Objects** with structural equality (`ValueObject` base class)
- **Strongly-typed IDs** (`EntityId<T>`) to prevent primitive obsession
- **Domain Events** raised by aggregates, dispatched within `SaveChanges` (right before persistence) via EF Core interceptor, so handlers' changes are committed in the same transaction
- **Repository pattern** with interfaces defined in the domain layer — repositories only stage changes in the change tracker
- **Unit of Work** (`IUnitOfWork`) — a single `SaveChangesAsync()` per use case, called by the orchestrating command handler or application service, persists everything atomically

### CQRS (Command Query Responsibility Segregation)

The `DDDWithCqrs` stack separates reads from writes:

- **Commands** (`ICommand<TResult>`) — `CreateTrainerCommand`, `DeleteTrainerCommand`, `CreateTrainingCommand`, `DeleteTrainingCommand`
- **Queries** (`IQuery<TResult>`) — `GetAllTrainersQuery`, `GetTrainerByIdQuery`, `GetAllTrainingsQuery`, `GetByIdQuery`
- **Mediator** (source-generated) for dispatching with pipeline behaviors:
  - `ValidationPipelineBehavior` — FluentValidation integration
  - `NoTrackingDuringQueryExecutionBehavior` — disables EF Core change tracking on reads

### Railway-Oriented Programming (Result Pattern)

- `Result` and `Result<T>` types for explicit error handling without exceptions
- Composable via `Bind()`, `Match()`, `MatchAsync()`, `Switch()`
- `ErrorCollection` for aggregating multiple validation errors

### Hexagonal Architecture (Ports & Adapters)

- **Domain** has zero external dependencies
- **Application** depends only on domain interfaces
- **Infrastructure** implements ports (repositories, services)
- **API** is a thin composition root

### Event-Driven Architecture

- Domain events accumulated by aggregates → collected and dispatched in batch by `DomainEventInterceptor` right before persistence (`IDomainEventDispatcher`)
- Event handlers run within the ambient `SaveChanges`: their changes are persisted in the same transaction as the original use case (e.g., cascade delete of a trainer's trainings)

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Runtime** | .NET 10 / .NET 9 (Blazor) |
| **API** | ASP.NET Core, Controllers |
| **ORM** | Entity Framework Core 10 (SQL Server) |
| **Auth** | ASP.NET Core Identity + JWT Bearer |
| **CQRS** | Mediator (source-generated) |
| **Validation** | FluentValidation |
| **UI** | Blazor WebAssembly + MudBlazor |
| **Client storage** | Blazored.LocalStorage |
| **API docs** | Swagger / Scalar |
| **API client gen** | NSwag |
| **Smart Enums** | Ardalis.SmartEnum |
| **Testing** | xUnit, FluentAssertions, Moq |
| **Database** | SQL Server 2022 (Docker) |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started)

### 1. Start SQL Server

```bash
docker compose up -d
```

This starts a SQL Server 2022 instance on port `1433`.

### 2. Run the DDD API

```bash
dotnet run --project src/DDD/Api/BLRefactoring.DDD.Api.csproj
```

### 3. Run the CQRS API

```bash
dotnet run --project src/DDDWithCqrs/Api/BLRefactoring.DDDWithCqrs.Api.csproj
```

### 4. Run the Blazor frontend

```bash
dotnet run --project src/Web/BLRefactoring.Blazor/BLRefactoring.Blazor/BLRefactoring.Blazor.csproj
```

> Database migrations are applied automatically on API startup.

---

## Running Tests

```bash
dotnet test
```

Tests cover:
- **Domain layer** — aggregate behavior, value object validation, Result pattern, entity equality
- **Application layer** — service orchestration, DTO mapping, event handlers
- Uses **Builder pattern** for test data (`TrainerBuilder`, `TrainingBuilder`)
