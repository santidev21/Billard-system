import { Component, EventEmitter, Input, Output, ViewChild, AfterViewInit, OnChanges } from '@angular/core';

@Component({
  selector: 'app-replay-player',
  templateUrl: './replay-player.component.html',
  styleUrls: ['./replay-player.component.css'],
  standalone: true,
})
export class ReplayPlayerComponent implements AfterViewInit, OnChanges {
  @Input() src: string | null = null;
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();
  @ViewChild('videoEl') videoEl: any;

  playbackRate = 1;

  ngOnChanges(): void {
    if (this.open && this.src) {
      // apply src each time it changes so the video element actually loads it
      this.setup();
    }
  }

  ngAfterViewInit(): void {
    this.setup();
  }

  private setup(): void {
    const el: HTMLVideoElement | undefined = this.videoEl?.nativeElement;
    if (el && this.src) {
      el.src = this.src;
      el.playbackRate = this.playbackRate;
      el.load();
      el.play().catch(() => undefined);
    }
  }

  setRate(rate: number): void {
    this.playbackRate = rate;
    const el = this.videoEl?.nativeElement as HTMLVideoElement;
    if (el) {
      el.playbackRate = rate;
    }
  }

  stepFrames(direction: number): void {
    const el = this.videoEl?.nativeElement as HTMLVideoElement;
    if (el) {
      el.currentTime += 0.033 * direction;
    }
  }

  playPause(): void {
    const el = this.videoEl?.nativeElement as HTMLVideoElement;
    if (el) {
      if (el.paused) {
        el.play().catch(() => undefined);
      } else {
        el.pause();
      }
    }
  }

  close(): void {
    this.closed.emit();
  }
}