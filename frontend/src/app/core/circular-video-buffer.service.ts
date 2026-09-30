import { computed, Injectable, signal } from '@angular/core';

import fixWebmDuration from 'fix-webm-duration';

/** Replay chunk size. 1s makes the counter granular and seeking smoother. */
const CHUNK_DURATION_MS = 1000;
/** Default circular buffer window (3 minutes). Override via localStorage.replayBufferSeconds. */
const DEFAULT_BUFFER_SECONDS = 180;

@Injectable({ providedIn: 'root' })
export class CircularVideoBuffer {
  readonly active = signal(false);
  readonly streamSize = signal(0);
  readonly durationSeconds = computed(() => this.streamSize() * (CHUNK_DURATION_MS / 1000));
  readonly error = signal<string | null>(null);

  private stream: MediaStream | null = null;
  private recorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private startedAt = 0;
  private replayObjectUrl: string | null = null;
  private maxChunks = DEFAULT_BUFFER_SECONDS;

  constructor() {
    try {
      const stored = Number(localStorage.getItem('replayBufferSeconds'));
      if (Number.isFinite(stored) && stored > 0) {
        this.configure(stored);
      }
    } catch {
      // localStorage may be unavailable; keep the default window.
    }
  }

  configure(maxReplaySeconds: number): void {
    this.maxChunks = Math.max(1, Math.floor(maxReplaySeconds / (CHUNK_DURATION_MS / 1000)));
  }

  async start(preferredDeviceId?: string): Promise<void> {
    if (this.active()) {
      return;
    }

    const constraints: MediaStreamConstraints = {
      audio: false,
      video: preferredDeviceId
        ? {
            deviceId: { exact: preferredDeviceId },
            width: { ideal: 1280 },
            height: { ideal: 720 },
            frameRate: { ideal: 24 },
          }
        : { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 24 } },
    };

