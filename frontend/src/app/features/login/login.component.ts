import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, SpinnerComponent],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  standalone: true,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  userName = '';
  password = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.loading.set(true);
    try {
      const res = await this.auth.login(this.userName, this.password);
      if (res.mustChangePassword) {
        await this.router.navigate(['/force-password']);
      } else if (this.auth.isSuperAdmin()) {
        await this.router.navigate(['/super']);
      } else {
        await this.router.navigate(['/admin']);
      }
    } catch (e: any) {
      if (e?.status === 429) {
        this.error.set('Demasiados intentos. Espera un minuto e intenta de nuevo.');
      } else {
        this.error.set((e?.error?.message as string) ?? 'Credenciales incorrectas.');
      }
      this.loading.set(false);
    }
  }
}
