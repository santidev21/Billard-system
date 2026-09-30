# 15 - Cámaras IP en Red Local (IP Cameras / Local Bridge)

Soporte para cámaras IP (Imou/Dahua, Hikvision, etc.) sin depender de la app del
fabricante. El navegador no reproduce RTSP, y una página HTTPS no puede hablar
directo con una IP local, así que se interpone un **bridge local**.

## Por qué existe el bridge

```
Navegador (página HTTPS de billard.santidev21.tech)
      │  sólo habla con localhost  ✅ (loopback = contexto seguro, exento de mixed-content)
      ▼
camera-bridge  (http://localhost:8089, corre en la PC del local)
      │  descubre + normaliza códec + registra streams
      ▼
go2rtc  (:1984 API · :8554 RTSP · :8555 WebRTC)  ──RTSP──►  cámaras IP (192.168.x.x)
```

Reglas del navegador que condicionan el diseño:
- Mixed content: una página HTTPS no puede `fetch` a `http://192.168.x.x`, pero
  **sí** a `http://localhost` (loopback es *potentially trustworthy*).
- CORS + Private Network Access: el bridge agrega
  `Access-Control-Allow-Origin` y `Access-Control-Allow-Private-Network: true`.
- CSP: el nginx del VPS debe permitir `connect-src http://localhost:* http://127.0.0.1:*`.

## Componentes

1. **`camera-bridge/`** (Node, sin dependencias):
   - `src/discovery.js` — escanea TCP/554 en la LAN (prefiere la interfaz de la
     ruta por defecto para ignorar redes Docker/VPN) y registra cada cámara en
     go2rtc (`PUT /api/streams`).
   - `src/dahua.js` — cliente HTTP con **Digest auth**; lee el tipo de equipo
     (`magicBox.cgi`) y **fuerza H.264** en el stream principal
     (`configManager.cgi`) porque los navegadores no decodifican HEVC por WebRTC.
   - `src/index.js` — API HTTP + proxy CORS hacia go2rtc (`/go2rtc/*`), incluido
     el signaling WebRTC (`POST /api/webrtc`).
2. **go2rtc** (contenedor oficial) — convierte RTSP → WebRTC/MSE. Los streams se
   registran en runtime y se persisten en el volumen `go2rtc-config`.
3. **Frontend Angular**:
   - `core/camera-bridge.service.ts` — detecta el bridge, lista cámaras, negocia WebRTC.
   - `core/camera.service.ts` — unifica fuentes: **dispositivo** (`getUserMedia`) e **IP**.
   - `features/player/camera-view.component.*` — selector de fuente + botón de reescaneo.
   - La repetición instantánea no cambia: el `MediaStream` de WebRTC se pasa al
     `CircularVideoBuffer` (mismo `MediaRecorder`).

## API del bridge

| Endpoint | Descripción |
|---|---|
| `GET /health` | Estado + cantidad de cámaras. |
| `GET /cameras` | Cámaras descubiertas (`?refresh=1` fuerza reescaneo). |
| `POST /go2rtc/api/webrtc?src=<id>` | Signaling WebRTC (body = SDP offer). |

## Configuración

Variables en `.env` (ver `.env.example`):

| Variable | Default | Descripción |
|---|---|---|
| `CAMERA_USER` | `admin` | Usuario RTSP/API. |
| `CAMERA_PASSWORD` | — | Password RTSP/API (en Imou es el *safety code*). |
| `CAMERA_SCAN_SUBNETS` | auto | CIDRs separados por coma; vacío = derivar /24 de las interfaces. |
| `CAMERA_AUTO_H264` | `true` | Forzar H.264 en cámaras Dahua/Imou. |

## Seguridad

- El bridge escucha en **127.0.0.1** (no en la LAN) y solo acepta los `ALLOWED_ORIGINS` configurados.
- El proxy expone **solo** `/api/webrtc` y `/api/frame.jpeg`. `/api/streams`
  (que contiene la URL RTSP con credenciales) queda **bloqueado**.
- La API/RTSP de go2rtc escuchan en loopback; WebRTC (`:8555`) es el único
  puerto en la LAN.
- Nunca reenviar estos puertos desde el router hacia internet.

## Puesta en marcha

```bash
# en la PC del local (misma LAN que las cámaras)
cp .env.example .env      # completar CAMERA_PASSWORD
npm run camera:up         # docker compose -f docker-compose.camera.yml up -d --build
npm run camera:logs
```

La base de datos y la app **no** se levantan con este compose: es sólo el bridge
de cámara y usa `network_mode: host`.

## Limitaciones conocidas

1. **Misma PC**: el navegador sólo alcanza el bridge por `localhost`. Abrir la
   app desde otro dispositivo de la LAN requeriría servir el bridge por HTTPS con
   un certificado de confianza (pendiente).
2. **Puertos**: go2rtc usa `1984`, `8554`, `8555` y el bridge `8089` en la PC.
3. **CSP**: si el CSP del gateway no incluye `http://localhost:*`, el navegador
   bloquea las llamadas al bridge. Ya actualizado en `deploy/nginx-billard.conf`.
4. **H.264**: la app de Imou puede revertir el encoder; el bridge lo vuelve a
   forzar en cada reescaneo (cada 2 min).
5. **Cámaras no-Dahua**: se listan si exponen RTSP/554, pero sin auto-H.264 ni
   detección de modelo (se asume HEVC/H.264 configurable a mano).
