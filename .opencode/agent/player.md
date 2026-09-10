---
description: Playbook for the Billard-system Player table kiosk (score, rounds, consumptions, waiter calls). Use when working on frontend/src/app/features/player.
mode: subagent
---

# Agente Player

Playbook para el quiosco de mesa (`frontend/src/app/features/player`).

- Interfaz para jugadores: marcador, rondas, consumos, llamados de mesero/cuenta.
- Modo libre: autoservicio con protecciones anti-cierre accidental (long-press + slide).
- En modo libre, el modal de partida terminada no debe ofrecer "Cerrar" (deja pantalla vacía).
- Endpoints de player son anónimos; las desconexiones LAN se absorben con cola offline.
