'use strict';

const fs = require('fs');
const net = require('net');
const os = require('os');

const cfg = require('./config');
const { getDeviceInfo, ensureH264 } = require('./dahua');

const state = {
  /** Discovered cameras (includes the RTSP url with credentials, never exposed). */
  cameras: [],
  scanning: false,
  updatedAt: null,
};

function getState() {
  return state;
}

function intToIp(value) {
  return [24, 16, 8, 0].map((shift) => (value >>> shift) & 255).join('.');
}

/** Expand a CIDR into host addresses (capped to avoid runaway scans). */
function expandCidr(cidr) {
  const match = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})(?:\/(\d{1,2}))?$/.exec(cidr.trim());
  if (!match) return [];
  const ip =
    ((Number(match[1]) << 24) |
      (Number(match[2]) << 16) |
      (Number(match[3]) << 8) |
      Number(match[4])) >>>
    0;
  const prefix = match[5] === undefined ? 24 : Number(match[5]);
  if (prefix < 16 || prefix > 30) return [];
  const mask = (0xffffffff << (32 - prefix)) >>> 0;
  const network = (ip & mask) >>> 0;
  const size = 2 ** (32 - prefix);
  const total = Math.min(size, 4096);
  const hosts = [];
  for (let i = 1; i < total - 1; i++) hosts.push(intToIp((network + i) >>> 0));
  return hosts;
}

/** Interface that owns the default route (the real LAN), if we can detect it. */
function defaultRouteInterface() {
  try {
    const lines = fs.readFileSync('/proc/net/route', 'utf8').split('\n').slice(1);
    for (const line of lines) {
      const cols = line.trim().split(/\s+/);
      if (cols.length >= 2 && cols[1] === '00000000') return cols[0];
    }
  } catch {
    // Not Linux or /proc unavailable — fall back to interface heuristics.
  }
  return null;
}

function isUsableAddress(addr, name) {
  if (addr.family !== 'IPv4' || addr.internal) return false;
  if (addr.address.startsWith('169.254.')) return false;
  return !/^(docker|br-|veth|virbr|vmnet|tap|tun)/.test(name);
}

function subnetOf(address) {
  const parts = address.split('.');
  return `${parts[0]}.${parts[1]}.${parts[2]}.0/24`;
}

/**
 * Derive /24 subnets from the host's LAN interfaces. Prefers the default-route
 * interface so Docker/VPN bridges (172.x, docker0, br-*, …) are ignored.
 */
function defaultSubnets() {
  const interfaces = os.networkInterfaces();
  const preferred = defaultRouteInterface();
  if (preferred && interfaces[preferred]) {
    const defaultSubnets = (interfaces[preferred] || [])
      .filter((addr) => isUsableAddress(addr, preferred))
      .map((addr) => subnetOf(addr.address));
    if (defaultSubnets.length) return [...new Set(defaultSubnets)];
  }
  const found = new Set();
  for (const [name, addrs] of Object.entries(interfaces)) {
    for (const addr of addrs || []) {
      if (isUsableAddress(addr, name)) found.add(subnetOf(addr.address));
    }
  }
  return [...found];
}

