#!/usr/bin/env bash
# Regenerate the canonical wire contract — contracts/openapi.json (ADR-0011 / QUAL-006).
#
# An ordinary `dotnet build` deliberately does NOT regenerate the contract
# (OpenApiGenerateDocumentsOnBuild=false), so the OpenApiContractTests sync-check stays honest.
# Run this after changing any REST DTO, endpoint, or response shape, then commit the updated
# contracts/openapi.json so the frontend and simulator mirror the real shapes.
#
# Idempotent: if nothing changed, the regenerated document is byte-identical.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Regenerating contracts/openapi.json from ServiceDelivery.Api ..."
dotnet build src/ServiceDelivery.Api --nologo -v minimal \
  /p:OpenApiGenerateDocumentsOnBuild=true

echo "==> Done. Review the diff and commit contracts/openapi.json:"
git -C "$REPO_ROOT" status --short contracts/openapi.json || true
