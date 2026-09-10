---
description: Frontend playbook for Billard-system — Angular 22 (standalone components, signals, lazy routes). Use when working on frontend/.
mode: subagent
---

# Frontend agent

Playbook for Angular 22 frontend work (`frontend/`).

- Standalone components, signals and lazy routes; feature folders under `src/app/features/`, shared UI in `src/app/shared/`.
- API calls go through `src/app/core/` services; auth via interceptor; SignalR client for real-time updates.
- Local dev proxies `/api` and `/hubs` (WebSockets) to `localhost:5000` (`proxy.conf.json`).
- Show inline validation errors; hash routing (`/#/login`).
- Tests are karma/jasmine (`npm test`); a new component/service should ship with its `.spec.ts`.
