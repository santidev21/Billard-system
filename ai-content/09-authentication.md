# 09 - Autenticación y Autorización (Authentication & Authorization)

## Esquema de Autenticación
- **Mecanismo**: Tokens JWT (JSON Web Tokens) firmados con clave simétrica HS256.
- **Expiración de Token**: 12 horas por sesión activa.
- **Acceso Jugadores**: Los dispositivos de la mesa (Player App) funcionan en modo Kiosk / Anónimo con permisos restringidos únicamente a endpoints y eventos de su propia mesa (`Table_{id}`). No requieren login.

## Matriz de Roles y Permisos

| Recurso / Acción | Rol Administrador | Rol Empleado | Player App (Mesa) |
|---|---|---|---|
| Iniciar Partida | Si | Si | Si (Solo Modo Libre) |
| Marcar Carambolas | Si | Si | Si |
| Cambiar Nombres | Si | Si | Si |
| Llamar Mesero | No | No | Si |
| Pedir Cuenta | No | No | Si |
| Agregar Consumo | Si | Si | No (Solo Lectura) |
| Cerrar Mesa | Si | Si | Si (Solo Modo Libre) |
| Ver Dashboard / Métricas | Si | No | No |
| Ver Auditoría | Si | No | No |
| Configurar Tarifas/Replay/Branding | Si | No | No |
| Gestionar Productos/Categorías | Si | No | No |
| Gestionar Usuarios | Si | No | No |

## Flujo de Autenticación Admin
1. El usuario ingresa credenciales (`Username` + `Password`) en la Admin App.
2. El endpoint `POST /api/v1/auth/login` valida la contraseña con Hashing seguro (BCrypt/PBKDF2).
3. Si es válido, retorna el token JWT conteniendo las Claims: `sub` (UserId), `name` (FullName), `role` (Administrador/Empleado).
4. El cliente Angular guarda el token en memoria/HTTP-Only Cookie y lo envía en el header `Authorization: Bearer <token>` en cada petición HTTP y en la reconexión de SignalR WebSockets.
