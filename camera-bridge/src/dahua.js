'use strict';

const crypto = require('crypto');

const cfg = require('./config');

function md5(value) {
  // MD5 is mandated by HTTP Digest authentication (RFC 2617), which the camera
  // web API requires. It is not used to store passwords. The weak-hash query is
  // excluded for this file in .github/codeql/codeql-config.yml.
  return crypto.createHash('md5').update(value).digest('hex');
}

/** Parse a `WWW-Authenticate: Digest ...` header into a lower-cased map. */
function parseDigest(header) {
  const out = {};
  const source = String(header || '').replace(/^Digest\s+/i, '');
  const re = /([a-zA-Z0-9_-]+)\s*=\s*(?:"([^"]*)"|([^,]*))/g;
  let match;
  while ((match = re.exec(source))) {
    out[match[1].toLowerCase()] = match[2] !== undefined ? match[2] : (match[3] || '').trim();
  }
  return out;
}

/**
 * HTTP GET with RFC 2617 Digest authentication. Returns the final Response.
 * The camera web/API is only reachable with Digest, so this is the smallest
 * helper that gets us device info and the encoder configuration.
 */
async function digestGet(urlStr, timeoutMs = 4000) {
  const url = new URL(urlStr);
  const request = (headers) =>
    fetch(url, { method: 'GET', headers, signal: AbortSignal.timeout(timeoutMs), redirect: 'manual' });

  let res = await request();
  if (res.status !== 401) return res;

  const challenge = parseDigest(res.headers.get('www-authenticate'));
  if (!challenge.realm || !challenge.nonce) return res;

  const uri = url.pathname + url.search;
  const nc = '00000001';
  const cnonce = crypto.randomBytes(8).toString('hex');
  const qop = (challenge.qop || '').split(',')[0].trim();
  const ha1 = md5(`${cfg.cameraUser}:${challenge.realm}:${cfg.cameraPassword}`);
  const ha2 = md5(`GET:${uri}`);
  const response = qop
    ? md5(`${ha1}:${challenge.nonce}:${nc}:${cnonce}:${qop}:${ha2}`)
    : md5(`${ha1}:${challenge.nonce}:${ha2}`);

  let authorization =
    `Digest username="${cfg.cameraUser}", realm="${challenge.realm}", nonce="${challenge.nonce}", ` +
    `uri="${uri}", response="${response}"`;
  if (challenge.opaque) authorization += `, opaque="${challenge.opaque}"`;
  if (qop) authorization += `, qop=${qop}, nc=${nc}, cnonce="${cnonce}"`;

  res = await request({ Authorization: authorization });
  return res;
}

function parseKeyValues(text) {
  const out = {};
  for (const line of String(text || '').split('\n')) {
    const idx = line.indexOf('=');
    if (idx > 0) out[line.slice(0, idx).trim()] = line.slice(idx + 1).trim();
  }
  return out;
}

/** Dahua/Imou device type + serial (works on Imou: type=IPC-G26E). */
async function getDeviceInfo(host) {
  const base = `http://${host}/cgi-bin/magicBox.cgi?action=`;
  const [typeRes, snRes] = await Promise.all([
    digestGet(`${base}getDeviceType`).catch(() => null),
    digestGet(`${base}getSerialNo`).catch(() => null),
  ]);
  const type = typeRes && typeRes.ok ? parseKeyValues(await typeRes.text()).type : '';
  const serial = snRes && snRes.ok ? parseKeyValues(await snRes.text()).sn : '';
  return { type: type || '', serial: serial || '' };
}

/**
 * Ensure the camera main stream encodes H.264 (browsers cannot decode HEVC
 * over WebRTC). Returns { supported, previous, codec, changed }.
 */
async function ensureH264(host) {
  const getUrl = `http://${host}/cgi-bin/configManager.cgi?action=getConfig&name=Encode`;
  const res = await digestGet(getUrl);
  if (!res.ok) return { supported: false };
  const text = await res.text();
  const match = /MainFormat\[0\]\.Video\.Compression=([^\s]+)/.exec(text);
  if (!match) return { supported: false };
  const previous = match[1];
  if (previous === 'H.264') return { supported: true, previous, codec: 'H.264', changed: false };

  const setUrl =
    `http://${host}/cgi-bin/configManager.cgi?action=setConfig&` +
    'Encode[0].MainFormat[0].Video.Compression=H.264';
  const setRes = await digestGet(setUrl);
  const body = await setRes.text().catch(() => '');
  return { supported: true, previous, codec: 'H.264', changed: setRes.ok && /OK/i.test(body) };
}

module.exports = { digestGet, getDeviceInfo, ensureH264, parseDigest };
