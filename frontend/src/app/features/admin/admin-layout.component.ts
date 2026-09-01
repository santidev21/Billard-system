import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import type { AdminNotification } from '../../core/signalr.service';
import { fmtMoney } from '../../core/format';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.css'],
  standalone: true,
})
export class AdminLayoutComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);

  readonly showChangePassword = signal(false);
  readonly callPopup = signal<AdminNotification | null>(null);
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  readonly passwordError = signal<string | null>(null);
  readonly saving = signal(false);

  constructor() {
    effect(() => {
      const n = this.signalr.adminNotification();
      if (n) {
        this.callPopup.set(n);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.signalr.joinAdminGroup();
  }

  popupTitle(n: AdminNotification | null): string {
    if (!n) {
      return '';
    }
    return n.type === 'check' ? 'Piden la cuenta' : 'Solicitan al mesero';
  }

  popupBody(n: AdminNotification | null): string {
    if (!n) {
      return '';
    }
    return n.type === 'check'
      ? `La mesa ${n.tableName} quiere cerrar su cuenta · Total $${fmtMoney(n.total ?? 0)}`
      : `La mesa ${n.tableName} te está esperando.`;
  }

  async attendFromPopup(n: AdminNotification | null): Promise<void> {
    this.callPopup.set(null);
    if (!n) {
      return;
    }
    await this.api.attendTable(n.tableId).catch(() => undefined);
  }

  closePopup(): void {
    this.callPopup.set(null);
  }


  async logout(): Promise<void> {
    await this.api.logout().catch(() => undefined);
    await this.signalr.leaveAdminGroup();
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
    if (this.newPassword.length < 8) {
      this.passwordError.set('La nueva clave debe tener al menos 8 caracteres.');
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
      await this.router.navigate(['/login']);
    } catch (e: any) {
      this.passwordError.set((e?.error?.message as string) ?? 'La clave actual no coincide.');
    } finally {
      this.saving.set(false);
    }
  }
}