function tcpProbe(host, port, timeoutMs) {
  return new Promise((resolve) => {
    const socket = new net.Socket();
    let settled = false;
    const finish = (ok) => {
      if (settled) return;
      settled = true;
      socket.destroy();
      resolve(ok);
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', () => finish(false));
    socket.connect(port, host);
  });
}

async function mapLimit(items, limit, fn) {
  const out = new Array(items.length);
  let index = 0;
  async function worker() {
    while (index < items.length) {
      const current = index++;
      try {
        out[current] = await fn(items[current]);
      } catch {
        out[current] = undefined;
      }
    }
  }
  const workers = Array.from({ length: Math.min(limit, items.length) }, worker);
  await Promise.all(workers);
  return out;
}

async function scanHosts(hosts, port, concurrency, timeoutMs) {
  const results = await mapLimit(hosts, concurrency, async (host) => {
    return (await tcpProbe(host, port, timeoutMs)) ? host : null;
  });
  return results.filter(Boolean);
}

function cameraIdFor(host) {
  return 'ip-' + host.replace(/\./g, '-');
}

function rtspUrlFor(host) {
  const user = encodeURIComponent(cfg.cameraUser);
  const password = encodeURIComponent(cfg.cameraPassword);
  return `rtsp://${user}:${password}@${host}:${cfg.rtspPort}${cfg.rtspPath}`;
}

async function go2rtcStreams() {
  try {
    const res = await fetch(`${cfg.go2rtcUrl}/api/streams`, { signal: AbortSignal.timeout(4000) });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

async function go2rtcPutStream(name, src) {
  const url = `${cfg.go2rtcUrl}/api/streams?name=${encodeURIComponent(name)}&src=${encodeURIComponent(src)}`;
  try {
    const res = await fetch(url, { method: 'PUT', signal: AbortSignal.timeout(4000) });
    return res.ok;
  } catch {
    return false;
  }
}

async function go2rtcDeleteStream(name) {
  try {
    await fetch(`${cfg.go2rtcUrl}/api/streams?name=${encodeURIComponent(name)}`, {
      method: 'DELETE',
      signal: AbortSignal.timeout(4000),
    });
  } catch {
    // go2rtc may be restarting; the periodic reconcile will fix it.
  }
}

/** Register every discovered camera in go2rtc and drop the stale ones. */
async function reconcileStreams(cameras) {
  const existing = await go2rtcStreams();
  if (existing) {
    const wanted = new Set(cameras.map((camera) => camera.id));
    for (const name of Object.keys(existing)) {
      if (name.startsWith('ip-') && !wanted.has(name)) {
        await go2rtcDeleteStream(name);
      }
    }
  }
  await mapLimit(cameras, 4, async (camera) => {
    camera.streamReady = await go2rtcPutStream(camera.id, camera.rtsp);
  });
}

async function probeCamera(host) {
  const info = await getDeviceInfo(host).catch(() => ({ type: '', serial: '' }));
  let codec = '';
  let changed = false;
  if (cfg.autoH264 && info.type) {
    const result = await ensureH264(host).catch(() => null);
    if (result && result.supported) {
      codec = result.codec;
      changed = result.changed;
    }
  }
  const model = info.type || 'IP Camera';
  return {
    id: cameraIdFor(host),
    host,
    name: `${model} · ${host}`,
    model: info.type || '',
    serial: info.serial || '',
    codec,
    h264Fixed: changed,
    online: true,
    rtsp: rtspUrlFor(host),
  };
}

let running = null;

/** Scan the LAN, ensure H.264 and (re)register the streams. Safe to call often. */
async function runDiscovery() {
  if (running) return running;
  running = (async () => {
    state.scanning = true;
    try {
      const subnets = cfg.scanSubnets.length ? cfg.scanSubnets : defaultSubnets();
      const hosts = [...new Set(subnets.flatMap(expandCidr))];
      const open = await scanHosts(hosts, cfg.rtspPort, cfg.scanConcurrency, cfg.scanTimeoutMs);
      const cameras = (await mapLimit(open, 8, probeCamera)).filter(Boolean);
      cameras.sort((a, b) => a.host.localeCompare(b.host, undefined, { numeric: true }));
      state.cameras = cameras;
      state.updatedAt = new Date().toISOString();
      await reconcileStreams(cameras);
      console.log(
        `[bridge] discovery: ${cameras.length} camera(s) on ${subnets.join(', ')}` +
          (cameras.length ? ` -> ${cameras.map((c) => `${c.host}${c.h264Fixed ? ' (H.264 fixed)' : ''}`).join(', ')}` : ''),
      );
    } finally {
      state.scanning = false;
      running = null;
    }
  })();
  return running;
}

module.exports = { runDiscovery, getState, expandCidr, defaultSubnets };
