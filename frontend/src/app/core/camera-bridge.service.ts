import { Injectable, signal } from '@angular/core';

export interface IpCamera {
  id: string;
  name: string;
  host: string;
  model: string;
  online: boolean;
  codec: string;
  streamReady: boolean;
}

interface BridgeCamerasResponse {
  cameras: IpCamera[];
  scanning: boolean;
  updatedAt: string | null;
}

/**
 * Loopback bridge addresses are tried in order. `localhost`/`127.0.0.1` are
 * secure contexts, so an HTTPS page can reach them (loopback is exempt from
 * mixed-content restrictions). Override with `localStorage.cameraBridgeUrl`.
 */
const PROBE_TIMEOUT_MS = 2500;
const REQUEST_TIMEOUT_MS = 20000;

function withTimeout(ms: number): { signal: AbortSignal; cancel: () => void } {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), ms);
  return { signal: controller.signal, cancel: () => clearTimeout(timer) };
}

/** Talks to the local camera-bridge: discovery list + go2rtc WebRTC signaling. */
@Injectable({ providedIn: 'root' })
export class CameraBridgeService {
  readonly available = signal(false);
  readonly scanning = signal(false);
  readonly cameras = signal<IpCamera[]>([]);
  readonly error = signal<string | null>(null);

  private peer: RTCPeerConnection | null = null;
  private stream: MediaStream | null = null;

  private resolvedUrl: string | null = null;

  get baseUrl(): string {
    return localStorage.getItem('cameraBridgeUrl') ?? this.resolvedUrl ?? 'http://127.0.0.1:8089';
  }

  /** Probe the bridge (trying loopback aliases) and load the discovered cameras. */
  async probe(): Promise<boolean> {
    const custom = localStorage.getItem('cameraBridgeUrl');
    const candidates = custom ? [custom] : ['http://127.0.0.1:8089', 'http://localhost:8089'];
    for (const url of candidates) {
      const { signal, cancel } = withTimeout(PROBE_TIMEOUT_MS);
      try {
        const res = await fetch(`${url}/health`, { signal });
        if (!res.ok) {
          continue;
        }
        this.resolvedUrl = url;
        this.available.set(true);
        cancel();
        await this.refresh();
        return true;
      } catch {
        // Try the next candidate.
      } finally {
        cancel();
      }
    }
    this.available.set(false);
    this.cameras.set([]);
    return false;
  }

  /** Load the discovered cameras. `force` triggers a LAN rescan on the bridge. */
  async refresh(force = false): Promise<void> {
    this.scanning.set(force);
    const { signal, cancel } = withTimeout(REQUEST_TIMEOUT_MS);
    try {
      const suffix = force ? '?refresh=1' : '';
      const res = await fetch(`${this.baseUrl}/cameras${suffix}`, { signal });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      const data = (await res.json()) as BridgeCamerasResponse;
      this.cameras.set(data.cameras ?? []);
      this.available.set(true);
      this.error.set(null);
    } catch {
      this.error.set('No se pudo consultar las cámaras de la red local.');
    } finally {
      this.scanning.set(false);
      cancel();
    }
  }

  /** Open a WebRTC connection to a bridge stream and return its MediaStream. */
  async connect(id: string): Promise<MediaStream> {
    this.disconnect();
    const peer = new RTCPeerConnection({ iceServers: [] });
    this.peer = peer;
    const stream = new MediaStream();
    this.stream = stream;

    peer.addTransceiver('video', { direction: 'recvonly' });
    // Audio intentionally omitted: the live view is muted and recording it adds
    // codec friction (the camera sends PCMU, which MediaRecorder must transcode).
    peer.ontrack = (event) => {
      if (event.track && !stream.getTracks().includes(event.track)) {
        stream.addTrack(event.track);
      }
    };

    const offer = await peer.createOffer();
    await peer.setLocalDescription(offer);
    await this.waitForIce(peer);

    const res = await fetch(`${this.baseUrl}/go2rtc/api/webrtc?src=${encodeURIComponent(id)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/sdp' },
      body: peer.localDescription?.sdp ?? '',
    });
    if (!res.ok) {
      this.disconnect();
      throw new Error(`WebRTC signaling failed (HTTP ${res.status})`);
    }
    const answer = await res.text();
    await peer.setRemoteDescription({ type: 'answer', sdp: answer });
    // MediaRecorder must not start on an empty stream: wait for the live video track.
    await this.waitForLiveVideo(stream);
    return stream;
  }

  disconnect(): void {
    if (this.peer) {
      this.peer.getReceivers().forEach((receiver) => receiver.track?.stop());
      this.peer.close();
      this.peer = null;
    }
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
  }

  /** Wait briefly for the WebRTC video track to become live before recording. */
  private async waitForLiveVideo(stream: MediaStream, timeoutMs = 6000): Promise<void> {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
      const track = stream.getVideoTracks()[0];
      if (track && track.readyState === 'live') {
        return;
      }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  }

  /** Wait briefly for ICE gathering so the offer carries host candidates. */
  private waitForIce(peer: RTCPeerConnection, timeoutMs = 2000): Promise<void> {
    if (peer.iceGatheringState === 'complete') {
      return Promise.resolve();
    }
    return new Promise((resolve) => {
      const done = (): void => {
        clearTimeout(timer);
        peer.removeEventListener('icegatheringstatechange', onChange);
        resolve();
      };
      const onChange = (): void => {
        if (peer.iceGatheringState === 'complete') {
          done();
        }
      };
      const timer = setTimeout(done, timeoutMs);
      peer.addEventListener('icegatheringstatechange', onChange);
    });
  }
}
