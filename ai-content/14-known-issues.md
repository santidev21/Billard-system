# 14 - Problemas Conocidos y Limitaciones (Known Issues)

## Consideraciones Técnicas
1. **Permisos de Cámara USB en Navegador**:
   - En la Player App, los navegadores (Chrome/Edge) requieren contexto seguro (`https://` o `localhost`) para conceder permisos permanentes a `navigator.mediaDevices.getUserMedia()`. Si se accede por IP local en LAN (`http://192.168.x.x`), se debe habilitar el flag `unsafely-treat-insecure-origin-as-secure` en el navegador del cliente o generar un certificado SSL autofirmado local.
2. **Buffer de Video en RAM**:
   - Para duraciones de buffer de 5 minutos en resolución 1080p, la memoria de la pestaña del navegador puede superar los 150 MB. Se recomienda configurar 720p @ 30 FPS en `Settings` para dispositivos con memoria RAM limitada (< 4 GB).
3. **Bloqueo de Sockets por Antivirus/Firewall en Windows**:
   - Algunas configuraciones de Windows Firewall pueden bloquear el puerto 5000 para conexiones entrantes desde otras computadoras o celulares en la misma red LAN. Se debe agregar una regla de entrada en el Firewall para el puerto 5000 durante la instalación.
