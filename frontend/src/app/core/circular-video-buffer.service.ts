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

    this.stream = await navigator.mediaDevices.getUserMedia({
      video: preferredDeviceId ? { deviceId: { exact: preferredDeviceId } } : true,
    });

    this.recorder = new MediaRecorder(this.stream);
    this.recorder.ondataavailable = (event) => {
      if (event.data && event.data.size > 0) {
        this.chunks.push(event.data);
        while (this.chunks.length > this.maxChunks) {
          this.chunks.shift();
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
    const endedAt = Date.now();
    const inWindow = this.chunks.filter((_, i) => i >= this.chunks.length - this.maxChunks);
    const blob = new Blob(inWindow, { type: this.recorder ? 'video/webm' : 'video/mp4' });
    const url = URL.createObjectURL(blob);
    return url;
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