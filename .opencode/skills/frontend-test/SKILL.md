---
name: frontend-test
description: Build and run the Billard-system Angular unit tests. Use when running frontend tests, npm test, karma, jasmine, Angular build, or checking frontend coverage.
---

# Frontend tests (Angular 22)

Run from `frontend/`:

```bash
npm install
npm test            # karma/jasmine
npm run build
```

Rules:
- Local dev proxies `/api` and `/hubs` (WebSockets) to `localhost:5000` (`proxy.conf.json`).
- A new component/service should ship with its `.spec.ts`.
- Real-time updates go through the SignalR client, never polling.
