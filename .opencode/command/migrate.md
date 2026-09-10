---
description: Add an EF Core migration for Billard-system (auto-applied at startup).
---

Follow the `db-migrations` skill. From `backend/`:

```bash
dotnet ef migrations add $ARGUMENTS --project src/BilliardSystem.Infrastructure --startup-project src/BilliardSystem.API
```

Rules: never edit an applied migration — add a new one. Migrations auto-apply at startup via `DatabaseInitializer` (`dotnet run` or container restart), so no manual apply step is needed.
