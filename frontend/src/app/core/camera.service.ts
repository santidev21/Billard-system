import { computed, inject, Injectable, signal } from '@angular/core';

import { CameraBridgeService } from './camera-bridge.service';
import { CircularVideoBuffer } from './circular-video-buffer.service';

export type CameraSourceKind = 'device' | 'ip';

export interface CameraSource {
  key: string;
  kind: CameraSourceKind;
  /** Device id (getUserMedia) or bridge stream id (go2rtc). */
  id: string;
  label: string;
  detail?: string;
}

/** Selects between the device camera (USB/phone) and the LAN IP cameras. */
@Injectable({ providedIn: 'root' })
export class CameraService {
  private readonly buffer = inject(CircularVideoBuffer);
  private readonly bridge = inject(CameraBridgeService);

  readonly bridgeUrl = this.bridge.baseUrl;

  readonly cameraOn = signal(false);
  readonly error = signal<string | null>(null);
  readonly devices = signal<MediaDeviceInfo[]>([]);
  readonly selectedDeviceId = signal('');
  readonly sources = signal<CameraSource[]>([]);
  readonly selectedKey = signal('');
  readonly pickerOpen = signal(false);

  readonly bridgeAvailable = this.bridge.available;
  readonly bridgeScanning = this.bridge.scanning;
  readonly ipCameras = this.bridge.cameras;

  readonly available = computed(() => this.sources().length > 0);
  readonly selectedSource = computed(
    () =>
      this.sources().find((source) => source.key === this.selectedKey()) ??
      this.sources()[0] ??
      null,
  );
  /** Seconds captured in the replay buffer. */
  readonly recordedSeconds = this.buffer.durationSeconds;
  readonly recordingError = this.buffer.error;

  private initialized = false;

  async init(): Promise<void> {
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    const tasks: Promise<unknown>[] = [];
    if (navigator.mediaDevices) {
      tasks.push(this.enumerateDevices());
      navigator.mediaDevices.addEventListener?.('devicechange', () => void this.enumerateDevices());
    }
    // Probe the local bridge in parallel; it resolves fast when absent.
    tasks.push(this.bridge.probe());
    await Promise.all(tasks);
    this.rebuildSources();
  }

  private async enumerateDevices(): Promise<void> {
    try {
      const all = await navigator.mediaDevices.enumerateDevices();
      const videoDevices = all.filter((d) => d.kind === 'videoinput');
      this.devices.set(videoDevices);
      if (
        !this.selectedDeviceId() ||
        !videoDevices.find((d) => d.deviceId === this.selectedDeviceId())
      ) {
        this.selectedDeviceId.set(videoDevices[0]?.deviceId ?? '');
      }
    } catch {
      this.devices.set([]);
    }
    this.rebuildSources();
  }

  private rebuildSources(): void {
    const sources: CameraSource[] = this.devices().map((device, index) => ({
      key: `device:${device.deviceId}`,
      kind: 'device',
      id: device.deviceId,
      label: device.label || `Cámara ${index + 1}`,
      detail: 'Dispositivo',
    }));

    for (const camera of this.bridge.cameras()) {
      sources.push({
        key: `ip:${camera.id}`,
        kind: 'ip',
        id: camera.id,
        label: camera.name,
        detail: camera.online ? 'Red local' : 'Sin conexión',
      });
    }

    this.sources.set(sources);
    if (!sources.find((source) => source.key === this.selectedKey())) {
      this.selectedKey.set(sources[0]?.key ?? '');
    }
  }

  /** Force a LAN rescan of IP cameras and refresh the selectable sources. */
  async refreshIpCameras(): Promise<void> {
    await this.bridge.refresh(true);
    this.rebuildSources();
  }

  async toggle(): Promise<void> {
    if (this.cameraOn()) {
      this.stop();
      return;
    }
    // Always open the picker so the user can see/select sources and rescan the LAN.
    this.pickerOpen.set(true);
  }

  /** Re-probe the bridge after it was unreachable. */
  async retryBridge(): Promise<void> {
    await this.bridge.probe();
    this.rebuildSources();
  }

  openPicker(): void {
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  /** Choose a source from the picker and start it. */
  async selectSource(key: string): Promise<void> {
    this.selectedKey.set(key);
    const source = this.sources().find((s) => s.key === key);
    if (source?.kind === 'device') {
      this.selectedDeviceId.set(source.id);
    }
    this.pickerOpen.set(false);
    if (this.cameraOn()) {
      this.stop();
    }
    await this.start();
  }

  async start(): Promise<void> {
    this.error.set(null);
    const source = this.selectedSource();
    if (!source) {
      this.error.set('No hay cámaras disponibles.');
      return;
    }

    try {
      if (source.kind === 'device') {
        this.selectedDeviceId.set(source.id);
        await this.buffer.start(source.id || undefined);
      } else {
        const stream = await this.bridge.connect(source.id);
        this.buffer.startWithStream(stream);
      }
      this.cameraOn.set(true);
    } catch (e: unknown) {
      this.cameraOn.set(false);
      this.bridge.disconnect();
      this.error.set(this.describeError(e));
    }
  }

  private describeError(e: unknown): string {
    switch ((e as { name?: string })?.name) {
      case 'NotAllowedError':
        return 'Permiso de cámara denegado. Permití el acceso en el navegador.';
      case 'NotFoundError':
        return 'No se encontró ninguna cámara.';
      case 'NotReadableError':
        return 'Cámara en uso por otra aplicación.';
      default:
        return 'No se pudo acceder a la cámara.';
    }
  }

  stop(): void {
    this.buffer.stop();
    this.bridge.disconnect();
    this.cameraOn.set(false);
    this.error.set(null);
  }

  async activeStream(): Promise<MediaStream | null> {
    return this.buffer.currentStream();
  }

  async captureFrame(): Promise<string | null> {
    return this.buffer.captureFrame();
  }

  onSourceChange(key: string): void {
    this.selectedKey.set(key);
    const source = this.sources().find((s) => s.key === key);
    if (source?.kind === 'device') {
      this.selectedDeviceId.set(source.id);
    }
    if (this.cameraOn()) {
      this.stop();
      void this.start();
    }
  }

  /** Kept for existing callers that pass a raw device id. */
  onDeviceChange(deviceId: string): void {
    this.onSourceChange(`device:${deviceId}`);
  }
}
