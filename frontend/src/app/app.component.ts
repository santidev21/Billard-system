import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterOutlet, Router, NavigationEnd } from '@angular/router';

import { SignalRService } from './core/signalr.service';
import { OfflineQueueService } from './core/offline-queue.service';
import { OfflineSyncService } from './core/offline-sync.service';
import { AuthService } from './core/auth.service';
import { ApiService } from './core/api.service';
import { CameraService } from './core/camera.service';
import { PlayerUiService } from './core/player-ui.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  standalone: true,
})
export class AppComponent implements OnInit {
  readonly connected = this.signalr.connected;
  readonly pendingCommands = this.queue.pendingCount;
  readonly online = signal(navigator.onLine);
  readonly area = signal<'player' | 'admin' | 'super' | null>(null);

  readonly cam = inject(CameraService);
  readonly playerUi = inject(PlayerUiService);
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);

  readonly tenantName = signal<string | null>(null);

  readonly showChangePassword = signal(false);
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  readonly passwordError = signal<string | null>(null);
  readonly saving = signal(false);

  constructor(
    private readonly signalr: SignalRService,
    private readonly queue: OfflineQueueService,
    private readonly sync: OfflineSyncService,
    private readonly router: Router,
  ) {
    const applyArea = (url: string): void => {
      if (url.startsWith('/t/') || url.startsWith('/play')) {
        this.area.set('player');
        this.tenantName.set(null);
      } else if (url.startsWith('/admin')) {
        this.area.set('admin');
        this.tenantName.set(this.auth.getUser()?.tenantName ?? null);
      } else if (url.startsWith('/super')) {
        this.area.set('super');
        this.tenantName.set(null);
      } else {
        this.area.set(null);
        this.tenantName.set(null);
      }
    };
    // initial sync (covers direct loads / refresh on /t/.../free)
    try { applyArea(window.location.pathname); } catch {}
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        applyArea(event.urlAfterRedirects);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    window.addEventListener('online', () => {
      this.online.set(true);
      void this.sync.flush();
    });
    window.addEventListener('offline', () => this.online.set(false));
    await this.queue.open();
    await this.signalr.connect();
    void this.sync.flush();
  }

  toggleCamera(): void {
    void this.cam.toggle();
  }

  async logout(): Promise<void> {
    if (this.area() === 'admin' || this.area() === 'super') {
      await this.signalr.leaveAdminGroup();
    }
    this.auth.logout();
    await this.router.navigate(['/login']);
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
      const user = this.auth.getUser();
      await this.api.changePassword(user?.name ?? '', this.currentPassword, this.newPassword);
      this.showChangePassword.set(false);
      await this.router.navigate(['/login']);
    } catch (e: any) {
      this.passwordError.set((e?.error?.message as string) ?? 'La clave actual no coincide.');
    } finally {
      this.saving.set(false);
    }
  }
}
