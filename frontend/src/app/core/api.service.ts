import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { lastValueFrom } from 'rxjs';

import {
  AuditLog,
  DashboardSummary,
  MatchListItem,
  Product,
  Settings,
  TableDetail,
  TableResponse,
  TopProduct,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // Auth
  login(password: string): Promise<{ token: string }> {
    return lastValueFrom(this.http.post<{ token: string }>(`${this.base}/auth/login`, { password }));
  }
  changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return lastValueFrom(
      this.http.post<void>(`${this.base}/auth/change-password`, { currentPassword, newPassword })
    );
  }

  // Tables
  getTables(): Promise<TableResponse[]> {
    return lastValueFrom(this.http.get<TableResponse[]>(`${this.base}/tables`));
  }
  createTable(name: string, hourlyRate: number): Promise<TableResponse> {
    return lastValueFrom(this.http.post<TableResponse>(`${this.base}/tables`, { name, hourlyRate }));
  }
  updateTable(id: string, name: string, hourlyRate: number): Promise<TableResponse> {
    return lastValueFrom(this.http.put<TableResponse>(`${this.base}/tables/${id}`, { name, hourlyRate }));
  }
  updateAllRates(hourlyRate: number): Promise<{ updated: number }> {
    return lastValueFrom(this.http.put<{ updated: number }>(`${this.base}/tables/rate/all`, { hourlyRate }));
  }
  getTable(id: string): Promise<TableDetail> {
    return lastValueFrom(this.http.get<TableDetail>(`${this.base}/tables/${id}`));
  }
  startSession(
    id: string,
    whitePlayerName: string,
    yellowPlayerName: string,
    gameMode: 'Managed' | 'FreeMode',
    transactionId: string
  ): Promise<{ tableId: string; matchId: string }> {
    return lastValueFrom(
      this.http.post<{ tableId: string; matchId: string }>(`${this.base}/tables/${id}/start`, {
        whitePlayerName,
        yellowPlayerName,
        gameMode,
        transactionId,
      })
    );
  }
  score(id: string, playerColor: 'white' | 'yellow', delta: number, transactionId: string): Promise<{ newScore: number }> {
    return lastValueFrom(
      this.http.post<{ newScore: number }>(`${this.base}/tables/${id}/score`, { playerColor, delta, transactionId })
    );
  }
  renamePlayers(id: string, whitePlayerName: string, yellowPlayerName: string, transactionId: string): Promise<void> {
    return lastValueFrom(
      this.http.post<void>(`${this.base}/tables/${id}/players`, { whitePlayerName, yellowPlayerName, transactionId })
    );
  }
  callWaiter(id: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/tables/${id}/call-waiter`, {}));
  }
  requestCheck(id: string): Promise<void> {
    return lastValueFrom(this.http.post<void>(`${this.base}/tables/${id}/request-check`, {}));
  }
  addConsumption(id: string, productId: string, quantity: number, transactionId: string): Promise<{ consumptionTotal: number }> {
    return lastValueFrom(
      this.http.post<{ consumptionTotal: number }>(`${this.base}/tables/${id}/consumption`, { productId, quantity, transactionId })
    );
  }
  finishSession(id: string, transactionId: string): Promise<{ matchHistoryId: string; grandTotal: number }> {
    return lastValueFrom(this.http.post<{ matchHistoryId: string; grandTotal: number }>(`${this.base}/tables/${id}/finish`, { transactionId }));
  }
  finishRound(id: string, transactionId: string): Promise<{ id: string; roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null }> {
    return lastValueFrom(
      this.http.post<{ id: string; roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null }>(`${this.base}/tables/${id}/finish-round`, { transactionId })
    );
  }
  getRounds(id: string): Promise<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string }[] }> {
    return lastValueFrom(this.http.get<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string }[] }>(`${this.base}/tables/${id}/rounds`));
  }

  // Catalog
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

  // Settings
  getSettings(): Promise<Settings> {
    return lastValueFrom(this.http.get<Settings>(`${this.base}/settings`));
  }
  updateSettings(values: Settings): Promise<void> {
    return lastValueFrom(this.http.put<void>(`${this.base}/settings`, values));
  }

  // History & Audit & Dashboard
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
}