import { Injectable, signal } from '@angular/core';

const CHUNK_DURATION_MS = 2000;

@Injectable({ providedIn: 'root' })
export class CircularVideoBuffer {
  readonly active = signal(false);
  readonly streamSize = signal(0);

  private stream: MediaStream | null = null;
  private recorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private maxChunks = Math.floor(60 / 2); // default 60s buffer

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
        ? { deviceId: { exact: preferredDeviceId }, width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 24 } }
        : { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 24 } },
    };

    this.stream = await navigator.mediaDevices.getUserMedia(constraints);

    this.recorder = new MediaRecorder(this.stream, { videoBitsPerSecond: 1_500_000 });
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

  public async activeStream(): Promise<MediaStream | null> {
    if (!this.stream) {
      await this.start();
    }
    return this.stream ?? null;
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
    const blob = new Blob(inWindow, { type: 'video/webm' });
    return URL.createObjectURL(blob);
  }

  stop(): void {
    this.recorder?.stop();
    this.stream?.getTracks().forEach((track) => track.stop());
    this.recorder = null;
    this.stream = null;
    this.chunks = [];
    this.streamSize.set(0);
    this.active.set(false);
  }

  captureStreamDurationSeconds(): number {
    return this.chunks.length * (CHUNK_DURATION_MS / 1000);
  }
}