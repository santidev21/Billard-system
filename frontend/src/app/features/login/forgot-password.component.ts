import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api.service';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink, SpinnerComponent],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css'],
  standalone: true,
})
export class ForgotPasswordComponent {
  private readonly api = inject(ApiService);

  userName = '';
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.loading.set(true);
    try {
      await this.api.forgotPassword(this.userName);
      this.sent.set(true);
    } catch (e: any) {
      this.error.set((e?.error?.message as string) ?? 'No se pudo procesar la solicitud.');
    } finally {
      this.loading.set(false);
    }
  }
}
