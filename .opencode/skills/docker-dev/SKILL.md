---
name: docker-dev
description: Run Billard-system locally with Docker Compose. Use when starting services with docker compose, billard-db, the billard app container, or debugging local ports.
---

# Local Docker

Prefer the root scripts (`npm run docker:dev`, `npm run db:up`, `npm run dev`). Raw compose ALWAYS passes both `-f` flags (without them the DB connection breaks — see README Gotchas). Run from the repo root:

```bash
# Full local stack (like production)
npm run docker:dev
# = docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build

# DB only (native dev: npm run dev / dev:api + dev:ui)
npm run db:up
# = docker compose -f docker-compose.yml -f docker-compose.local.yml up -d db

# Tear down (data persists in billard-pg volume)
docker compose -f docker-compose.yml -f docker-compose.local.yml down

# Full reset (deletes DB data)
docker compose -f docker-compose.yml -f docker-compose.local.yml down -v
```

Local ports:
- App: `http://localhost:5000` (health at `/api/health`)
- DB: `127.0.0.1:5433` (loopback only)

Single `billard` image serves frontend + backend; Postgres is internal-only.

Rules:
- Native (no-Docker app) dev: `npm run dev` (DB in Docker + `dotnet run` backend `:5000` + `ng serve` frontend `:4200`); single side via `dev:api` / `dev:ui`.
- Stop the `billard` container before native `dotnet run`: `docker stop billard` (port 5000 conflict).
- Never bake secrets into images — `.env` is excluded via `.dockerignore`.
