import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { LoginResponse, UserInfo } from './models';

const ACCESS_KEY = 'billiard-access-token';
const REFRESH_KEY = 'billiard-refresh-token';
const USER_KEY = 'billiard-user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly api: ApiService) {}

  isAuthenticated(): boolean {
    return !!localStorage.getItem(ACCESS_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(ACCESS_KEY);
  }

  getUser(): UserInfo | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as UserInfo;
    } catch {
      return null;
    }
  }

  getTenantSlug(): string | null {
    return this.getUser()?.tenantSlug ?? null;
  }

  isSuperAdmin(): boolean {
    return this.getUser()?.role === 'SuperAdmin';
  }

  mustChangePassword(): boolean {
    return this.getUser()?.mustChangePassword === true;
  }

  async login(userName: string, password: string): Promise<LoginResponse> {
    const res = await this.api.login(userName, password);
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify({
      name: res.userName,
      role: res.role,
      tenantName: res.tenantName,
      tenantSlug: res.tenantSlug,
      mustChangePassword: res.mustChangePassword,
    } as UserInfo));
    return res;
  }

  async refresh(): Promise<void> {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (!refreshToken) {
      throw new Error('No refresh token');
    }
    const res = await this.api.refresh(refreshToken);
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify({
      name: res.userName,
      role: res.role,
      tenantName: res.tenantName,
      tenantSlug: res.tenantSlug,
      mustChangePassword: res.mustChangePassword,
    } as UserInfo));
  }

  async forceChangePassword(newPassword: string): Promise<void> {
    const res = await this.api.forceChangePassword(newPassword);
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify({
      name: res.userName,
      role: res.role,
      tenantName: res.tenantName,
      tenantSlug: res.tenantSlug,
      mustChangePassword: res.mustChangePassword,
    } as UserInfo));
  }

  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (refreshToken) {
      await this.api.logout(refreshToken).catch(() => undefined);
    }
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
  }
}
