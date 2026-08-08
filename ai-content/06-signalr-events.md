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

## Estado Implementado Actual - 2026-08-07 (Real-time entre Player y Admin)
- Se emitieron broadcasts reales desde los endpoints REST hacia SignalR para que la pantalla de jugador y la de admin se actualicen en vivo sin recargar:
  - **`start`**: `TableStateUpdated` a `Clients.All` (status `Occupied`) y `SessionStarted` al grupo `table:{id}`.
  - **`score`**: `PlayerScored` al grupo `table:{id}` y ademas `TableStateUpdated` a `Clients.All`.
  - **`players`**: `PlayerNamesChanged` al grupo y `TableStateUpdated` a `Clients.All`.
  - **`consumption`**: `ConsumptionAdded` al grupo (con `consumptionTotal`) y `TableStateUpdated` a `Clients.All`.
  - **`call-waiter`**: `AdminNotification` a `Clients.All`.
  - **`request-check`**: `AdminRequest` a `Clients.All`.
  - **`finish`**: `SessionEnded` al grupo y `TableStateUpdated` (status `Available`) a `Clients.All`.
  - **`finish-round`**: `TableStateUpdated` (grupo + All) y `PlayerScored` de reset.
  - **`PUT /tables/rate/all`**: `TableStateUpdated` con `tableId=null, status="RateChanged"` a `Clients.All`.
- **Contrato cliente**: `TableStateUpdated` es la fuente de verdad autoritativa: tanto `DashboardComponent` (admin) como `PlayerComponent` reaccionan y re-consultan el detalle de la mesa afectada, sincronizando puntuacion, consumos, nombres, tarifas y cierre sin recargar.
- `PlayerComponent` tambien suscribe `PlayerScored`, `ConsumptionAdded` y `PlayerNamesChanged` para aplicar cambios granulares inmediatos cuando el evento corresponde a su `tableId`.
- El dashboard de admin hace *debounce* (350 ms) del refresco ante rafagas de `TableStateUpdated` y carga los detalles de todas las mesas en paralelo (`Promise.all`) para no saturar maquinas viejas.
