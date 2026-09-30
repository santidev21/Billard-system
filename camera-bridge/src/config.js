'use strict';

function env(name, fallback) {
  const value = process.env[name];
  return value === undefined || value === '' ? fallback : value;
}

module.exports = {
  /** Port the bridge HTTP API listens on (reachable as http://localhost:<port>). */
  port: Number(env('BRIDGE_PORT', '8089')),

  /** Interface the bridge binds to. Loopback by default so the LAN can't reach it. */
  host: env('BRIDGE_HOST', '127.0.0.1'),

  /**
   * Origins allowed to call the bridge. Anything else is rejected (no CORS header
   * + 403), so random websites cannot talk to the local bridge.
   */
  allowedOrigins: env(
    'ALLOWED_ORIGINS',
    'https://billard.santidev21.tech,http://localhost:4200,http://127.0.0.1:4200,http://localhost:5000,http://127.0.0.1:5000',
  )
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean),

  /** Internal go2rtc API base URL. */
  go2rtcUrl: env('GO2RTC_URL', 'http://127.0.0.1:1984').replace(/\/+$/, ''),

  /** RTSP credentials used to probe and stream the cameras. */
  cameraUser: env('CAMERA_USER', 'admin'),
  cameraPassword: env('CAMERA_PASSWORD', ''),

  /** Optional comma-separated CIDR list. Empty = derive /24 from host interfaces. */
  scanSubnets: env('CAMERA_SCAN_SUBNETS', '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean),

  /** RTSP port and main-stream path for the discovered cameras. */
  rtspPort: Number(env('CAMERA_RTSP_PORT', '554')),
  rtspPath: env('CAMERA_RTSP_PATH', '/cam/realmonitor?channel=1&subtype=0'),

  /** When true the bridge switches Dahua/Imou main streams to H.264 automatically. */
  autoH264: env('CAMERA_AUTO_H264', 'true') === 'true',

  /** Scan tuning. */
  scanConcurrency: Number(env('CAMERA_SCAN_CONCURRENCY', '128')),
  scanTimeoutMs: Number(env('CAMERA_SCAN_TIMEOUT_MS', '600')),
  rescanIntervalMs: Number(env('CAMERA_RESCAN_INTERVAL_MS', '120000')),
};
