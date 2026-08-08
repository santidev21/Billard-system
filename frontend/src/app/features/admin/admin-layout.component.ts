import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.css'],
  standalone: true,
})
export class AdminLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly showChangePassword = signal(false);
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  readonly passwordError = signal<string | null>(null);
  readonly saving = signal(false);

  async logout(): Promise<void> {
    this.auth.logout();
    await this.router.navigate(['/play']);
  }

  openChangePassword(): void {
    this.currentPassword = '';
    this.newPassword = '';
    this.confirmPassword = '';
    this.passwordError.set(null);
    this.showChangePassword.set(true);
  }

  closeChangePassword(): void {
    this.showChangePassword.set(false);
    this.saving.set(false);
  }

  async savePassword(): Promise<void> {
    this.passwordError.set(null);
    if (this.newPassword.length < 4) {
      this.passwordError.set('La nueva clave debe tener al menos 4 caracteres.');
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('La confirmación no coincide con la nueva clave.');
      return;
    }
    this.saving.set(true);
    try {
      await this.auth.changePassword(this.currentPassword, this.newPassword);
      this.showChangePassword.set(false);
    } catch (e: any) {
      this.passwordError.set((e?.error?.message as string) ?? 'La clave actual no coincide.');
    } finally {
      this.saving.set(false);
    }
  }
}