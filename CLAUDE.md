# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the backend repository for the Service Delivery system. It contains a .NET 10 Web API built with Clean Architecture and Azure infrastructure defined in Terraform.

## System Context

This API powers a fleet dispatch system — "Uber for service reps." When a requester reports a fault on their equipment (identified by a Diagnostic Trouble Code), the system finds the nearest qualified service vehicle carrying the right equipment and dispatches the rep. Dispatchers manage the fleet and handle priority escalations. Real-time updates flow over SignalR.

Three personas consume this API: **Dispatcher** (fleet management, override authority), **ServiceRep** (job offers, state transitions, mark complete), and **Requester** (submit requests, track assigned rep). A **Simulator** service account drives vehicle positions for the POC.

## Required Reading Before Implementing

Read these docs before writing any code in this repo. They are the authoritative specification — do not re-derive business logic from scratch or make assumptions that contradict them.

- [`docs/domain-model.md`](docs/domain-model.md) — all entities, fields, relationships, and complete seed data (DTCs, vehicles, users, DTC coverage distribution)
- [`docs/business-rules.md`](docs/business-rules.md) — matching algorithm, priority/redirect rules, cooldown, state machines, ETA calculation
- [`docs/api-design.md`](docs/api-design.md) — REST endpoint groups by role, all 4 SignalR hubs with event payloads

Cross-cutting architecture decisions (why SignalR, why Haversine, why simulated auth) are in the central repo at `docs/adr/`.

## Implementing Stories

Stories for this repo (`BE-001` through `BE-025`) are implemented using the Master agent in `service-delivery-central`. Invoke it with the story ID:

```
/master BE-010
```

The agent creates a feature branch, runs the full TDD pipeline (evaluate → plan → implement → AI review → review → PR), and pauses at two human checkpoints. Never implement a story by writing code directly without the agent — TDD discipline and SOLID checks are enforced through that pipeline.

### Audit Files (`.stories/`)

During story execution the agent writes ephemeral working files to `.stories/<STORY-ID>/` in this repo. These files are gitignored and deleted at the start of each new run — they are session-scoped working memory for the pipeline, not source files. Do not create or commit anything under `.stories/`.

## Commands

```bash
# Build all projects
dotnet build

# Build a single project
dotnet build src/ServiceDelivery.Api

# Run the API
dotnet run --project src/ServiceDelivery.Api

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/ServiceDelivery.Domain.Tests
dotnet test tests/ServiceDelivery.Application.Tests
dotnet test tests/ServiceDelivery.Infrastructure.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~YourTestName"

# Terraform (run from terraform/ directory)
terraform init -backend-config=environments/dev/backend.tfvars
terraform plan -var-file=environments/dev/terraform.tfvars
terraform apply -var-file=environments/dev/terraform.tfvars
```

## Architecture

Clean Architecture with four layers. The dependency rule is strict: inner layers never reference outer layers.

```
Domain  ←  Application  ←  Infrastructure
                        ←  Api (composition root only)
```

- **Domain** (`src/ServiceDelivery.Domain`) — `net10.0` class library. Pure business logic: entities (`Entities/`), value objects (`ValueObjects/`), domain events (`Events/`), and repository interfaces (`Interfaces/`). Zero external dependencies — this project references nothing.
- **Application** (`src/ServiceDelivery.Application`) — `net10.0` class library. Use cases implemented via CQRS: commands and queries in `Features/<FeatureName>/Commands/` and `Features/<FeatureName>/Queries/`. Pipeline behaviors in `Common/Behaviors/`. Application-level service interfaces in `Common/Interfaces/`. References Domain only.
- **Infrastructure** (`src/ServiceDelivery.Infrastructure`) — `net10.0` class library. EF Core DbContext in `Persistence/`, repository implementations in `Repositories/`, external Azure service integrations in `Services/`. References Domain and Application.
- **Api** (`src/ServiceDelivery.Api`) — `net10.0` ASP.NET Core Web API. Controllers or minimal endpoints in `Controllers/`, custom middleware in `Middleware/`. This is the composition root — it wires DI and references Application and Infrastructure. Business logic never lives here.

## Project References (enforced by .csproj)

```
Domain          → no references
Application     → Domain only
Infrastructure  → Domain, Application
Api             → Application, Infrastructure
Domain.Tests    → Domain
Application.Tests → Application, Domain
Infrastructure.Tests → Infrastructure
```

Any code that would require violating this graph belongs in a different layer.

## Test-Driven Development

TDD is mandatory in this repo. No production code is written without a failing test first.

### The Cycle

```
Red   → Write a failing test that describes the behaviour you want
Green → Write the minimum production code to make it pass
Refactor → Clean up without breaking the tests
```

Never write production code speculatively. If there is no failing test, there is no code to write.

### Test Projects and What They Cover

