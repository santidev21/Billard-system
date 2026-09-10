# 01 - Arquitectura de Software (Architecture)

## Enfoque Arquitectónico
El sistema sigue los principios de **Clean Architecture** (Arquitectura Limpia) combinada con **Domain-Driven Design (DDD) ligero** y **Event-Driven Architecture (Event Bus)** en C# / ASP.NET Core 10 (.NET 10, Minimal API).

```
                  +-----------------------------------+
                  |   BilliardSystem.API (Controllers)|
                  +-----------------+-----------------+
                                    |
                                    v
                  +-----------------+-----------------+
                  |   BilliardSystem.Application      |
                  |   (Use Cases / CQRS / Events)     |
                  +-----------------+-----------------+
                                    |
                                    v
                  +-----------------+-----------------+
                  |   BilliardSystem.Domain           |
                  |   (Entities / Enums / Rules)      |
                  +-----------------+-----------------+
                                    ^
                                    |
                  +-----------------+-----------------+
                  |   BilliardSystem.Infrastructure   |
                   |   (EF Core / PostgreSQL / SignalR)  |
                  +-----------------------------------+
```

## Proyectos Backend y Responsabilidades
1. **`BilliardSystem.Domain`**:
   - Entidades del dominio (`Table`, `TableSession`, `Player`, `MatchHistory`, `MatchScoreLog`, `MatchConsumption`, `Category`, `Product`, `User`, `AuditLog`, `Settings`).
   - Reglas de negocio puras, objetos de valor y eventos de dominio (`IDomainEvent`).
   - Sin dependencias de frameworks ni librerías externas.
2. **`BilliardSystem.Application`**:
   - Casos de uso (Servicios de aplicación / Comandos y Consultas).
   - Event Bus embebido (`IEventBus`, `IEventHandler<T>`).
   - DTOs, validaciones con FluentValidation e interfaces de servicios.
3. **`BilliardSystem.Infrastructure`**:
   - Implementación de `BilliardDbContext` con Entity Framework Core 10 sobre PostgreSQL (SQLite solo fue el motor inicial en LAN).
   - Implementaciones de repositorios.
   - Hub de SignalR (`TableHub`) para comunicación en tiempo real.
   - Servicio de auditoría y notificaciones.
4. **`BilliardSystem.API`**:
   - Minimal API endpoints.
   - Auth middleware (sesiones opacas), manejo global de excepciones y CORS.
   - Configuración de hosting de archivos estáticos en Modo Producción (SPA Hosting).

## Arquitectura de Eventos Internos (Event Bus)
Todas las mutaciones de estado publican eventos en el `EventBus`. Los handlers procesan la actualización de base de datos, generación de logs de auditoría y emisión de eventos de SignalR de forma desacoplada.

## Modos de Operación
- **Dev Mode**: Angular `ng serve` en port 4200 conectado vía HTTP/WebSockets a la API en `localhost:5000`.
- **Prod Mode**: La API de .NET sirve los assets estáticos compilados de Angular directamente en `wwwroot`, empaquetando toda la app en un solo binario `.exe`.

## Flujo de Tráfico y Despliegue (2026-09)
```
Internet → vps-gateway (:80/:443)
  └── billard.santidev21.tech → billard (single image: Angular + .NET)
        ├── /api/*  → .NET 10 Minimal API
        ├── /hubs   → SignalR WebSockets
        └── /*      → Angular 22 SPA
              └── db (PostgreSQL, internal network only)
```

| Servicio Docker | Descripción |
|---|---|
| `db` | PostgreSQL 16 (`billard-db-1`, loopback `:5433` en local) |
| `billard` | Single image: Angular SPA + .NET 10 API (`:5000`, health en `/api/health`) |
