# 06 - Eventos de SignalR (SignalR Events)

Hub Endpoint: `/hubs/tables`

## Grupos de SignalR
- `AllTables`: Suscritos los administradores y empleados para ver cambios globales en todas las mesas.
- `Table_{tableId}`: Suscritos los clientes (Player App) asignados a esa mesa específica.

## Eventos Emitidos por el Servidor hacia Clientes

### 1. `TableStateUpdated`
- **Publicado cuando**: Cambia el estado completo de una mesa (Inicio de partida, cambio de tarifa, cambio de estado).
- **Payload**: `TableStateDto` (Id, Number, Status, SessionData, TotalAmount, TimeElapsedSeconds).

### 2. `PlayerScored`
- **Publicado cuando**: Un jugador suma o resta carambolas.
- **Payload**: `{ tableId: number, playerNumber: 1|2, delta: number, newScore: number, totalCarambolas: number }`

### 3. `PlayerNamesChanged`
- **Publicado cuando**: Se actualiza el nombre de un jugador.
- **Payload**: `{ tableId: number, player1Name: string, player2Name: string }`

### 4. `ConsumptionAdded`
- **Publicado cuando**: El administrador agrega un producto a la mesa.
- **Payload**: `{ tableId: number, item: MatchConsumptionDto, subtotal: number, total: number }`

### 5. `WaiterRequestedNotification`
- **Publicado cuando**: La mesa presiona "Llamar Mesero".
- **Payload**: `{ tableId: number, tableName: string, timestamp: string }`

### 6. `CheckRequestedNotification`
- **Publicado cuando**: La mesa presiona "Pedir Cuenta".
- **Payload**: `{ tableId: number, tableName: string, totalAmount: number, timestamp: string }`

### 7. `SessionEnded`
- **Publicado cuando**: Se finaliza la partida (por Admin o Modo Libre).
- **Payload**: `{ tableId: number, matchHistoryId: number, winnerName: string }`

### 8. `AuditLogCreated`
- **Publicado a**: Grupo `AllTables` (Solo Admin).
- **Payload**: `AuditLogDto`.
---

## Estado Implementado Actual - 2026-08-04
- `TableHub` existe en `backend/src/BilliardSystem.API/Hubs/TableHub.cs`.
- Metodos disponibles:
  - `JoinTableGroup(Guid tableId)`: une la conexion al grupo `table:{tableId}`.
  - `LeaveTableGroup(Guid tableId)`: saca la conexion del grupo `table:{tableId}`.
- Aun faltan handlers que emitan los eventos de servidor listados arriba.
- El formato de grupo implementado es `table:{tableId}`; el formato `Table_{tableId}` queda como objetivo anterior a reconciliar.
