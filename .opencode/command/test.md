---
description: Run the full Billard-system test suites (backend + frontend unit tests).
---

Run both suites from the repo root and report failures per suite:

1. Backend (xUnit + FluentAssertions + Moq, EF InMemory — no database needed):
```bash
dotnet test backend/BilliardSystem.slnx -c Release
```

2. Frontend (from `frontend/`, karma/jasmine):
```bash
npm test
```

Extra input: $ARGUMENTS (e.g. a test name filter or a single suite: `backend` / `frontend`).

Do not fix failures unless asked — report which suite and which test failed.
