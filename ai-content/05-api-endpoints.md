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
