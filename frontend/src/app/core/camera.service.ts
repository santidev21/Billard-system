import { inject, Injectable, signal } from '@angular/core';

import { CircularVideoBuffer } from './circular-video-buffer.service';

@Injectable({ providedIn: 'root' })
export class CameraService {
  private readonly buffer = inject(CircularVideoBuffer);

  readonly available = signal(false);
  readonly cameraOn = signal(false);
  readonly devices = signal<MediaDeviceInfo[]>([]);
  readonly selectedDeviceId = signal('');
  readonly error = signal<string | null>(null);

  private initialized = false;

  async init(): Promise<void> {
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    if (!navigator.mediaDevices?.enumerateDevices) {
      return;
    }

    await this.enumerateDevices();

    if (navigator.mediaDevices.addEventListener) {
      navigator.mediaDevices.addEventListener('devicechange', () => {
        this.enumerateDevices();
      });
    }
  }

  private async enumerateDevices(): Promise<void> {
    try {
      const all = await navigator.mediaDevices.enumerateDevices();
      const videoDevices = all.filter((d) => d.kind === 'videoinput');
      this.devices.set(videoDevices);

      if (videoDevices.length === 0) {
        this.available.set(false);
        return;
      }

      this.available.set(true);
      if (!this.selectedDeviceId() || !videoDevices.find((d) => d.deviceId === this.selectedDeviceId())) {
        this.selectedDeviceId.set(videoDevices[0].deviceId);
      }
    } catch {
      this.available.set(false);
    }
  }

  async toggle(): Promise<void> {
    if (this.cameraOn()) {
      this.stop();
      return;
    }

    this.error.set(null);
    try {
      await this.buffer.start(this.selectedDeviceId() || undefined);
      this.cameraOn.set(true);
    } catch (e: any) {
      this.cameraOn.set(false);
      if (e?.name === 'NotAllowedError') {
        this.error.set('Permiso de cámara denegado. Permití el acceso en el navegador.');
      } else if (e?.name === 'NotFoundError') {
        this.error.set('No se encontró ninguna cámara.');
        this.available.set(false);
      } else if (e?.name === 'NotReadableError') {
        this.error.set('Cámara en uso por otra aplicación.');
      } else {
        this.error.set('No se pudo acceder a la cámara.');
      }
    }
  }

  stop(): void {
    this.buffer.stop();
    this.cameraOn.set(false);
    this.error.set(null);
  }

  async activeStream(): Promise<MediaStream | null> {
    return this.buffer.activeStream();
  }

  async captureFrame(): Promise<string | null> {
    return this.buffer.captureFrame();
  }

  onDeviceChange(deviceId: string): void {
    this.selectedDeviceId.set(deviceId);
    if (this.cameraOn()) {
      this.stop();
      void this.toggle();
    }
  }
}
