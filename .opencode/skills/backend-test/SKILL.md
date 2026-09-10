---
name: backend-test
description: Build and run the Billard-system .NET backend tests. Use when running backend tests, dotnet build, dotnet test, BilliardSystem.Tests, or checking backend coverage.
---

# Backend tests (.NET 10)

Solution: `backend/BilliardSystem.slnx` (`.slnx` format — needs .NET 9+ SDK). Tests use xUnit + FluentAssertions + Moq with EF InMemory (no database needed).

Run from the repo root:

```bash
dotnet build backend/BilliardSystem.slnx -c Release
dotnet test backend/BilliardSystem.slnx -c Release --collect:"XPlat Code Coverage"
```

Rules:
- Unit tests run standalone; the DB always lives in Docker and is not needed for tests.
- Verified command form (from `backend/`): `dotnet test BilliardSystem.slnx`.
- Coverage targets: >85% on Domain/Application business rules (time/rate calculation, validations); unit-test Angular state services, scoring and offline queue.
- When you change an API contract or hub method, update the Angular types/services and tests in the same pass (see `api-contract`).
