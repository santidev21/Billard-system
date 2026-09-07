# 05 - Endpoints de la API (API Endpoints)

Todos los endpoints REST están prefijados con `/api/v1`.

## 1. Autenticación (`/api/v1/auth`)
- `POST /api/v1/auth/login`: Autentica un usuario (Admin/Empleado) y retorna un token JWT.
- `GET /api/v1/auth/me`: Retorna los datos del usuario autenticado actual.

## 2. Gestión de Mesas (`/api/v1/tables`)
- `GET /api/v1/tables`: Lista todas las mesas con su estado actual (Free, Occupied, WaiterRequested, CheckRequested).
- `GET /api/v1/tables/{id}`: Obtiene el estado detallado de una mesa específica (sesión activa, nombres, marcador, cronómetro, consumos).
- `POST /api/v1/tables`: Crea una nueva mesa (Admin).
- `PUT /api/v1/tables/{id}`: Actualiza datos de la mesa (Admin).
- `POST /api/v1/tables/{id}/start`: Inicia una sesión en la mesa (Modo Administrado o Libre, nombres iniciales, tarifa por hora).
- `POST /api/v1/tables/{id}/score`: Registra actualización de puntaje (`PlayerNumber`: 1/2, `Delta`: +1, +2, +3, +5, -1, `TransactionId`: GUID).
- `POST /api/v1/tables/{id}/players`: Actualiza nombres de Jugador 1 / Jugador 2.
- `POST /api/v1/tables/{id}/call-waiter`: Presiona el botón "Llamar Mesero".
- `POST /api/v1/tables/{id}/request-check`: Presiona el botón "Pedir Cuenta".
- `POST /api/v1/tables/{id}/consumption`: Agrega un producto del catálogo a la mesa (Admin/Empleado).
- `POST /api/v1/tables/{id}/finish`: Finaliza la sesión de la mesa, liquida totales, genera `MatchHistory` y reinicia el marcador.

## 3. Catálogo de Productos (`/api/v1/products`)
- `GET /api/v1/products`: Lista todos los productos activos agrupados por categoría.
- `POST /api/v1/products`: Crea un nuevo producto (Admin).
- `PUT /api/v1/products/{id}`: Actualiza un producto (Admin).
- `DELETE /api/v1/products/{id}`: Desactiva un producto (Admin).

## 4. Historial de Partidas (`/api/v1/matches`)
- `GET /api/v1/matches`: Lista el historial de partidas filtrado por fecha, modo de juego o mesa.
- `GET /api/v1/matches/{id}`: Obtiene el detalle completo de una partida pasada (carambolas por minuto, consumos, tiempos, operador).

## 5. Dashboard Administrativo (`/api/v1/dashboard`)
- `GET /api/v1/dashboard/metrics`: Obtiene KPIs en vivo (mesas libres/ocupadas, ingresos hoy, tiempo promedio).
- `GET /api/v1/dashboard/top-products`: Obtiene los productos más vendidos del día/semana.

## 6. Auditoría (`/api/v1/audit`)
- `GET /api/v1/audit/logs`: Consulta de registros de auditoría filtrados por usuario, fecha o acción (Admin).

## 7. Configuración (`/api/v1/settings`)
- `GET /api/v1/settings`: Obtiene la configuración del sistema (branding, tarifa default, buffer de replay).
- `PUT /api/v1/settings`: Actualiza la configuración global (Admin).
---

## Estado Implementado Actual - 2026-08-04
La API inicial usa prefijo `/api` sin versionado todavia:
- `GET /api/health`
- `GET /api/tables`
- `GET /api/products`
- `GET /api/settings`
- `GET /api/dashboard/summary`

## Estado Implementado Actual - 2026-08-07
- `POST /api/auth/login`: login admin. Primer login sin clave configurada crea `AdminPassword` (SHA256 hex en `Settings`); si ya existe, valida contra el hash. Retorna `{ token }` (token simple, sin JWT real por ahora). Body: `{ password }` (solo clave, sin usuario).
- `POST /api/auth/change-password`: actualiza la clave de acceso. Body: `{ currentPassword, newPassword }` (nueva clave min. 4 caracteres). Valida la clave actual y retorna `{ ok: true }`.
- `POST /api/tables`: crea mesa (`CreateTableRequest: name, hourlyRate`).
- `PUT /api/tables/{id}`: renombra y/o actualiza tarifa (`UpdateTableRequest: name?, hourlyRate`).
- `PUT /api/tables/rate/all`: **nuevo** — aplica la misma tarifa por hora a TODAS las mesas (`UpdateAllRatesRequest: hourlyRate`). Emite `TableStateUpdated` a `Clients.All`.
- `POST /api/tables/{id}/start|score|players|consumption|call-waiter|request-check|finish`: comandos de partida con idempotencia por `TransactionId` (via auditoria) y broadcasts SignalR (ver `06-signalr-events.md`).
- `POST /api/tables/{id}/finish-round`: cierra la ronda actual (`MatchRound`), incrementa `RoundNumber`, resetea el marcador a 0/0 y emite broadcast. Retorna `RoundResponse(Id, RoundNumber, WhiteScore, YellowScore, WinnerName)`.
- `GET /api/tables/{id}/rounds`: **nuevo** — historial de rondas de la partida activa. Retorna `RoundHistoryResponse(WhiteRounds, YellowRounds, CurrentRoundNumber, Rounds[])` con cada ronda `RoundDetailResponse(RoundNumber, WhiteScore, YellowScore, WinnerName, EndedAt)`.
- `GET /api/matches`, `GET /api/matches/{id}`, `GET /api/dashboard/summary`, `GET /api/dashboard/top-products`, `GET /api/audit/logs`, `GET/PUT /api/settings`, CRUD `/api/products`.
- `GET /api/dashboard/summary` ahora incluye `SalesByGame` y `SalesByConsumption` (desglose de ventas del dia).
- El seed crea 1 sola mesa: "Mesa 1" (`10000000-0000-0000-0000-000000000001`, tarifa 12000).
