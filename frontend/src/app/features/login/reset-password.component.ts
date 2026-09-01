import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { ApiService } from '../../core/api.service';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink, SpinnerComponent],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css'],
  standalone: true,
})
export class ResetPasswordComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  userName = '';
  code = '';
  newPassword = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    if (this.newPassword.length < 8) {
      this.error.set('La nueva clave debe tener al menos 8 caracteres.');
      return;
    }
    this.loading.set(true);
    try {
      await this.api.resetPassword(this.userName, this.code, this.newPassword);
      await this.router.navigate(['/login']);
    } catch (e: any) {
      this.error.set((e?.error?.message as string) ?? 'No se pudo restablecer la clave.');
    } finally {
      this.loading.set(false);
    }
  }
}
