import { Component, input } from '@angular/core';

@Component({
  selector: 'app-spinner',
  template: `
    @if (show()) {
      <div class="spinner-wrap">
        <div class="spinner"></div>
        @if (label()) {
          <span class="spinner-label">{{ label() }}</span>
        }
      </div>
    }
  `,
  styles: [
    `
      .spinner-wrap {
        display: inline-flex;
        align-items: center;
        gap: 0.6rem;
        justify-content: center;
      }
      .spinner {
        width: 20px;
        height: 20px;
        border: 3px solid rgba(255, 255, 255, 0.2);
        border-top-color: var(--pa-color, #f59e0b);
        border-radius: 50%;
        animation: sp 0.8s linear infinite;
      }
      .spinner-label {
        font-size: 0.85rem;
        color: var(--text-muted, #94a3b8);
      }
      @keyframes sp {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
  standalone: true,
})
export class SpinnerComponent {
  readonly show = input(true);
  readonly label = input('');
}