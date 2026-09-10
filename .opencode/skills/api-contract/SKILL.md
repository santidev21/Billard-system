---
name: api-contract
description: Keep the .NET Minimal API and Angular client in sync when a contract changes. Use when changing endpoints, DTOs, SignalR hub methods, Angular models, services calling /api, or response shapes.
---

# API contract sync

Billard-system has no codegen — contracts are synced by hand. When a backend endpoint, DTO, or SignalR hub method changes, do all of these in the same pass:

1. Backend: endpoint/hub in `BilliardSystem.API`, DTO/service in `Application`, server-side validation on the endpoint.
2. Frontend: matching TypeScript type/interface, the Angular service that calls `/api/*`, and the SignalR client handler for hub changes.
3. Tests: backend test in `BilliardSystem.Tests/` and frontend `.spec.ts` covering the new shape.
4. Docs: update `docs/specs/` and `AGENTS.md` if behavior changed.

Checklist before finishing:
- `dotnet build backend/BilliardSystem.slnx -c Release` passes.
- `npm run build` (from `frontend/`) passes.
- Auth boundary preserved: admin endpoints protected, player/kiosk endpoints anonymous by design.
