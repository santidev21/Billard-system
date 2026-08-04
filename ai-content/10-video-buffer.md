# 10 - Buffer Circular de Video y Repetición (Video Buffer & Instant Replay)

## Arquitectura de Captura en Memoria
Para garantizar la repetición instantánea sin interrumpir la grabación de la mesa en vivo:

```
[ Cámara USB ]
      |
      v  navigator.mediaDevices.getUserMedia()
[ HTML5 Video Element (Live Feed) ]
      |
      v  MediaRecorder API (fragmentos de 2 segundos)
+-------------------------------------------------------------+
| Circular Video Buffer (RAM Array - Ventana Flotante Config) |
| [Chunk 1] [Chunk 2] ... [Chunk N (Máx 30s / 1m / 2m / 5m)]  |
+-------------------------------------------------------------+
      | (Al presionar "Ver Repetición")
      v
[ Blob / MediaSource Synthesis ]
      |
      v
[ HTML5 Replay Video Player (Pause, 0.25x, 0.5x, Frame Step) ]
```

## Componentes Técnicos
1. **`CircularVideoBuffer` (Servicio Angular)**:
   - Mantiene una cola en memoria RAM con los últimos fragmentos WebM/MP4 de 2000 ms.
   - El número de fragmentos mantenidos en RAM se calcula dinámicamente según la duración máxima de replay configurada (`MaxReplaySeconds`):
     - 30 segundos = 15 fragmentos.
     - 60 segundos (1 min) = 30 fragmentos.
     - 120 segundos (2 min) = 60 fragmentos.
     - 180 segundos (3 min) = 90 fragmentos.
     - 300 segundos (5 min) = 150 fragmentos.
2. **Grabación Ininterrumpida**:
   - La instancia de `MediaRecorder` emite el evento `ondataavailable` cada 2000 ms.
   - Estos fragmentos se añaden al arreglo en RAM y el fragmento más antiguo que sobrepase la ventana de tiempo es descartado con Garbage Collection.
3. **Reproducción de Repetición**:
   - Cuando el usuario solicita "Ver Repetición" (ej. últimos 30 segundos), el servicio toma los últimos 15 fragmentos de la cola, construye un `new Blob(chunks, { type: 'video/webm' })` y asigna la URL `URL.createObjectURL(blob)` al reproductor de repetición modal.
   - Mientras el jugador analiza la jugada a velocidad `0.5x` o `0.25x`, la instancia de `MediaRecorder` en segundo plano **sigue capturando y reteniendo** nuevos fragmentos sin ninguna interrupción.

## Consumo Estimado de Memoria RAM
- Calidad 720p @ 30 FPS: ~1.5 MB por cada 10 segundos de video.
- Buffer de 3 minutos: ~27 MB en RAM.
- Buffer de 5 minutos: ~45 MB en RAM.
- Consumo óptimo y seguro para PCs locales de baja o mediana gama.
