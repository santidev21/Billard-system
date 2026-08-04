# 00 - Visión General del Proyecto (Project Overview)

## Descripción General
El **Sistema de Administración de Mesas de Billar Tres Bandas** es una solución de software moderna diseñada para operar de forma local (LAN) en establecimientos de billar y dispositivos individuales. Controla en tiempo real el marcador de carambolas, el tiempo transcurrido por mesa, el consumo de alimentos y bebidas, la transmisión y repetición instantánea por cámara USB, y la auditoría completa de operaciones.

## Objetivos del Sistema
1. **Control de Marcador en Tiempo Real**: Proporcionar una interfaz visual optimizada basada en la bola blanca y amarilla de billar tres bandas para anotar puntajes (`+1, +2, +3, +5, -1`) sin latencia.
2. **Repetición Instantánea (Instant Replay)**: Grabar continuamente la mesa mediante una cámara USB manteniendo un buffer circular configurable (30s a 5m) en memoria sin detener la grabación mientras se reproduce.
3. **Administración y Auditoría**: Ofrecer a los administradores y empleados control sobre tarifas, inicio/cierre de mesas, venta de productos y registro de auditoría de cada acción.
4. **Modos de Juego Flexibles**:
   - **Modo Administrado**: La sesión la inicia y liquida el administrador; sincroniza el consumo y total acumulado.
   - **Modo Libre**: Autoservicio para jugadores en casa o juego casual con seguridad UX anti-cierres accidentales (Long-press + Slide confirmation).
5. **Resiliencia LAN / Offline**: Garantizar que desconexiones temporales de la red Wi-Fi/LAN no causen pérdida de datos mediante colas de comandos con resincronización por GUID de transacción.

## Componentes del Sistema
- **Player App (Aplicación de la Mesa)**: Interfaz responsiva para jugadores en la mesa (PC/Tablet).
- **Admin App (Aplicación del Administrador)**: Panel de control con grid de mesas, catálogo de productos, dashboard, auditoría y configuración.
- **Backend API & Real-time Hub**: API en ASP.NET Core 9 con SignalR WebSockets y Event Bus desacoplado.
- **AI Content (`/ai-content`)**: Documentación técnica incremental en vivo para desarrollo con IA.
