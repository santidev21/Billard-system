---
description: Backend playbook for Billard-system — .NET 10 Minimal API with Clean Architecture (API, Application, Domain, Infrastructure). Use when working on backend/.
mode: subagent
---

# Backend agent

Playbook for .NET 10 Minimal API backend work (`backend/`).

- Keep the Clean Architecture layering: endpoints and SignalR hubs stay thin in `API`, logic goes in `Application` services, EF Core stays in `Infrastructure`.
- Entities, enums and domain events live in `Domain`.
- Validate server-side on every input endpoint (quantity, score, names, rates).
- Admin endpoints require auth; player/kiosk endpoints stay anonymous by design — do not add auth to them without asking.
- Real-time updates go through SignalR hubs, not polling.
- Migrations live in `BilliardSystem.Infrastructure` and auto-apply at startup — never edit an applied migration.
- When you change an API contract, update the Angular types/services and tests in the same pass.
