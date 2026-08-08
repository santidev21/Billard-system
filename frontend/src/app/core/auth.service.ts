import { Injectable } from '@angular/core';
import { ApiService } from './api.service';

const TOKEN_KEY = 'billiard-admin-token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly api: ApiService) {}

  isAuthenticated(): boolean {
    return !!sessionStorage.getItem(TOKEN_KEY);
  }

  async login(password: string): Promise<void> {
    const res = await this.api.login(password);
    sessionStorage.setItem(TOKEN_KEY, res.token);
  }

  changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return this.api.changePassword(currentPassword, newPassword);
  }

  logout(): void {
    sessionStorage.removeItem(TOKEN_KEY);
  }
}