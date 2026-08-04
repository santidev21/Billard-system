# Estado de Progreso del Proyecto (Progress)

## Resumen de Avance
- **Porcentaje Estimado**: 5%
- **Fase Actual**: Fase 0 completada | Esperando aprobación para Fase 1 (Backend Architecture & Event Bus).
- **Último Cambio**: Inicialización completa de la suite de documentación técnica incremental para IA en `/ai-content` (Fase 0).

---

## Detalle por Fases

### Fase 0: Inicialización y Documentación IA (`/ai-content`)
- [x] Estructuración y creación de 17 archivos de contexto en `/ai-content`.
- [x] Registro de decisiones iniciales de arquitectura (ADR-001 a ADR-006).

### Fase 1: Backend Architecture, Event Bus & Base de Datos (.NET 9)
- [ ] Solución .NET en 4 proyectos (`Domain`, `Application`, `Infrastructure`, `API`).
- [ ] Implementar Event Bus e `IDomainEvent`.
- [ ] Modelo de datos EF Core con SQLite (Entidades, Migraciones, Auditoría, Settings, MatchHistory enriquecido).
- [ ] Hub de SignalR (`TableHub`) para tiempo real.
- [ ] Endpoints REST (Auth, Tables, Matches, Products, Settings, Audit, Dashboard).
- [ ] Pruebas unitarias e integración con xUnit.

### Fase 2: Frontend Angular (19+) Dev/Prod & Módulos
- [ ] Proyecto Angular 19+ con Standalone Components, Signals y Proxy Dev.
- [ ] Player App (Marcador 3 bandas blanco/amarillo, cronómetro, consumos, campana, pedido de cuenta).
- [ ] Módulo de Cámara & Replay Configurable (30s a 5m) en RAM.
- [ ] Modo Libre con seguridad anti-alcohol (Long-press 3s + Modal Slide).
- [ ] Resiliencia Offline y Cola de Comandos IndexedDB.
- [ ] Admin App (Grid mesas, métricas dashboard, productos, auditoría en vivo).
- [ ] Historial de partidas enriquecido.

### Fase 3: Pruebas Automatizadas y Empaquetado Prod
- [ ] Pruebas de integración frontend y backend.
- [ ] Compilación SPA servida por API Kestrel (Ejecutable `.exe` único).

---

## Siguiente Tarea Recomendada
Presentar el diseño técnico detallado de la **Fase 1 (Backend Architecture, Event Bus & Base de Datos)** al usuario para su revisión y aprobación antes de generar el código.
---

## Actualizacion 2026-08-04 - Fase 1 iniciada
- **Porcentaje estimado actualizado**: 18%.
- **Estado**: backend base compilable y probado.
- **Implementado**:
  - Entidades de dominio para mesas, usuarios, auditoria, settings, catalogo, historial, scores y consumos.
  - Eventos de dominio principales e interfaces `IDomainEvent`, `IDomainEventDispatcher`, `IDomainEventHandler`.
  - `BilliardDbContext` con EF Core SQLite, seed inicial y `EnsureCreatedAsync`.
  - `TableHub` SignalR con grupos `table:{tableId}`.
  - Endpoints iniciales: `/api/health`, `/api/tables`, `/api/products`, `/api/settings`, `/api/dashboard/summary`.
  - Tests xUnit iniciales para reglas de inicio de sesion de mesa.
- **Verificacion**:
  - `dotnet build C:\Dev\Billard-system\backend\BilliardSystem.slnx` compila.
  - `dotnet test C:\Dev\Billard-system\backend\BilliardSystem.slnx` pasa 2/2 tests fuera del sandbox.
- **Siguiente tarea recomendada**: implementar comandos de partida con auditoria e idempotencia por `TransactionId`.
