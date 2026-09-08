# Billiard System

A real-time billiard hall management platform built with Angular and .NET. Manage tables, track sessions, score games, handle consumptions, and monitor your business from a single dashboard.

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-10-purple)
![Angular](https://img.shields.io/badge/Angular-22-red)
![SignalR](https://img.shields.io/badge/SignalR-real--time-green)

<!-- Hero screenshot -->
<!-- ![Dashboard](docs/screenshots/dashboard.png) -->

---

## Features

| Module | Description |
|--------|-------------|
| **Dashboard** | Live table overview, daily sales, top products, table status at a glance |
| **Table Management** | Create, edit, enable/disable tables with unique codes and hourly rates |
| **Game Sessions** | Start, score, rename players, finish sessions with automatic billing |
| **Rounds** | Track rounds within a match with winner detection |
| **Consumption** | Add products to active sessions with real-time total updates |
| **Waiter/Check Calls** | Players request service or the check from the table UI |
| **Camera Replay** | Circular video buffer for instant replay on each table |
| **Catalog** | Manage products and categories |
| **History** | Full match history with filters |
| **Audit Log** | Track every action with user, timestamp, and details |
| **Admin Auth** | Token-based authentication with 30-day sessions and PBKDF2 hashing |
| **Real-time** | Instant updates across all devices via SignalR WebSockets |

---

## Tech Stack

- **Frontend**: [Angular 22](https://angular.dev/) (standalone components, signals, lazy routes)
- **Backend**: [.NET 10](https://dotnet.microsoft.com/) Minimal API with Clean Architecture
- **Database**: SQLite via Entity Framework Core
- **Real-time**: [SignalR](https://learn.microsoft.com/aspnet/core/signalr/) WebSockets
- **Auth**: Custom opaque token sessions with PBKDF2 password hashing
- **CI/CD**: GitHub Actions (build on VPS via deploy.sh)
- **Deployment**: Docker + Nginx reverse proxy on VPS

---

## Architecture

```
                    Internet
                       │
            vps-gateway (nginx, 80/443)
                       │ billard-net (external)
                       ▼
┌──────────────────────────────────────┐
│   billard (app)                      │
│   ── billard-net (shared, gateway)   │
│   ── billard-internal-net (internal) │
│         └── db (postgres, aislada)   │
└──────────────────────────────────────┘
```

The Angular frontend and the .NET backend are packaged into a single image (`billard`). The Postgres DB lives on an internal network (`internal: true`) and only the app can reach it.

```
Frontend (Angular)          Backend (.NET)
┌─────────────────┐        ┌─────────────────────┐
│  SPA + Router   │──API──▶│  Minimal API         │
│  SignalR Client │◀─WS────│  SignalR Hub         │
│  Auth Interceptor│       │  Auth Middleware      │
│  Offline Queue  │        │  Rate Limiting       │
└─────────────────┘        │  EF Core + SQLite    │
                            └─────────────────────┘
```

---

## Project Structure

```
Billard-system/
├── backend/
│   └── src/
│       ├── BilliardSystem.API/           # Endpoints, Hubs, Auth
│       ├── BilliardSystem.Application/   # Abstractions, Services
│       ├── BilliardSystem.Domain/        # Entities, Enums, Events
│       └── BilliardSystem.Infrastructure/# Persistence, DI
├── frontend/
│   └── src/app/
│       ├── core/          # Auth, API, SignalR, Models
│       ├── features/      # Admin, Player, Catalog, History, Audit
│       └── shared/        # Reusable components
├── deploy/                # Nginx config, deployment guide
├── ai-context/            # Project documentation
├── Dockerfile             # Multi-stage build
└── docker-compose.yml     # Container orchestration
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://docs.docker.com/get-docker/) (for deployment)

### Local Development

```bash
# Clone
git clone git@github.com:santidev21/Billard-system.git
cd Billard-system

# Backend
cd backend/src/BilliardSystem.API
dotnet run
# API runs on http://localhost:5000

# Frontend (new terminal)
cd frontend
npm install
npm start
# App runs on http://localhost:4200
```

### Default Login

- URL: `http://localhost:4200/#/login`
- Password: `admin`
- You will be prompted to change it on first login (min. 8 characters)

### Docker Dev (test the current code in Docker)

```bash
# local: local networks + debug ports
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build

# clean state: wipes DB volume and re-seeds Demo/M1 (requires .env)
docker compose -f docker-compose.yml -f docker-compose.local.yml down -v
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build

# verify
docker compose -f docker-compose.yml -f docker-compose.local.yml ps
docker logs -f billard
docker exec billard-db-1 psql -U postgres -d billard -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1;"
# app at http://127.0.0.1:5000 (health: http://localhost:5000/api/health)
```

#### Troubleshooting

```bash
# "The container name /billard is already in use" -> remove the stale container, then retry up
docker rm -f billard
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build

# localhost:5000 refuses connection -> you probably ran without the local override;
# the base file publishes no ports. Always include both -f flags (see above).
```

---

## Deployment

Deploys happen automatically on push to `main` via GitHub Actions. For VPS setup and manual deploy commands, see [deploy/DEPLOY.md](deploy/DEPLOY.md).

---

## Screenshots

<!-- Add your screenshots here -->
<!-- ![Dashboard](docs/screenshots/dashboard.png) -->
<!-- ![Player](docs/screenshots/player.png) -->
<!-- ![Admin](docs/screenshots/admin.png) -->

---

## Security

- **PBKDF2** password hashing with 100k iterations and random salt
- **Opaque session tokens** (32 bytes, SHA-256 hashed in DB, 30-day sliding expiry)
- **Rate limiting** on login (5 req/min) and API (60 req/min)
- **Server-side validation** on all inputs (quantity, score, names, rates)
- **Admin endpoints protected** — player/kiosk endpoints remain anonymous
- **Security headers** (CSP, HSTS, nosniff, frame-ancestors)
- **Forwarded headers** for correct IP behind reverse proxy

---

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/login` | Public | Login (rate-limited) |
| POST | `/api/auth/logout` | Admin | Revoke session |
| POST | `/api/auth/change-password` | Admin | Change password + revoke all sessions |
| GET | `/api/tables` | Public | List all tables |
| POST | `/api/tables` | Admin | Create table |
| PUT | `/api/tables/{id}` | Admin | Update table |
| POST | `/api/tables/{id}/start` | Public | Start session |
| POST | `/api/tables/{id}/score` | Public | Add score |
| POST | `/api/tables/{id}/finish` | Public | Finish session |
| POST | `/api/tables/{id}/consumption` | Public | Add consumption |
| GET | `/api/dashboard/summary` | Admin | Daily summary |
| GET | `/api/audit/logs` | Admin | Audit trail |

---

## AI Context

[ai-context/](ai-context/) is the canonical project context for AI-assisted work (architecture snapshot, specs, agents, and skills).

---

## To Do

- [ ] Free-play mode: remove the "Cerrar" button from the "Partida terminada" modal — closing it leaves a blank screen.
- [ ] Audit log: stop logging waiter calls and check requests. Only log session end, catalog modifications, table creation, table disable/delete, and price-per-hour changes.
