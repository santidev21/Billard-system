import { Component, inject, Input, OnDestroy, OnInit, signal, AfterViewInit, ElementRef, ViewChild } from '@angular/core';

import { CircularVideoBuffer } from '../../core/circular-video-buffer.service';
import { ReplayPlayerComponent } from '../../shared/replay-player.component';

@Component({
  selector: 'app-camera-view',
  imports: [ReplayPlayerComponent],
  templateUrl: './camera-view.component.html',
  styleUrls: ['./camera-view.component.css'],
  standalone: true,
})
export class CameraViewComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly buffer = inject(CircularVideoBuffer);
  @ViewChild('liveVideo') liveVideo!: ElementRef<HTMLVideoElement>;
  @Input() deviceId = '';
  readonly active = this.buffer.active;
  readonly replayOpen = signal(false);
  readonly replayUrl = signal<string | null>(null);
  private attached = false;

  async ngOnInit(): Promise<void> {
    const saved = localStorage.getItem('replayBufferSeconds');
    this.buffer.configure(saved ? Number(saved) : 30);
    await this.buffer.start(this.deviceId || undefined);
  }

  ngAfterViewInit(): void {
    this.attachStream();
  }

  private async attachStream(): Promise<void> {
    const el = this.liveVideo?.nativeElement;
    if (el && !this.attached) {
      el.srcObject = await this.buffer.activeStream();
      this.attached = true;
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
    this.buffer.stop();
  }
}