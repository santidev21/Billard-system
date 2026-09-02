import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-force-password',
  imports: [FormsModule, SpinnerComponent],
  template: `
    <section class="login-wrap">
      <div class="login-card glass">
        <h2>Cambia tu clave</h2>
        <p class="hint">Es tu primer ingreso. Define una clave nueva para continuar.</p>

        <label>Nueva clave</label>
        <input [(ngModel)]="newPassword" type="password" placeholder="Mínimo 8 caracteres" autocomplete="new-password" />

        <label>Confirmar clave</label>
        <input [(ngModel)]="confirmPassword" type="password" placeholder="Repite la clave" autocomplete="new-password" />

        @if (error(); as e) {
          <div class="error">{{ e }}</div>
        }

        <button class="btn btn-primary" [disabled]="loading() || !newPassword || newPassword.length < 8 || newPassword !== confirmPassword" (click)="submit()">
          @if (loading()) {
            <app-spinner [label]="'Guardando…'" />
          } @else {
            Guardar y entrar
          }
        </button>
      </div>
    </section>
  `,
  styles: [`
    .login-wrap { display: flex; align-items: center; justify-content: center; flex: 1; padding: 1rem; }
    .login-card { display: flex; flex-direction: column; gap: 0.6rem; max-width: 360px; width: 100%; }
    .login-card h2 { margin: 0; }
    .hint { font-size: 0.85rem; color: var(--text-muted); margin: 0; }
    label { font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--text-muted); }
    input { padding: 0.55rem 0.75rem; border-radius: var(--radius); border: 1px solid var(--border); background: var(--slate-dark); color: var(--text); width: 100%; box-sizing: border-box; }
    .error { color: var(--red); font-size: 0.85rem; }
    button { margin-top: 0.3rem; }
  `],
  standalone: true,
})
export class ForcePasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  newPassword = '';
  confirmPassword = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Las claves no coinciden.');
      return;
    }
    if (this.newPassword.length < 8) {
      this.error.set('La clave debe tener al menos 8 caracteres.');
      return;
    }
    this.error.set(null);
    this.loading.set(true);
    try {
      await this.auth.forceChangePassword(this.newPassword);
      if (this.auth.isSuperAdmin()) {
        await this.router.navigate(['/super']);
      } else {
        await this.router.navigate(['/admin']);
      }
    } catch (e: any) {
      this.error.set((e?.error?.message as string) ?? 'No se pudo actualizar la clave.');
    } finally {
      this.loading.set(false);
    }
  }
}
