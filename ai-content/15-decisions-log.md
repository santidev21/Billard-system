# 15 - Registro de Decisiones de Arquitectura (Decisions Log / ADRs)

## ADR-001: Elección de .NET 9 + ASP.NET Core SignalR para el Backend
- **Contexto**: Se requiere una API de alto rendimiento que funcione en red local (LAN) en Windows, con sincronización de eventos en tiempo real.
- **Decisión**: Utilizar .NET 9 y SignalR.
- **Razón**: Máxima compatibilidad con C#, excelente soporte de WebSockets con reconexión automática y bajo consumo de recursos en PCs locales.

## ADR-002: Elección de SQLite con Entity Framework Core 9
- **Contexto**: El cliente no debe tener la complejidad de instalar y configurar servidores de base de datos como SQL Server o PostgreSQL en la PC del billar.
- **Decisión**: Adoptar SQLite a través de EF Core 9.
- **Razón**: Base de datos liviana en un solo archivo `.db`, portable y rápida. El uso de EF Core abstrae las consultas, permitiendo escalar a PostgreSQL o SQL Server en el futuro cambiando la cadena de conexión.

## ADR-003: Elección de Angular 19+ (Standalone Components & Signals)
- **Contexto**: Se requiere una interfaz moderna, ultra reactiva e idéntica tanto en pantallas de PC como en celulares y tablets.
- **Decisión**: Desarrollar la Player App y Admin App en Angular 19+.
- **Razón**: Las Signals de Angular proporcionan actualización instantánea en el DOM del marcador sin retrasos, reutilizando el 100% del código en todas las plataformas.

## ADR-004: Buffer Circular de Video en RAM Client-Side
- **Contexto**: El usuario requiere repetición instantánea de la jugada sin detener la grabación continua de la cámara USB.
- **Decisión**: Manejar el buffer circular mediante `MediaRecorder` API en la app Angular de la mesa, guardando fragmentos de 2s en un arreglo flotante en memoria RAM.
- **Razón**: Elimina la necesidad de transmitir video pesado hacia el servidor backend en red LAN, optimizando el ancho de banda y garantizando repetición a 0 latencia.

## ADR-005: Arquitectura Orientada a Eventos (Event Bus en C#)
- **Contexto**: Desacoplar la lógica de puntuación, consumos, notificaciones, auditoría y actualización de SignalR.
- **Decisión**: Implementar un Event Bus interno con Domain Events (`IDomainEvent`).
- **Razón**: Permite agregar nuevas funciones (ej. pantallas de TV, auditoría, estadísticas) simplemente suscribiendo nuevos event handlers sin tocar la lógica de negocio existente.

## ADR-006: Modos de Ejecución Dev vs Prod (Single Binary)
- **Contexto**: Facilitar el desarrollo con Hot-Reload y permitir la instalación en el cliente en un solo clic.
- **Decisión**: `Dev Mode` con `ng serve` + API separada; `Prod Mode` hospedando los archivos estáticos de Angular dentro de la API en Kestrel.
- **Razón**: Ofrece la mejor experiencia de desarrollo y despliegue sin fricción para el usuario final.
