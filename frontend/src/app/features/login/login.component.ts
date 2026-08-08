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

  password = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.loading.set(true);
    try {
      await this.auth.login(this.password);
      await this.router.navigate(['/admin']);
    } catch (e: any) {
      this.error.set((e?.error?.message as string) ?? 'Clave incorrecta.');
      this.loading.set(false);
    }
  }
}