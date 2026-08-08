# 14 - Problemas Conocidos y Limitaciones (Known Issues)

## Consideraciones Técnicas
1. **Permisos de Cámara USB en Navegador**:
   - En la Player App, los navegadores (Chrome/Edge) requieren contexto seguro (`https://` o `localhost`) para conceder permisos permanentes a `navigator.mediaDevices.getUserMedia()`. Si se accede por IP local en LAN (`http://192.168.x.x`), se debe habilitar el flag `unsafely-treat-insecure-origin-as-secure` en el navegador del cliente o generar un certificado SSL autofirmado local.
2. **Buffer de Video en RAM**:
   - Para duraciones de buffer de 5 minutos en resolución 1080p, la memoria de la pestaña del navegador puede superar los 150 MB. Se recomienda configurar 720p @ 30 FPS en `Settings` para dispositivos con memoria RAM limitada (< 4 GB).
3. **Bloqueo de Sockets por Antivirus/Firewall en Windows**:
   - Algunas configuraciones de Windows Firewall pueden bloquear el puerto 5000 para conexiones entrantes desde otras computadoras o celulares en la misma red LAN. Se debe agregar una regla de entrada en el Firewall para el puerto 5000 durante la instalación.
---

## Issue registrado 2026-08-04
- `Microsoft.OpenApi 2.0.0` y `SQLitePCLRaw.lib.e_sqlite3 2.1.11` reportan warnings NU1903 de vulnerabilidad durante `dotnet build` y `dotnet test`.
- Revisar upgrade, dependencia transitiva o supresion controlada antes de release.

## Resuelto 2026-08-08
- **ECONNREFUSED en `/api/*` tras reiniciar la maquina**: el backend no se levantaba solo y ademas su puerto (`5168` en launchSettings) no coincidia con el proxy de Vite (`localhost:5000`). Fix:
  - La API ahora escucha SIEMPRE en `http://localhost:5000` via appsettings `"Urls"` (aplica a `dotnet run` desde cualquier directorio) y `launchSettings.json` fue sincronizado.
  - Se creo `start-dev.bat` en la raiz del repo (`C:\Dev\Billard-system\start-dev.bat`) que levanta en dos ventanas la API (`dotnet run`) y el frontend (`npm start`). Tras un reinicio basta ejecutar ese archivo.
- **Login admin con usuario**: se simplifico a solo "clave de ingreso" (sin campo usuario) y se agrego el endpoint `POST /api/auth/change-password` con modal "Cambiar clave" en el panel admin (ver `09-authentication.md`).
- **Consumo total solo mostraba el ultimo item**: el endpoint `POST /tables/{id}/consumption` cargaba la `MatchHistory` SIN `.Include(Consumptions)`, por lo que `AddConsumption` recalculaba `ConsumptionTotal` con la coleccion vacia en memoria y sobrescribia el total persistido con solo el ultimo consumo. Se anadio `.Include(history => history.Consumptions)` antes de `AddConsumption`.
- **Sin actualizacion en vivo**: los broadcasts de SignalR no se emitian (solo existian los metodos Join/Leave del hub) y el `PlayerComponent` no se suscribia a ningun evento. Se emitieron broadcasts desde los endpoints y ambos lados (Player y Admin) ahora reaccionan a `TableStateUpdated` (ver `06-signalr-events.md`).
- **Repeticion de video en gris (blob ilegible)**: `chunks.shift()` descartaba el primer chunk del ring buffer, que contiene el segmento de inicializacion WebM; sin el, el blob concatenado no se decodifica. Ahora se conserva siempre el chunk 0 (init) y se desplazan solo los demas.
- **Login "usuario o contrasena incorrecto" al primer uso**: la BD ya contenia una `AdminPassword` de pruebas anteriores, por lo que el flujo de primer login (crear clave) se omitia y validaba contra ese hash desconocido. Se removio la fila `AdminPassword` de `Settings` para reiniciar el flujo.
- **Altura no completaba 100%**: faltaba dar `flex:1; min-height:0` al elemento host del componente ruteado (`:host`) y `min-height:0; overflow:hidden` al `.shell` para completar la cadena de altura; la pantalla del jugador ahora ocupa todo el alto disponible.