| Project | What to test | Tools |
|---------|-------------|-------|
| `Domain.Tests` | Entity invariants, value object equality, domain event creation — pure logic, no mocks | xUnit |
| `Application.Tests` | Command/query handler behaviour — mock repository and service interfaces | xUnit, Moq |
| `Infrastructure.Tests` | Repository implementations against a real (in-memory or test container) DB | xUnit, EF Core InMemory |
| `Architecture.Tests` | Layer dependency rules — Domain/Application/Infrastructure/Api cannot reference outer layers | NetArchTest.Rules |
| `Api.Tests` | HTTP contracts — status codes, response shapes, error handling | xUnit, WebApplicationFactory |

### Test Naming

Use the `GivenA_When_Then` convention for all test method names:

```csharp
public void GivenANewServiceRequest_WhenTitleIsEmpty_ThenDomainExceptionIsThrown()
public void GivenAValidCredential_WhenLoginCalled_ThenJwtIsReturned()
public void GivenARepOnSite_WhenRedirectAttempted_ThenReturns422()
```

- `GivenA` — the starting state or precondition
- `When` — the action or event under test
- `Then` — the expected outcome

### Test Structure — Arrange / Act / Assert

Every test must have clearly separated sections:

```csharp
[Fact]
public void GivenAServiceRequest_WhenStatusIsUpdated_ThenDomainEventIsRaised()
{
    // Arrange
    var request = ServiceRequest.Create("Fix printer", RequestPriority.High);

    // Act
    request.MarkAsInProgress();

    // Assert
    Assert.Contains(request.DomainEvents, e => e is ServiceRequestStatusChangedEvent);
}
```

### Layer-Specific TDD Rules

- **Domain** — Write tests for every invariant and business rule before implementing it. Domain tests require no mocks — entities and value objects are pure C#.
- **Application** — Write the handler test first using mocked interfaces. The test defines the contract the handler must fulfil.
- **Infrastructure** — Write tests against the real data store (use EF Core InMemory for unit speed, TestContainers for integration accuracy). Never mock `DbContext`.
- **Api** — Write the endpoint test with `WebApplicationFactory` before wiring the route. The test defines the HTTP contract.

### What Not to Test

- Framework behaviour (EF Core, ASP.NET routing) — test your code, not the framework
- Trivial property getters/setters with no logic
- Private methods — test through the public interface

## SOLID Principles

All additions and modifications to this repo must follow these principles, mapped directly to the Clean Architecture layers.

### S — Single Responsibility
Each layer has exactly one job:
- **Domain** = business rules and invariants
- **Application** = orchestrating use cases
- **Infrastructure** = talking to external systems (DB, Azure, APIs)
- **Api** = HTTP concerns and DI wiring only

Each class should have one reason to change. A command handler that also sends emails violates SRP — the email concern belongs in an application service interface (`Common/Interfaces/`) with the implementation in Infrastructure.

### O — Open/Closed
- Add new features by creating new files under `Application/Features/<FeatureName>/` — never by modifying existing unrelated feature handlers.
- Extend infrastructure behavior through new implementations of existing interfaces, not by modifying those interfaces.
- New API surface = new endpoint or controller, not additions to an existing unrelated one.

### L — Liskov Substitution
- Repository implementations in Infrastructure must fully honour the contracts defined in Domain interfaces — no partial implementations or exceptions thrown for "unsupported" operations.
- If a repository only needs a subset of operations, define a narrower interface in Domain rather than implementing a broad one partially.

### I — Interface Segregation
- Repository interfaces in `Domain/Interfaces/` should be focused per aggregate (e.g. `ITicketRepository`, not a single `IRepository<T>` for everything).
- Application service interfaces in `Application/Common/Interfaces/` should be narrow — one interface per external capability (e.g. `IEmailService`, `IStorageService`).
- Command handlers and query handlers should not depend on interfaces they don't use.

### D — Dependency Inversion
- Application depends on domain interfaces, never on Infrastructure implementations.
- Infrastructure implements those interfaces — it is never referenced by Application or Domain for business logic.
- Api references Infrastructure only to register implementations in the DI container (`Program.cs`). All business logic is invoked through Application layer abstractions.

## Key Conventions

- New features → `Application/Features/<FeatureName>/Commands/` and `Application/Features/<FeatureName>/Queries/`
- Repository interfaces → `Domain/Interfaces/`
- Repository implementations → `Infrastructure/Repositories/`
- Application service interfaces → `Application/Common/Interfaces/`
- External service implementations → `Infrastructure/Services/`
- Pipeline behaviors (validation, logging, etc.) → `Application/Common/Behaviors/`
- `appsettings.Local.json` is gitignored — use it for local secrets and connection strings

## Terraform

Infrastructure is split into reusable modules under `terraform/modules/` (app-service, database, networking) and environment-specific configs under `terraform/environments/` (dev, staging, prod). Always target an environment's `terraform.tfvars` when running plan or apply.
