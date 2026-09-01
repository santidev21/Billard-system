# 12 - Despliegue y Ejecución (Deployment & Execution)

## Modos de Ejecución

### 1. Modo Desarrollo (Dev Mode)
En este modo, el frontend y el backend corren por separado para permitir Hot-Reload de Angular y depuración fluida en .NET.

1. **Iniciar Backend**:
   ```powershell
   cd backend/src/BilliardSystem.API
   dotnet run
   # La API estará escuchando en http://localhost:5000 / https://localhost:5001
   ```
2. **Iniciar Frontend**:
   ```powershell
   cd frontend
   npm install
   npm start
   # Angular Dev Server estará disponible en http://localhost:4200 (Proxy hacia localhost:5000)
   ```

---

### 2. Modo Producción (Prod Mode - Single Binary Execution)
En este modo, Angular se compila y se integra directamente dentro de la API de ASP.NET Core. El resultado es un único ejecutable `.exe` independiente para Windows.

1. **Compilar Frontend**:
   ```powershell
   cd frontend
   npm run build -- --configuration production
   # Genera los archivos en frontend/dist/billiard-frontend/browser
   ```
2. **Copiar a `wwwroot` del Backend & Compilar Executable**:
   ```powershell
   cd ../backend/src/BilliardSystem.API
   dotnet publish -c Release -o ./publish /p:PublishSingleFile=true /p:SelfContained=true -r win-x64
   ```
3. **Ejecución en la PC del Cliente**:
   - Simplemente ejecutar `BilliardSystem.API.exe`.
   - Abrir el navegador en `http://localhost:5000` o en la IP local de la red LAN (ej. `http://192.168.1.100:5000`).

## Variables de Entorno / Configuración (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=billiard_system.db"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```
