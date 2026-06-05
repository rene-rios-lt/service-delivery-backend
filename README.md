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
  ServiceDelivery.Domain.Tests/
  ServiceDelivery.Application.Tests/
  ServiceDelivery.Infrastructure.Tests/
terraform/
  environments/                   # Per-environment tfvars (dev, staging, prod)
  modules/                        # Reusable Terraform modules
```

## Getting Started

See the [service-delivery-central](https://github.com/rene-rios-lt/service-delivery-central) repo for scripts to run the full system locally.

## Infrastructure

Azure infrastructure is managed via Terraform in `terraform/`. Each environment has its own `terraform.tfvars` under `terraform/environments/<env>/`.
