import { Component, effect, inject, Input, OnDestroy, OnInit, ViewChild, ElementRef, signal } from '@angular/core';

import { CameraService } from '../../core/camera.service';
import { ReplayPlayerComponent } from '../../shared/replay-player.component';

@Component({
  selector: 'app-camera-view',
  imports: [ReplayPlayerComponent],
  templateUrl: './camera-view.component.html',
  styleUrls: ['./camera-view.component.css'],
  standalone: true,
})
export class CameraViewComponent implements OnInit, OnDestroy {
  protected readonly cam = inject(CameraService);
  @ViewChild('liveVideo') liveVideo!: ElementRef<HTMLVideoElement>;

  @Input() fill = false;

  readonly replayOpen = signal(false);
  readonly replayUrl = signal<string | null>(null);

  private attached = false;

  constructor() {
    effect(() => {
      if (this.cam.cameraOn()) {
        void this.attachIfReady();
      } else {
        this.attached = false;
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.cam.init();
  }

  async attachIfReady(): Promise<void> {
    if (this.cam.cameraOn() && this.liveVideo?.nativeElement && !this.attached) {
      this.liveVideo.nativeElement.srcObject = await this.cam.activeStream();
      this.attached = true;
    }
  }

  onToggle(): void {
    void this.cam.toggle();
    this.attached = false;
  }

  onDeviceChange(deviceId: string): void {
    this.cam.onDeviceChange(deviceId);
    this.attached = false;
  }

  async openReplay(): Promise<void> {
    const url = await this.cam.captureFrame();
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
    this.cam.stop();
  }
}
