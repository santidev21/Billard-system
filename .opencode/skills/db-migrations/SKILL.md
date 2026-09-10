---
name: db-migrations
description: Add EF Core migrations for Billard-system. Use when creating SQL migrations in BilliardSystem.Infrastructure (auto-applied at startup, never edited).
---

# EF Core migrations

Migrations live in `backend/src/BilliardSystem.Infrastructure/Migrations`. Run from `backend/`:

```bash
# Add a migration
dotnet ef migrations add YourMigrationName --project src/BilliardSystem.Infrastructure --startup-project src/BilliardSystem.API

# No manual apply step — DatabaseInitializer applies pending migrations automatically
# at startup (dotnet run or container restart)
```

Verify applied migrations:

```bash
docker exec -e PGPASSWORD=postgres billard-db-1 psql -U postgres -d billiard \
  -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1;"
```

Rules:
- Never edit an applied migration — add a new one.
- The DB always runs in Docker; never point migrations at a non-Docker database.
