# Billard-system — Contexto del Proyecto

Este archivo es el contexto de trabajo para Billard-system. Mantenerlo actualizado cuando cambien la arquitectura, las rutas, los scripts o las convenciones. Ver [specs/](specs/) para los documentos de detalle.

## Snapshot del Proyecto
Billard-system es una plataforma de gestión de salas de billar en tiempo real con:
- Frontend Angular 22 (standalone, signals, lazy routes)
- Backend .NET 10 Minimal API con Clean Architecture
- SQLite via EF Core (despliegue LAN) / Postgres en Docker (VPS)
- WebSockets en tiempo real con SignalR
- Sesiones con tokens opacos (PBKDF2, expiración deslizante de 30 días)
- Dos apps: Admin (panel de control) y Player (quiosco de mesa)

## Layout del Repo
```text
Billard-system/
├─ backend/src/    # API, Application, Domain, Infrastructure
├─ frontend/src/   # core, features (admin, player), shared
├─ ai-context/     # este contexto
├─ deploy/         # guía de deploy + nginx
├─ Dockerfile
└─ docker-compose.yml
```

## Reglas de Trabajo
- Cambios pequeños y enfocados.
- Los endpoints admin van protegidos; los de player/quiosco son anónimos.
- Al cambiar un contrato de API, actualizar los tipos del frontend en el mismo paso.
- Mantener el README raíz y este archivo sincronizados cuando el comportamiento cambie.
