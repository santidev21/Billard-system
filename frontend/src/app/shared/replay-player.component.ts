import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  ViewChild,
} from '@angular/core';

@Component({
  selector: 'app-replay-player',
  templateUrl: './replay-player.component.html',
  styleUrls: ['./replay-player.component.css'],
  standalone: true,
})
export class ReplayPlayerComponent implements OnChanges {
  @Input() src: string | null = null;
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();

  playbackRate = 1;

  private videoEl?: ElementRef<HTMLVideoElement>;

  /**
   * The `<video>` lives inside `@if (open && src)`, so it only exists after the
   * modal renders. This setter fires at that exact moment and attaches the blob
   * URL (ngOnChanges alone runs too early, before the element exists).
   */
  @ViewChild('videoEl')
  set videoRef(ref: ElementRef<HTMLVideoElement> | undefined) {
    this.videoEl = ref;
    if (ref) {
      this.setup();
    }
  }

  ngOnChanges(): void {
    this.setup();
  }

  private setup(): void {
    const el = this.videoEl?.nativeElement;
    if (el && this.src) {
      el.src = this.src;
      el.playbackRate = this.playbackRate;
      el.onloadedmetadata = () => {
        // Jump near the end: the interesting play is the most recent footage.
        if (Number.isFinite(el.duration) && el.duration > 8) {
          el.currentTime = Math.max(0, el.duration - 8);
        }
        el.play().catch(() => undefined);
      };
      el.load();
    }
  }

  setRate(rate: number): void {
    this.playbackRate = rate;
    const el = this.videoEl?.nativeElement;
    if (el) {
      el.playbackRate = rate;
    }
  }

  stepFrames(direction: number): void {
    const el = this.videoEl?.nativeElement;
    if (el) {
      el.currentTime += 0.033 * direction;
    }
  }

  playPause(): void {
    const el = this.videoEl?.nativeElement;
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
