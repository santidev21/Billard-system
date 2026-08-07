import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-slide-confirm',
  templateUrl: './slide-confirm.component.html',
  styleUrls: ['./slide-confirm.component.css'],
  standalone: true,
})
export class SlideConfirmComponent {
  @Input() open = false;
  @Input() title = 'Confirmar';
  @Input() message = '';
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  dragX = 0;
  dragging = false;
  completed = false;
  readonly threshold = 260;

  onPointerDown(event: PointerEvent): void {
    if (this.completed) {
      return;
    }
    this.dragging = true;
    const el = event.currentTarget as HTMLElement;
    const startX = event.clientX;
    const onMove = (move: PointerEvent) => {
      const delta = move.clientX - startX;
      this.dragX = Math.max(0, Math.min(delta, this.threshold));
    };
    const onUp = () => {
      this.dragging = false;
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      if (this.dragX >= this.threshold) {
        this.completed = true;
        setTimeout(() => this.confirmed.emit(), 250);
      } else {
        this.dragX = 0;
      }
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  close(): void {
    if (this.completed) {
      return;
    }
    this.dragX = 0;
    this.cancelled.emit();
  }
}