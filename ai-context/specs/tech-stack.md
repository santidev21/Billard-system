# 02 - Stack Tecnológico (Tech Stack)

## Backend & API
- **Framework**: .NET 9.0 (ASP.NET Core Web API).
- **Lenguaje**: C# 13.
- **ORM / Persistencia**: Entity Framework Core 9.0 con proveedor SQLite (`Microsoft.EntityFrameworkCore.Sqlite`).
- **Real-Time WebSockets**: ASP.NET Core SignalR.
- **Seguridad**: JWT Bearer Tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`).
- **Pruebas Automatizadas**: xUnit, FluentAssertions, Moq, EF Core SQLite In-Memory.

## Frontend
- **Framework**: Angular 19+ (Standalone Components, Signals, new Control Flow `@if/@for`).
- **Lenguaje**: TypeScript 5.5+.
- **Estilos**: Vanilla CSS moderno (CSS Variables, Flexbox/Grid, Glassmorphism, temas HSL con bola blanca `#FFFFFF`, bola amarilla `#FACC15`, paño verde `#0F5132` y slate oscuro `#1E293B`).
- **WebSockets Client**: `@microsoft/signalr` (versión 8/9).
- **Media API**: HTML5 `navigator.mediaDevices.getUserMedia` + `MediaRecorder` API para buffer circular de video.
- **Resiliencia Client**: `IndexedDB` / `LocalStorage` para cola de comandos offline.
- **Pruebas**: Jasmine & Karma / Vitest.

## Justificación de Decisiones Técnicas
1. **.NET 9 + SQLite**: Rendimiento superior, consumo mínimo de RAM en la PC del billar, ejecución 100% offline sin necesidad de instalar servicios de base de datos de terceros. EF Core permite migrar a SQL Server o Postgres sin reescribir la lógica.
2. **Angular 19+**: Unifica el código para PC, tablets y móviles con 100% de reutilización. Las Signals de Angular 19 garantizan reactividad instantánea en los marcadores sin sobrecarga de cambio de detección.
3. **SignalR**: Solución nativa de .NET para WebSockets con reconexión automática, manejo de salas por mesa y resiliencia de red.
