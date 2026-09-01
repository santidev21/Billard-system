import { Component, inject, OnDestroy, OnInit, signal, ViewChild, ElementRef } from '@angular/core';

import { CircularVideoBuffer } from '../../core/circular-video-buffer.service';
import { ReplayPlayerComponent } from '../../shared/replay-player.component';

@Component({
  selector: 'app-camera-view',
  imports: [ReplayPlayerComponent],
  templateUrl: './camera-view.component.html',
  styleUrls: ['./camera-view.component.css'],
  standalone: true,
})
export class CameraViewComponent implements OnInit, OnDestroy {
  private readonly buffer = inject(CircularVideoBuffer);
  @ViewChild('liveVideo') liveVideo!: ElementRef<HTMLVideoElement>;

  readonly available = signal(false);
  readonly cameraOn = signal(false);
  readonly devices = signal<MediaDeviceInfo[]>([]);
  readonly selectedDeviceId = signal('');
  readonly error = signal<string | null>(null);
  readonly replayOpen = signal(false);
  readonly replayUrl = signal<string | null>(null);

  private attached = false;

  async ngOnInit(): Promise<void> {
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

  async toggleCamera(): Promise<void> {
    if (this.cameraOn()) {
      this.stopCamera();
      return;
    }

    this.error.set(null);
    try {
      await this.buffer.start(this.selectedDeviceId() || undefined);
      this.cameraOn.set(true);
      this.attached = false;
      await this.attachStream();
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

  private stopCamera(): void {
    this.buffer.stop();
    this.cameraOn.set(false);
    const el = this.liveVideo?.nativeElement;
    if (el) {
      el.srcObject = null;
    }
    this.attached = false;
  }

  private async attachStream(): Promise<void> {
    const el = this.liveVideo?.nativeElement;
    if (el && !this.attached) {
      el.srcObject = await this.buffer.activeStream();
      this.attached = true;
    }
  }

  onDeviceChange(deviceId: string): void {
    this.selectedDeviceId.set(deviceId);
    if (this.cameraOn()) {
      this.stopCamera();
      this.toggleCamera();
    }
  }

  async openReplay(): Promise<void> {
    const url = await this.buffer.captureFrame();
    if (url) {
      this.replayUrl.set(url);
      this.replayOpen.set(true);
    }
  }

  closeReplay(): void {
    this.replayOpen.set(false);
    if (this.replayUrl()) {
      URL.revokeObjectURL(this.replayUrl()!);
      this.replayUrl.set(null);
    }
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }
}
