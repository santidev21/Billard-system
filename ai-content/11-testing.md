# 11 - Estrategia de Pruebas Automatizadas (Testing Strategy)

## Cobertura de Pruebas Esperada
- **Backend (Domain & Application)**: >85% cobertura en reglas de negocio, cálculo de tiempo/tarifa y validaciones.
- **Frontend (Angular Services & Signals)**: Pruebas unitarias de servicios de estado, marcadores y cola offline.

## Estructura de Pruebas Backend (.NET xUnit)
Proyectos de pruebas en `backend/tests/`:

1. **`BilliardSystem.Domain.Tests`**:
   - `TableTests.cs`: Verifica cambios de estado (`Free` -> `Occupied` -> `CheckRequested` -> `Free`), límites de score (no permitir puntajes < 0).
   - `TariffCalculatorTests.cs`: Verifica cálculo del costo por tiempo transcurrido en segundos exactos.
2. **`BilliardSystem.Application.Tests`**:
   - `ScoreCommandTests.cs`: Prueba la idempotencia con GUIDs duplicados.
   - `SessionEndedHandlerTests.cs`: Pruebas de integración de persistencia de `MatchHistory` con consumos e historial de carambolas.
   - `AuditLogHandlerTests.cs`: Verifica la generación inmutable de logs ante acciones de usuario.
3. **`BilliardSystem.Infrastructure.Tests`**:
   - `BilliardDbContextTests.cs`: Pruebas sobre la base de datos SQLite In-Memory (`Microsoft.EntityFrameworkCore.Sqlite`).

## Comandos de Ejecución de Pruebas
- **Ejecutar Pruebas Backend**:
  ```powershell
  dotnet test backend/BilliardSystem.sln --verbosity normal
  ```
- **Ejecutar Pruebas Frontend**:
  ```powershell
  cd frontend
  npm test
  ```
