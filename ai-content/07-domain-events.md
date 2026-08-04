# 07 - Eventos de Dominio (Domain Events)

Interface base: `IDomainEvent` en `BilliardSystem.Domain.Events`.

## Eventos y Handlers

### 1. `SessionStartedEvent`
- **Se dispara cuando**: Se inicia una nueva sesión de juego en una mesa.
- **Handlers**:
  - `SessionStartedAuditHandler`: Escribe un registro en `AuditLogs`.
  - `SessionStartedSignalRHandler`: Transmite `TableStateUpdated` vía SignalR.

### 2. `PlayerScoredEvent`
- **Se dispara cuando**: Se presiona un botón de anotación (+1, +2, +3, +5, -1).
- **Handlers**:
  - `PlayerScoredSignalRHandler`: Notifica `PlayerScored` a la mesa y a los administradores.

### 3. `PlayerNameChangedEvent`
- **Se dispara cuando**: Se modifican los nombres de los jugadores.
- **Handlers**:
  - `PlayerNameChangedSignalRHandler`: Notifica `PlayerNamesChanged`.

### 4. `ConsumptionAddedEvent`
- **Se dispara cuando**: Se agrega un producto del catálogo a una mesa ocupada.
- **Handlers**:
  - `ConsumptionAddedAuditHandler`: Genera log de auditoría (ej. "Administrador Juan agregó 2 Poker 12:42 PM").
  - `ConsumptionAddedSignalRHandler`: Transmite `ConsumptionAdded` a la Player App y Admin App.

### 5. `WaiterRequestedEvent`
- **Se dispara cuando**: La Player App activa la campana de mesero.
- **Handlers**:
  - `WaiterRequestedSignalRHandler`: Transmite `WaiterRequestedNotification` al panel de administradores.

### 6. `CheckRequestedEvent`
- **Se dispara cuando**: La Player App activa "Pedir Cuenta".
- **Handlers**:
  - `CheckRequestedSignalRHandler`: Notifica `CheckRequestedNotification` a los administradores.

### 7. `SessionEndedEvent`
- **Se dispara cuando**: Se confirma el cierre de la partida.
- **Handlers**:
  - `SessionEndedPersistenceHandler`: Compone y guarda la entidad `MatchHistory` con todos sus score logs y consumos en SQLite.
  - `SessionEndedAuditHandler`: Registra la finalización de partida en auditoría.
  - `SessionEndedSignalRHandler`: Transmite `SessionEnded` para reiniciar la UI del marcador a 0.
---

## Estado Implementado Actual - 2026-08-04
- `IDomainEvent` esta implementado en `BilliardSystem.Domain.Common`, no en `BilliardSystem.Domain.Events`.
- Eventos implementados en `backend/src/BilliardSystem.Domain/Events/`.
- Dispatcher interno implementado en `BilliardSystem.Infrastructure.Events.DomainEventDispatcher`.
- Aun faltan handlers concretos de auditoria, persistencia estadistica y broadcast SignalR.
