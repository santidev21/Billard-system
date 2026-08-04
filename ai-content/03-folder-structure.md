# 03 - Estructura de Directorios (Folder Structure)

```
c:\Dev\Billard-system\
├── ai-content/                         # Documentación incremental para IA
│   ├── 00-project-overview.md
│   ├── 01-architecture.md
│   ├── 02-tech-stack.md
│   ├── ...
│   └── progress.md
├── backend/                            # Solución C# / .NET 9
│   ├── BilliardSystem.sln
│   ├── src/
│   │   ├── BilliardSystem.Domain/
│   │   │   ├── Entities/               # Table, MatchHistory, Product, AuditLog, User...
│   │   │   ├── Enums/                  # TableStatus, GameMode, UserRole...
│   │   │   ├── Events/                 # Domain Events (IDomainEvent)
│   │   │   └── Interfaces/             # Repositories & Services contracts
│   │   ├── BilliardSystem.Application/
│   │   │   ├── EventBus/               # IEventBus, EventPublisher
│   │   │   ├── EventHandlers/          # Handlers for domain events
│   │   │   ├── Services/               # Use cases / Application services
│   │   │   └── DTOs/                   # Data Transfer Objects
│   │   ├── BilliardSystem.Infrastructure/
│   │   │   ├── Persistence/            # BilliardDbContext, Migrations, Repositories
│   │   │   ├── Hubs/                   # TableHub (SignalR)
│   │   │   └── Services/               # AuditLogger, NotificationService
│   │   └── BilliardSystem.API/
│   │       ├── Controllers/            # TablesController, MatchesController, ProductsController...
│   │       ├── Middlewares/            # ExceptionMiddleware, JwtMiddleware
│   │       └── Program.cs              # DI Setup & SPA Static Files hosting
│   └── tests/
│       ├── BilliardSystem.Domain.Tests/
│       ├── BilliardSystem.Application.Tests/
│       └── BilliardSystem.Infrastructure.Tests/
└── frontend/                           # Proyecto Angular 19+
    ├── angular.json
    ├── proxy.conf.json                 # Proxy para Dev Mode hacia localhost:5000
    ├── src/
    │   ├── app/
    │   │   ├── core/                   # Services (SignalR, OfflineQueue, Auth, Api)
    │   │   ├── shared/                 # Components (ScoreCard, VideoReplay, Modal)
    │   │   ├── features/
    │   │   │   ├── player/             # Player App (Marcador, Cámara, Consumo)
    │   │   │   ├── admin/              # Admin App (Grid mesas, Dashboard, Consumo)
    │   │   │   ├── settings/           # Configuración del sistema
    │   │   │   ├── history/            # Historial de partidas enriquecido
    │   │   │   └── audit/              # Visor de logs de auditoría
    │   │   └── app.routes.ts
    │   └── assets/
    └── package.json
```

## Convenciones de Código
- **C#**: CamelCase en variables locales, PascalCase en métodos/propiedades/clases. Async/Await en llamadas E/S.
- **TypeScript**: camelCase en variables/métodos, PascalCase en clases/interfaces. Signals para estado reactivo.
- **Git / Commits**: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`).
