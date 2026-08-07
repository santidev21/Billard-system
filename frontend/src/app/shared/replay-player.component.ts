import { Component, EventEmitter, Input, Output, ViewChild, AfterViewInit } from '@angular/core';

@Component({
  selector: 'app-replay-player',
  templateUrl: './replay-player.component.html',
  styleUrls: ['./replay-player.component.css'],
  standalone: true,
})
export class ReplayPlayerComponent implements AfterViewInit {
  @Input() src: string | null = null;
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();
  @ViewChild('videoEl') videoEl: any;

  playbackRate = 1;

  ngAfterViewInit(): void {
    this.setup();
  }

  private setup(): void {
    const el: HTMLVideoElement | undefined = this.videoEl?.nativeElement;
    if (el && this.src) {
      el.src = this.src;
      el.playbackRate = this.playbackRate;
      el.load();
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
        el.play();
      } else {
        el.pause();
      }
    }
  }

  close(): void {
    this.closed.emit();
  }
}