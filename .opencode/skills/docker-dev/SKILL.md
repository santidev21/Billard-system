---
name: docker-dev
description: Run Billard-system locally with Docker Compose. Use when starting services with docker compose, billard-db, the billard app container, or debugging local ports.
---

# Local Docker

ALWAYS pass both `-f` flags (without them the DB connection breaks — see README Gotchas). Run from the repo root:

```bash
# Full local stack (Flujo A, like production)
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build

# DB only (Flujo B: manual dotnet run + ng serve)
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d db

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
- Manual (no-Docker app) dev: backend `http://localhost:5000` (`dotnet run` from `backend/src/BilliardSystem.API`), frontend `http://localhost:4200` (`npm start` from `frontend/`).
- Stop the `billard` container before manual `dotnet run`: `docker stop billard` (port 5000 conflict).
- Never bake secrets into images — `.env` is excluded via `.dockerignore`.
