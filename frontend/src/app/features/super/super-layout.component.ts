import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { LocalInfo, RecoveryCode } from '../../core/models';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-super-layout',
  imports: [FormsModule, DatePipe, SpinnerComponent],
  templateUrl: './super-layout.component.html',
  styleUrls: ['./super-layout.component.css'],
  standalone: true,
})
export class SuperLayoutComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly activeTab = signal<'locales' | 'recoveries'>('locales');
  readonly locals = signal<LocalInfo[]>([]);
  readonly recoveries = signal<RecoveryCode[]>([]);
  readonly loading = signal(false);
  readonly revealedCode = signal<string | null>(null);
  readonly revealedId = signal<string | null>(null);

  newLocalName = '';
  newLocalPassword = '';

  ngOnInit(): void {
    if (!this.auth.isAuthenticated() || !this.auth.isSuperAdmin()) {
      void this.router.navigate(['/login']);
      return;
    }
    void this.loadLocales();
  }

  async loadLocales(): Promise<void> {
    this.loading.set(true);
    try {
      this.locals.set(await this.api.getSuperLocals());
    } catch {
      // ignore
    } finally {
      this.loading.set(false);
    }
  }

  async loadRecoveries(): Promise<void> {
    this.loading.set(true);
    try {
      this.recoveries.set(await this.api.getSuperRecoveries());
    } catch {
      // ignore
    } finally {
      this.loading.set(false);
    }
  }

  switchTab(tab: 'locales' | 'recoveries'): void {
    this.activeTab.set(tab);
    if (tab === 'locales') {
      void this.loadLocales();
    } else {
      void this.loadRecoveries();
    }
  }

  async createLocal(): Promise<void> {
    if (!this.newLocalName.trim()) {
      return;
    }
    this.loading.set(true);
    try {
      await this.api.createLocal(this.newLocalName.trim(), this.newLocalPassword.trim() || undefined);
      this.newLocalName = '';
      this.newLocalPassword = '';
      await this.loadLocales();
    } catch {
      // ignore
    } finally {
      this.loading.set(false);
    }
  }

  async revealCode(id: string): Promise<void> {
    try {
      const res = await this.api.revealRecovery(id);
      this.revealedCode.set(res.code);
      this.revealedId.set(id);
    } catch {
      // ignore
    }
  }

  closeRevealed(): void {
    this.revealedCode.set(null);
    this.revealedId.set(null);
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/login']);
  }
}
