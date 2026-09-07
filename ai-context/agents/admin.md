# Agente Admin

Playbook para el panel de administración (`frontend/src/app/features/admin`).

- Vistas: grid de mesas, dashboard, catálogo, historial, auditoría, configuración.
- Todas las rutas admin requieren sesión (tokens opacos, 30 días de expiración deslizante).
- Cambiar la contraseña revoca todas las sesiones.
- El audit registra fin de sesión, cambios de catálogo, creación/inhabilitación/borrado de mesas y precio por hora (no llamadas de mesero ni solicitudes de cuenta).
