import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import type { AdminNotification } from '../../core/signalr.service';
import { fmtMoney } from '../../core/format';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.css'],
  standalone: true,
})
export class AdminLayoutComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);

  readonly callPopup = signal<AdminNotification | null>(null);

  constructor() {
    effect(() => {
      const n = this.signalr.adminNotification();
      if (n) {
        this.callPopup.set(n);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    if (this.auth.mustChangePassword()) {
      await this.router.navigate(['/force-password']);
      return;
    }
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
}
