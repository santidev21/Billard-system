import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { lastValueFrom } from 'rxjs';

import {
  AuditLog,
  DashboardSummary,
  LocalInfo,
  LoginResponse,
  MatchListItem,
  Product,
  RecoveryCode,
  Settings,
  TableDetail,
  TableResponse,
  TopProduct,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  login(userName: string, password: string): Promise<LoginResponse> {
    return lastValueFrom(this.http.post<LoginResponse>(`${this.base}/auth/login`, { userName, password }));
  }
  refresh(refreshToken: string): Promise<LoginResponse> {
    return lastValueFrom(this.http.post<LoginResponse>(`${this.base}/auth/refresh`, { refreshToken }));
  }
  logout(refreshToken: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/auth/logout`, { refreshToken }));
  }
  forgotPassword(userName: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/auth/forgot`, { userName }));
  }
  resetPassword(userName: string, code: string, newPassword: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/auth/reset`, { userName, code, newPassword }));
  }
  changePassword(userId: string, currentPassword: string, newPassword: string): Promise<void> {
    return lastValueFrom(
      this.http.post<void>(`${this.base}/auth/change-password`, { userId, currentPassword, newPassword })
    );
  }

  getTables(): Promise<TableResponse[]> {
    return lastValueFrom(this.http.get<TableResponse[]>(`${this.base}/tables`));
  }
  createTable(name: string, hourlyRate: number, code?: string): Promise<TableResponse> {
    return lastValueFrom(this.http.post<TableResponse>(`${this.base}/tables`, { name, hourlyRate, code }));
  }
  updateTable(id: string, name: string, hourlyRate: number, code?: string): Promise<TableResponse> {
    return lastValueFrom(this.http.put<TableResponse>(`${this.base}/tables/${id}`, { name, hourlyRate, code }));
  }
  updateAllRates(hourlyRate: number): Promise<{ updated: number }> {
    return lastValueFrom(this.http.put<{ updated: number }>(`${this.base}/tables/rate/all`, { hourlyRate }));
  }
  attendTable(id: string): Promise<TableResponse> {
    return lastValueFrom(this.http.post<TableResponse>(`${this.base}/tables/${id}/attend`, {}));
  }
  disableTable(id: string): Promise<TableResponse> {
    return lastValueFrom(this.http.post<TableResponse>(`${this.base}/tables/${id}/disable`, {}));
  }
  enableTable(id: string): Promise<TableResponse> {
    return lastValueFrom(this.http.post<TableResponse>(`${this.base}/tables/${id}/enable`, {}));
  }
  deleteTable(id: string): Promise<{ ok: boolean }> {
    return lastValueFrom(this.http.delete<{ ok: boolean }>(`${this.base}/tables/${id}`));
  }

  getTenantTables(slug: string): Promise<TableResponse[]> {
    return lastValueFrom(this.http.get<TableResponse[]>(`${this.base}/t/${slug}/tables`));
  }
  getTenantTable(slug: string, identifier: string): Promise<TableDetail> {
    return lastValueFrom(this.http.get<TableDetail>(`${this.base}/t/${slug}/tables/${identifier}`));
  }
  getTenantProducts(slug: string): Promise<Product[]> {
    return lastValueFrom(this.http.get<Product[]>(`${this.base}/t/${slug}/products`));
  }
  startSession(slug: string, id: string, whitePlayerName: string, yellowPlayerName: string, gameMode: 'Managed' | 'FreeMode', transactionId: string): Promise<{ tableId: string; matchId: string }> {
    return lastValueFrom(
      this.http.post<{ tableId: string; matchId: string }>(`${this.base}/t/${slug}/tables/${id}/start`, {
        whitePlayerName,
        yellowPlayerName,
        gameMode,
        transactionId,
      })
    );
  }
  score(slug: string, id: string, playerColor: 'white' | 'yellow', delta: number, transactionId: string): Promise<{ newScore: number }> {
    return lastValueFrom(
      this.http.post<{ newScore: number }>(`${this.base}/t/${slug}/tables/${id}/score`, { playerColor, delta, transactionId })
    );
  }
  renamePlayers(slug: string, id: string, whitePlayerName: string, yellowPlayerName: string, transactionId: string): Promise<void> {
    return lastValueFrom(
      this.http.post<void>(`${this.base}/t/${slug}/tables/${id}/players`, { whitePlayerName, yellowPlayerName, transactionId })
    );
  }
  callWaiter(slug: string, id: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/t/${slug}/tables/${id}/call-waiter`, {}));
  }
  requestCheck(slug: string, id: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/t/${slug}/tables/${id}/request-check`, {}));
  }
  addConsumption(slug: string, id: string, productId: string, quantity: number, transactionId: string): Promise<{ consumptionTotal: number }> {
    return lastValueFrom(
      this.http.post<{ consumptionTotal: number }>(`${this.base}/t/${slug}/tables/${id}/consumption`, { productId, quantity, transactionId })
    );
  }
  finishSession(slug: string, id: string, transactionId: string): Promise<{ matchHistoryId: string; grandTotal: number }> {
    return lastValueFrom(this.http.post<{ matchHistoryId: string; grandTotal: number }>(`${this.base}/t/${slug}/tables/${id}/finish`, { transactionId }));
  }
  finishRound(slug: string, id: string, transactionId: string): Promise<{ id: string; roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null }> {
    return lastValueFrom(
      this.http.post<{ id: string; roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null }>(`${this.base}/t/${slug}/tables/${id}/finish-round`, { transactionId })
    );
  }
  getRounds(slug: string, id: string): Promise<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string; duration: string }[] }> {
    return lastValueFrom(this.http.get<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string; duration: string }[] }>(`${this.base}/t/${slug}/tables/${id}/rounds`));
  }

  getProducts(): Promise<Product[]> {
    return lastValueFrom(this.http.get<Product[]>(`${this.base}/products`));
  }
  createProduct(name: string, price: number): Promise<Product> {
    return lastValueFrom(this.http.post<Product>(`${this.base}/products`, { name, price }));
  }
  updateProduct(id: string, name: string, price: number): Promise<void> {
    return lastValueFrom(this.http.put<void>(`${this.base}/products/${id}`, { name, price }));
  }
  deactivateProduct(id: string): Promise<void> {
    return lastValueFrom(this.http.delete<void>(`${this.base}/products/${id}`));
  }

  getSettings(): Promise<Settings> {
    return lastValueFrom(this.http.get<Settings>(`${this.base}/settings`));
  }
  updateSettings(values: Settings): Promise<void> {
    return lastValueFrom(this.http.put<void>(`${this.base}/settings`, values));
  }

  getMatches(): Promise<MatchListItem[]> {
    return lastValueFrom(this.http.get<MatchListItem[]>(`${this.base}/matches`));
  }
  getMatch(id: string): Promise<any> {
    return lastValueFrom(this.http.get<any>(`${this.base}/matches/${id}`));
  }
  getDashboardSummary(): Promise<DashboardSummary> {
    return lastValueFrom(this.http.get<DashboardSummary>(`${this.base}/dashboard/summary`));
  }
  getTopProducts(): Promise<TopProduct[]> {
    return lastValueFrom(this.http.get<TopProduct[]>(`${this.base}/dashboard/top-products`));
  }
  getAuditLogs(): Promise<AuditLog[]> {
    return lastValueFrom(this.http.get<AuditLog[]>(`${this.base}/audit/logs`));
  }

  getSuperLocals(): Promise<LocalInfo[]> {
    return lastValueFrom(this.http.get<LocalInfo[]>(`${this.base}/super/locals`));
  }
  createLocal(name: string, password?: string): Promise<LocalInfo> {
    return lastValueFrom(this.http.post<LocalInfo>(`${this.base}/super/locals`, { name, password }));
  }
  getSuperRecoveries(): Promise<RecoveryCode[]> {
    return lastValueFrom(this.http.get<RecoveryCode[]>(`${this.base}/super/recoveries`));
  }
  revealRecovery(id: string): Promise<{ code: string }> {
    return lastValueFrom(this.http.post<{ code: string }>(`${this.base}/super/recoveries/${id}/reveal`, {}));
  }
}
