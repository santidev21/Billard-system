# Billard-system Project Context

This file is the working context for Billard-system. Keep it updated when architecture, routing, scripts, or conventions change. See [docs/specs/](docs/specs/) for detail docs.

## Project Snapshot
Real-time billiard hall management platform:
- Angular 22 frontend (standalone components, signals, lazy routes; karma/jasmine tests)
- .NET 10 Minimal API backend with Clean Architecture (API, Application, Domain, Infrastructure)
- PostgreSQL 16 via EF Core (migrations in Infrastructure, auto-applied at startup)
- SignalR WebSockets for real-time table/session updates (`/hubs`)
- Opaque token sessions (30-day sliding expiry) with PBKDF2 password hashing
- Two apps: Admin panel (`features/admin`) + Player table kiosk (`features/player`)
- Single Docker image (frontend + backend); Postgres isolated on internal network
- Root `package.json` orchestrates local dev (`dev`, `dev:ui/dev:api`, `db:*`, `docker:dev` scripts)

## Repository Layout
```text
Billard-system/
├─ backend/         # .NET solution (BilliardSystem.slnx: API, Application, Domain, Infrastructure, Tests)
├─ frontend/        # Angular 22 application (src/app: core, features, shared)
├─ deploy/          # Nginx config, deployment guide
├─ docs/            # Guides, screenshots, specs (docs/specs/)
├─ .opencode/       # AI home: agent/, command/, skills/ (tracked; local plugin scaffold ignored)
├─ .github/         # CI/CD workflows
├─ Dockerfile       # Multi-stage single-image build
├─ scripts/         # Local-dev orchestration scripts (run-billard.mjs)
├─ package.json     # Root orchestration scripts (dev, db:*, docker:dev, build, test)
├─ docker-compose.yml
├─ docker-compose.local.yml
├─ opencode.json    # opencode config: instructions, MCP, permissions
└─ .env.example
```

## Backend Architecture
Minimal API with Clean Architecture layers: `API` (endpoints, SignalR hubs, auth middleware) → `Application` (services, abstractions) → `Domain` (entities, enums, events) → `Infrastructure` (EF Core, `BilliardDbContext`, DI, `DatabaseInitializer`). Migrations auto-apply at startup — never edit applied migrations, add a new one.

## Frontend Architecture
Angular 22 SPA in `frontend/src/app` (`core/` auth/API/SignalR/models, `features/` admin/player/catalog/history/audit, `shared/` components). Hash routing (`/#/login`). Dev proxies `/api` and `/hubs` (WebSockets) → `localhost:5000` (`proxy.conf.json`).

## Commands (run from repo root via root scripts unless noted)
- Both: `npm run dev` (DB in Docker + backend + frontend, hot reload) · `npm run build` · `npm run test` (see `/test`)
- Single side: `npm run dev:api` · `npm run dev:ui`
- Migrations: `npm run db:migrate` (ensures DB; migrations auto-apply at startup) · `npm run db:migration:add -- <Name>` (see `db-migrations`, or `/migrate`)
- DB: `npm run db:up` (Postgres on `127.0.0.1:5433`, loopback-only) · `npm run db:down`
- Docker: `npm run docker:dev` (= `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build`) · `npm run docker:down` (see `docker-dev`)
- Shortcuts: `/test` (both suites) · `/migrate`

## Ports
| Context | Backend | Frontend | DB |
|---|---|---|---|
| Native dev (`npm run dev`) | `http://localhost:5000` | `http://localhost:4200` (proxies `/api`, `/hubs` → 5000) | `127.0.0.1:5433` (docker, same volume) |
| Docker local (`npm run docker:dev`) | `http://localhost:5000` (health at `/api/health`) | served from the same image | internal only |

## AI Setup
- `.opencode/` is the AI home (tracked in git): `skills/` (task playbooks in `SKILL.md` format), `agent/` (per-area playbooks: backend, frontend, reviewer), `command/` (shortcuts: /test, /migrate). Local plugin scaffold (`node_modules`, `package.json`) is ignored.
- `opencode.json` holds instructions, MCP servers and permissions. Skills, agents and commands need no config — opencode auto-discovers `.opencode/`.
- `AGENTS.md` is the single source of truth; `docs/specs/` holds details.

## Working Rules For This Repo
- Prefer small, focused changes.
- Keep API contracts, frontend types, and tests aligned in the same pass.
- EF migrations live in `BilliardSystem.Infrastructure`; never edit applied migrations, add a new one (auto-applied at startup via `DatabaseInitializer`; `npm run db:migration:add -- <Name>`).
- DB always runs in Docker — prefer the root scripts (`npm run dev/db:*`); raw compose ALWAYS uses both `-f` flags: `docker compose -f docker-compose.yml -f docker-compose.local.yml …`.
- Native `dotnet run` takes DB/JWT/super-admin values from `.env` via the root scripts; prefer `npm run dev*` over per-folder commands.
- Stop the `billard` container before native `dotnet run` (port 5000 conflict): `docker stop billard` (or `npm run docker:down` first).
- Keep the root README and this file synchronized when behavior changes.