    const stream = await navigator.mediaDevices.getUserMedia(constraints);
    this.begin(stream);
  }

  /** Record an already-acquired stream (e.g. an IP camera received over WebRTC). */
  startWithStream(stream: MediaStream): void {
    if (this.active()) {
      this.stop();
    }
    this.begin(stream);
  }

  private begin(stream: MediaStream): void {
    this.stream = stream;
    this.error.set(null);
    this.startedAt = Date.now();
    this.recorder = new MediaRecorder(stream, this.recorderOptions());
    this.recorder.onstart = () =>
      console.log('[buffer] MediaRecorder started', stream.getTracks().length, 'track(s)');
    this.recorder.onerror = (event) => {
      const detail = (event as unknown as { error?: DOMException }).error;
      const message = detail?.message || 'Error de grabación';
      console.error('[buffer] MediaRecorder error', detail);
      this.error.set(message);
    };
    this.recorder.ondataavailable = (event) => {
      if (event.data && event.data.size > 0) {
        this.chunks.push(event.data);
        // Keep the first chunk (WebM init segment) and slide the rest, otherwise
        // the concatenated blob loses its initialization data and won't decode.
        while (this.chunks.length > this.maxChunks) {
          this.chunks.splice(1, this.chunks.length - this.maxChunks);
        }
        this.streamSize.set(this.chunks.length);
      }
    };
    this.recorder.start(CHUNK_DURATION_MS);
    this.active.set(true);
  }

  private recorderOptions(): MediaRecorderOptions {
    const options: MediaRecorderOptions = { videoBitsPerSecond: 1_500_000 };
    // Prefer VP8/WebM so the recorded chunks match the `video/webm` replay blob.
    if (
      typeof MediaRecorder !== 'undefined' &&
      MediaRecorder.isTypeSupported?.('video/webm;codecs=vp8')
    ) {
      options.mimeType = 'video/webm;codecs=vp8';
    }
    return options;
  }

  /** Current live stream, if recording. Does not start anything. */
  currentStream(): MediaStream | null {
    return this.stream;
  }

  async captureFrame(): Promise<string | null> {
    // Flush pending media data so the resulting WebM has a complete init segment
    // and is actually playable while the recorder keeps running.
    if (this.recorder && this.recorder.state !== 'inactive') {
      try {
        this.recorder.requestData();
      } catch {
        // recorder may not support requestData; continue with existing chunks
      }
      await new Promise<void>((resolve) => setTimeout(resolve, 150));
    }
    const inWindow = this.chunks.filter((_, i) => i >= this.chunks.length - this.maxChunks);
    if (inWindow.length === 0) {
      return null;
    }
    const mimeType = this.recorder?.mimeType || 'video/webm';
    return this.buildReplayUrl(inWindow, mimeType);
  }

  /**
   * Build a playable, seekable clip from the ring buffer.
   *
   * Once the window slides, the retained chunks contain the WebM init segment
   * (chunk 0) plus a jump in timestamps, which makes the naive blob unplayable
   * and wrongly long. `MediaSource` in `sequence` mode re-times the appended
   * segments back-to-back, producing a continuous clip with a real duration.
   */
  private async buildReplayUrl(chunks: Blob[], mimeType: string): Promise<string | null> {
    this.revokeReplayUrl();

    if (typeof MediaSource === 'undefined' || !MediaSource.isTypeSupported(mimeType)) {
      return await this.buildBlobUrl(chunks, mimeType);
    }

    const source = new MediaSource();
    const url = URL.createObjectURL(source);
    this.replayObjectUrl = url;

    source.addEventListener('sourceopen', () => {
      let buffer: SourceBuffer;
      try {
        buffer = source.addSourceBuffer(mimeType);
        buffer.mode = 'sequence';
      } catch {
        return;
      }

      // Append the init segment, then the whole contiguous window as one block.
      const segments = [new Blob([chunks[0]], { type: mimeType })];
      if (chunks.length > 1) {
        segments.push(new Blob(chunks.slice(1), { type: mimeType }));
      }

      let index = 0;
      const appendNext = async (): Promise<void> => {
        if (index >= segments.length) {
          try {
            const buffered = buffer.buffered;
            if (buffered.length > 0) {
              source.duration = buffered.end(buffered.length - 1);
            }
          } catch {
            // Some browsers reject setting duration; the clip still plays.
          }
          try {
            if (source.readyState === 'open') {
              source.endOfStream();
            }
          } catch {
            // ignore
          }
          return;
        }
        const segment = segments[index++];
        let data: ArrayBuffer;
        try {
          data = await segment.arrayBuffer();
        } catch {
          void appendNext();
          return;
        }
        const onUpdate = (): void => {
          buffer.removeEventListener('updateend', onUpdate);
          void appendNext();
        };
        buffer.addEventListener('updateend', onUpdate);
        try {
          buffer.appendBuffer(data);
        } catch {
          buffer.removeEventListener('updateend', onUpdate);
          void appendNext();
        }
      };
      void appendNext();
    });

    return url;
  }

  private async buildBlobUrl(chunks: Blob[], mimeType: string): Promise<string | null> {
    const raw = new Blob(chunks, { type: mimeType });
    const durationMs = this.startedAt
      ? Date.now() - this.startedAt
      : chunks.length * CHUNK_DURATION_MS;
    try {
      const fixed = await fixWebmDuration(raw, durationMs, { logger: false });
      return URL.createObjectURL(fixed);
    } catch {
      return URL.createObjectURL(raw);
    }
  }

  private revokeReplayUrl(): void {
    if (this.replayObjectUrl) {
      URL.revokeObjectURL(this.replayObjectUrl);
      this.replayObjectUrl = null;
    }
  }

  stop(): void {
    this.recorder?.stop();
    this.stream?.getTracks().forEach((track) => track.stop());
    this.recorder = null;
    this.stream = null;
    this.chunks = [];
    this.streamSize.set(0);
    this.active.set(false);
    this.revokeReplayUrl();
  }

  captureStreamDurationSeconds(): number {
    return this.chunks.length * (CHUNK_DURATION_MS / 1000);
  }
}
