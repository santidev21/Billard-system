'use strict';

const http = require('http');

const cfg = require('./config');
const { runDiscovery, getState } = require('./discovery');

const VERSION = '0.1.0';

/** go2rtc paths the browser is allowed to reach through the bridge. */
const PROXY_ALLOWED = [/^\/api\/webrtc$/, /^\/api\/frame\.jpeg$/];

function isAllowedOrigin(origin) {
  return !origin || cfg.allowedOrigins.includes(origin);
}

/** Map the path to a fixed label so logs never contain raw user input. */
function routeLabel(pathname) {
  if (pathname === '/health') return '/health';
  if (pathname === '/cameras') return '/cameras';
  if (pathname.startsWith('/go2rtc/')) return '/go2rtc/*';
  return 'other';
}

function corsHeaders(req) {
  const origin = req.headers.origin;
  const headers = {
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': req.headers['access-control-request-headers'] || 'Content-Type',
    // Required so a public HTTPS page can reach this loopback service (Chrome PNA).
    'Access-Control-Allow-Private-Network': 'true',
    'Access-Control-Max-Age': '600',
    Vary: 'Origin',
  };
  if (origin && cfg.allowedOrigins.includes(origin)) {
    headers['Access-Control-Allow-Origin'] = origin;
  }
  return headers;
}

function sendJson(req, res, status, body) {
  const data = JSON.stringify(body);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(data),
    ...corsHeaders(req),
  });
  res.end(data);
}

function publicCamera(camera) {
  return {
    id: camera.id,
    name: camera.name,
    kind: 'ip',
    host: camera.host,
    model: camera.model,
    online: camera.online,
    codec: camera.codec,
    streamReady: camera.streamReady === true,
  };
}

/** Reverse-proxy an allow-listed go2rtc path, adding CORS headers. */
function proxy(req, res, parsedUrl) {
  const target = new URL(cfg.go2rtcUrl);
  const path = parsedUrl.pathname.replace(/^\/go2rtc/, '') + parsedUrl.search;
  const headers = { ...req.headers };
  delete headers.host;
  delete headers.origin;
  delete headers.referer;

  const upstream = http.request(
    {
      hostname: target.hostname,
      port: target.port || 80,
      path,
      method: req.method,
      headers,
    },
    (up) => {
      const out = {};
      for (const [key, value] of Object.entries(up.headers)) {
        const lower = key.toLowerCase();
        if (
          lower === 'connection' ||
          lower === 'transfer-encoding' ||
          lower === 'content-length' ||
          lower === 'keep-alive'
        ) {
          continue;
        }
        out[key] = value;
      }
      Object.assign(out, corsHeaders(req));
      res.writeHead(up.statusCode || 502, out);
      up.pipe(res);
    },
  );

  upstream.on('error', (error) => {
    if (!res.headersSent) {
      sendJson(req, res, 502, { error: `go2rtc unreachable: ${error.message}` });
    } else {
      res.end();
    }
  });

  req.pipe(upstream);
}

const server = http.createServer((req, res) => {
  const started = Date.now();
  const parsed = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  res.on('finish', () => {
    // Only log allow-listed constants, never raw request values (avoids log injection).
    const method = ['GET', 'POST', 'OPTIONS'].includes(req.method) ? req.method : 'OTHER';
    const route = routeLabel(parsed.pathname);
    const originState = isAllowedOrigin(req.headers.origin) ? 'allowed' : 'blocked';
    console.log(
      `[bridge] ${method} ${route} -> ${res.statusCode} (${Date.now() - started}ms) origin=${originState}`,
    );
  });

  if (req.method === 'OPTIONS') {
    res.writeHead(204, corsHeaders(req));
    res.end();
    return;
  }

  // Reject calls from origins that are not explicitly allowed.
  if (!isAllowedOrigin(req.headers.origin)) {
    return sendJson(req, res, 403, { error: 'origin not allowed' });
  }

  if (parsed.pathname === '/health') {
    const snapshot = getState();
    return sendJson(req, res, 200, {
      status: 'ok',
      version: VERSION,
      go2rtc: cfg.go2rtcUrl,
      cameras: snapshot.cameras.length,
      scanning: snapshot.scanning,
      updatedAt: snapshot.updatedAt,
    });
  }

  if (parsed.pathname === '/cameras') {
    const snapshot = getState();
    if (parsed.searchParams.get('refresh') === '1') void runDiscovery();
    return sendJson(req, res, 200, {
      cameras: snapshot.cameras.map(publicCamera),
      scanning: snapshot.scanning,
      updatedAt: snapshot.updatedAt,
    });
  }

  if (parsed.pathname.startsWith('/go2rtc/')) {
    const subPath = parsed.pathname.replace(/^\/go2rtc/, '');
    if (!PROXY_ALLOWED.some((re) => re.test(subPath))) {
      return sendJson(req, res, 403, { error: 'path not allowed' });
    }
    return proxy(req, res, parsed);
  }

  return sendJson(req, res, 404, { error: 'not found' });
});

server.listen(cfg.port, cfg.host, () => {
  console.log(
    `[bridge] listening on http://${cfg.host}:${cfg.port} -> go2rtc ${cfg.go2rtcUrl}` +
      ` (origins: ${cfg.allowedOrigins.join(', ') || 'any'})`,
  );
  if (!cfg.cameraPassword) {
    console.warn('[bridge] CAMERA_PASSWORD is empty: RTSP probing/streaming will likely fail.');
  }
  void runDiscovery();
  setInterval(() => void runDiscovery(), cfg.rescanIntervalMs);
});
