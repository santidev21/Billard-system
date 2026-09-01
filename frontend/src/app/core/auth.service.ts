import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

const TOKEN_KEY = 'billiard-admin-token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly api: ApiService) {}

  isAuthenticated(): boolean {
    return !!localStorage.getItem(TOKEN_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  async login(password: string): Promise<void> {
    const res = await this.api.login(password);
    localStorage.setItem(TOKEN_KEY, res.token);
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await this.api.changePassword(currentPassword, newPassword);
    this.logout();
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
  }
}
