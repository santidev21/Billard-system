# Camera Bridge (local LAN)

Runs on the machine that displays the Billard app. It lets the HTTPS app
(`https://billard.santidev21.tech`) use IP cameras that live on the local LAN,
without the vendor cloud app.

```
Browser (HTTPS page) ──► http://localhost:8089 (bridge) ──► go2rtc ──► RTSP cameras
        ▲ loopback is exempt from mixed-content/CORS issues
```

## What it does

1. **Discovers** cameras on the local network by scanning TCP/554 and probing the
   Dahua/Imou `magicBox` CGI.
2. **Normalizes the codec**: Dahua/Imou main streams often use HEVC (H.265), which
   browsers cannot decode over WebRTC. The bridge switches them to H.264 over the
   official CGI (`configManager.cgi`). Setting `CAMERA_AUTO_H264=false` disables it.
3. **Registers** each camera as a stream in go2rtc (RTSP → WebRTC).
4. **Serves** a small HTTP API for the Angular app, with the CORS / Private
   Network Access headers the browser requires, and proxies the go2rtc WebRTC
   signaling under `/go2rtc/*`.

## API

| Endpoint | Description |
|---|---|
| `GET /health` | Liveness + camera count. |
| `GET /cameras` | Discovered cameras (`?refresh=1` forces a rescan). |
| `POST /go2rtc/api/webrtc?src=<id>` | WebRTC signaling proxy (body = SDP offer). |

## Configuration (env)

| Variable | Default | Description |
|---|---|---|
| `BRIDGE_PORT` | `8089` | API port. |
| `BRIDGE_HOST` | `127.0.0.1` | Bind interface. Loopback keeps the bridge off the LAN. |
| `ALLOWED_ORIGINS` | app origins | Comma-separated CORS allow-list (other origins get 403). |
| `GO2RTC_URL` | `http://127.0.0.1:1984` | go2rtc API base URL. |
| `CAMERA_USER` | `admin` | RTSP/API user. |
| `CAMERA_PASSWORD` | *(empty)* | RTSP/API password (Imou “safety code”). |
| `CAMERA_SCAN_SUBNETS` | *(auto)* | Comma-separated CIDRs; empty = derive /24 from host interfaces. |
| `CAMERA_RTSP_PORT` | `554` | RTSP port. |
| `CAMERA_RTSP_PATH` | `/cam/realmonitor?channel=1&subtype=0` | Main stream path. |
| `CAMERA_AUTO_H264` | `true` | Force H.264 on Dahua/Imou main streams. |
| `CAMERA_RESCAN_INTERVAL_MS` | `120000` | Periodic rescan interval. |

## Security

- The bridge binds **127.0.0.1** and only accepts the configured `ALLOWED_ORIGINS`.
- The proxy exposes **only** `/api/webrtc` and `/api/frame.jpeg`; `/api/streams`
  (which contains the RTSP URL with credentials) is **not** reachable.
- go2rtc's API/RTSP listen on loopback; WebRTC (`:8555`) is the only LAN port.
- Never forward these ports from the router to the internet.

## Local development

```bash
node src/index.js
# or, from the repo root:
npm run camera:bridge
```
