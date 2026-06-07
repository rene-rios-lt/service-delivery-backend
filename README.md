# Service Delivery — Backend

.NET 10 API and Azure infrastructure for the Service Delivery system.

## Structure

```
src/
  ServiceDelivery.Domain/         # Entities, value objects, domain events, interfaces
  ServiceDelivery.Application/    # Use cases, CQRS commands/queries, behaviors
  ServiceDelivery.Infrastructure/ # EF Core, repositories, external services
  ServiceDelivery.Api/            # ASP.NET Core Web API, middleware, endpoints
tests/
  ServiceDelivery.Domain.Tests/        # Entity invariants, value object logic — no mocks
  ServiceDelivery.Application.Tests/   # Command/query handlers — mocked interfaces
  ServiceDelivery.Infrastructure.Tests/ # Repository implementations against real DB
  ServiceDelivery.Api.Tests/           # HTTP contracts via WebApplicationFactory
  ServiceDelivery.Architecture.Tests/  # Layer dependency rules via NetArchTest.Rules
terraform/
  environments/                   # Per-environment tfvars (dev, staging, prod)
  modules/                        # Reusable Terraform modules
```

## Implementing Stories

Stories are implemented using the Master agent in the central repo. Invoke it with a backend story ID:

```
/master BE-001
```

The agent runs the full TDD pipeline (evaluate → plan → implement → AI review → review → PR) with two human checkpoints. See [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) for the full agent system documentation.

## Getting Started

See the [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) repo for scripts to run the full system locally.

## Infrastructure

Azure infrastructure is managed via Terraform in `terraform/`. Each environment has its own `terraform.tfvars` under `terraform/environments/<env>/`.
