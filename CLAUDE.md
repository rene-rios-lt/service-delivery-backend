# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the backend repository for the Service Delivery system. It contains a .NET 10 Web API built with Clean Architecture and Azure infrastructure defined in Terraform.

## Commands

```bash
# Build
dotnet build

# Run the API
dotnet run --project src/ServiceDelivery.Api

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/ServiceDelivery.Domain.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~YourTestName"

# Terraform (from terraform/)
terraform init -backend-config=environments/dev/backend.tfvars
terraform plan -var-file=environments/dev/terraform.tfvars
terraform apply -var-file=environments/dev/terraform.tfvars
```

## Architecture

Clean Architecture with four layers. Dependency rule: inner layers never reference outer layers.

```
Domain → Application → Infrastructure
                    → Api
```

- **Domain** (`src/ServiceDelivery.Domain`) — pure business logic. Entities, value objects, domain events, repository interfaces. Zero external dependencies.
- **Application** (`src/ServiceDelivery.Application`) — use cases via CQRS (commands/queries). Defines `IRepository` and service interfaces consumed by Infrastructure. Depends only on Domain.
- **Infrastructure** (`src/ServiceDelivery.Infrastructure`) — EF Core DbContext, repository implementations, Azure service integrations. Depends on Domain + Application.
- **Api** (`src/ServiceDelivery.Api`) — ASP.NET Core host, controllers or minimal endpoints, middleware, DI wiring. Depends on Application + Infrastructure (for DI registration only).

## Key Conventions

- Features go in `src/ServiceDelivery.Application/Features/<FeatureName>/` with Commands and Queries as subfolders.
- Repository interfaces are defined in `Domain/Interfaces/`, implemented in `Infrastructure/Repositories/`.
- `appsettings.Local.json` is gitignored — use it for local secrets/overrides.

## Terraform

Infrastructure is split into reusable modules under `terraform/modules/` and environment-specific configs under `terraform/environments/`. Always target an environment's `terraform.tfvars` when running plan/apply.
