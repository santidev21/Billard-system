import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { DashboardSummary, TableResponse, TableDetail, TopProduct } from '../../core/models';
import { fmtMoney } from '../../core/format';
import { CameraViewComponent } from '../player/camera-view.component';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-dashboard',
  imports: [FormsModule, CameraViewComponent, SpinnerComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
  standalone: true,
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);

  readonly tables = signal<TableResponse[]>([]);
  readonly loading = signal(true);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly topProducts = signal<TopProduct[]>([]);
  readonly details = signal<Record<string, TableDetail>>({});
  readonly notifications = signal<{ id: string; text: string; type: string }[]>([]);
  readonly showAddTable = signal(false);
  readonly showRateCard = signal(false);
  readonly selectedTable = signal<TableDetail | null>(null);
  readonly consumptionProducts = signal<{ id: string; name: string; price: number }[]>([]);

  readonly fmtMoney = fmtMoney;

  newTableName = '';
  newTableCode = '';
  newTableRate = 12000;
  toastProductId = '';
  selectedWhite = '';
  selectedYellow = '';
  showStartForm = false;
  readonly showFinishConfirm = signal(false);
  readonly now = signal(0);

  private notifId = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;
  private refreshToken = 0;

  private clockTimer: ReturnType<typeof setInterval> | undefined;
  private pollTimer: ReturnType<typeof setInterval> | undefined;

  async ngOnInit(): Promise<void> {
    await this.loadGlobalRate();
    await this.loadProducts();
    this.clockTimer = setInterval(() => this.now.set(Date.now()), 1000);
    this.pollTimer = setInterval(() => void this.refresh().catch(() => undefined), 10000);
    effect(() => {
      this.signalr.tableStateUpdated();
      this.scheduleRefresh();
    });
    effect(() => {
      const n = this.signalr.adminNotification();
      if (n) {
        this.pushNotification(n);
      }
    });
    await this.refresh().catch(() => undefined);
  }

  ngOnDestroy(): void {
    if (this.clockTimer) {
      clearInterval(this.clockTimer);
    }
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  private scheduleRefresh(): void {
    this.refreshToken += 1;
    const token = this.refreshToken;
    clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      if (token === this.refreshToken) {
        void this.refresh();
      }
    }, 350);
  }

  private async loadGlobalRate(): Promise<void> {
    try {
      const settings = await this.api.getSettings();
      const rate = Number(settings['HourlyRate']);
      if (rate > 0) {
        this.newTableRate = rate;
      }
    } catch {
      // keep default
    }
  }

  private async loadProducts(): Promise<void> {
    try {
      this.consumptionProducts.set(await this.api.getProducts());
    } catch {
      // ignore
    }
  }

  cardElapsed(detail: TableDetail): string {
    const m = detail.activeMatch;
    if (!m) {
      return '00:00';
    }
    const secs = Math.max(0, Math.floor((this.now() - new Date(m.startedAt).getTime()) / 1000));
    const h = String(Math.floor(secs / 3600)).padStart(2, '0');
    const min = String(Math.floor((secs % 3600) / 60)).padStart(2, '0');
    return `${h}:${min}`;
  }

  cardTimeCost(detail: TableDetail): number {
    const m = detail.activeMatch;
    if (!m) {
      return 0;
    }
    const minutes = Math.max(0, Math.floor((this.now() - new Date(m.startedAt).getTime()) / 60000));
    return Math.floor((minutes / 60) * detail.hourlyRate);
  }

  cardTotal(detail: TableDetail): number {
    return this.cardTimeCost(detail) + (detail.activeMatch?.consumptionTotal ?? 0);
  }

  private async refresh(): Promise<void> {
    this.loading.set(true);
    try {
      const [tables, summary, top] = await Promise.all([
        this.api.getTables(),
        this.api.getDashboardSummary(),
        this.api.getTopProducts().catch(() => [] as TopProduct[]),
      ]);
      this.tables.set(tables);
      this.summary.set(summary);
      this.topProducts.set(top);
      this.details.set(await this.loadDetails(tables));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadDetails(tables: TableResponse[]): Promise<Record<string, TableDetail>> {
    const entries = await Promise.all(
      tables.map(async (table) => {
        try {
          const detail = await this.api.getTable(table.id);
          return [table.id, detail] as const;
        } catch {
          return [table.id, null as unknown as TableDetail] as const;
        }
      })
    );
    return Object.fromEntries(entries.filter(([, d]) => !!d));
  }

  private pushNotification(n: { type: string; tableId: string; tableName: string; total?: number }): void {
    const text = n.type === 'waiter'
      ? `🔔 ${n.tableName} solicita mesero`
      : `🧾 ${n.tableName} pide cuenta · Total $${fmtMoney(n.total ?? 0)}`;
    const entry = { id: String(++this.notifId), text, type: n.type };
    this.notifications.update((list) => [entry, ...list].slice(0, 6));
    setTimeout(() => this.notifications.update((list) => list.filter((x) => x.id !== entry.id)), 9000);
  }

  statusDot(status: string): string {
    switch (status) {
      case 'Available': return 'free';
      case 'Occupied': return 'occupied';
      case 'WaitingForWaiter': return 'waiter';
      case 'WaitingForCheck': return 'check';
      default: return 'free';
    }
  }

  statusName(status: string): string {
    switch (status) {
      case 'Available': return 'Libre';
      case 'Occupied': return 'Ocupada';
      case 'WaitingForWaiter': return 'Esperando mesero';
      case 'WaitingForCheck': return 'Esperando cuenta';
      case 'OutOfService': return 'Fuera de servicio';
      default: return status;
    }
  }

  async addTable(): Promise<void> {
    if (!this.newTableName.trim()) {
      return;
    }
    await this.api.createTable(this.newTableName.trim(), 0, this.newTableCode.trim() || undefined);
    this.newTableName = '';
    this.newTableCode = '';
    this.showAddTable.set(false);
    await this.refresh();
  }

  async attendTable(tableId: string): Promise<void> {
    try {
      await this.api.attendTable(tableId);
    } catch {
      // petición ya resuelta
    }
    await this.refresh();
    const sel = this.selectedTable();
    if (sel) {
      await this.openTable(sel.id);
    }
  }

  async toggleEnabled(table: TableResponse): Promise<void> {
    try {
      if (table.isActive) {
        await this.api.disableTable(table.id);
      } else {
        await this.api.enableTable(table.id);
      }
    } catch {
      // ignore
    }
    await this.refresh();
  }

  async deleteTable(tableId: string): Promise<void> {
    const ok = confirm(`¿Eliminar definitivamente esta mesa? Esta acción no se puede deshacer.`);
    if (!ok) {
      return;
    }
    try {
      await this.api.deleteTable(tableId);
    } catch {
      alert('No se pudo borrar: la mesa tiene partida activa o historial.');
    }
    this.selectedTable.set(null);
    await this.refresh();
  }

  copyLink(table: TableResponse): void {
    const url = `${window.location.origin}/tables/${encodeURIComponent(table.code)}`;
    void navigator.clipboard?.writeText(url).catch(() => undefined);
  }

  async saveAllRates(): Promise<void> {
    if (this.newTableRate <= 0) {
      return;
    }
    try {
      await this.api.updateAllRates(this.newTableRate);
    } catch {
      // ignore
    }
    this.showRateCard.set(false);
    await this.refresh();
  }

  async openTable(tableId: string): Promise<void> {
    try {
      const detail = await this.api.getTable(tableId);
      this.selectedTable.set(detail);
    } catch {
      // ignore
    }
  }

  closeTable(): void {
    this.selectedTable.set(null);
    this.toastProductId = '';
  }

  async addConsumptionToTable(tableId: string): Promise<void> {
    const product = this.consumptionProducts().find((p) => p.id === this.toastProductId);
    if (!product) {
      return;
    }
    await this.api.addConsumption(tableId, product.id, 1, crypto.randomUUID());
    this.toastProductId = '';
    await this.refresh();
    const sel = this.selectedTable();
    if (sel) {
      await this.openTable(sel.id);
    }
  }

  triggerWaiter(tableId: string): void {
    this.api.callWaiter(tableId).catch(() => undefined);
  }

  triggerCheck(tableId: string): void {
    this.api.requestCheck(tableId).catch(() => undefined);
  }

  openStartForm(tableId: string): void {
    const table = this.tables().find((t) => t.id === tableId);
    if (!table) {
      return;
    }
    this.selectedTable.set({ id: table.id, name: table.name, code: table.code, status: table.status, hourlyRate: table.hourlyRate, isActive: table.isActive, activeMatch: null, activeMatchId: null });
    this.selectedWhite = 'Jugador 1';
    this.selectedYellow = 'Jugador 2';
    this.showStartForm = true;
  }

  async confirmStart(tableId: string): Promise<void> {
    await this.api.startSession(tableId, this.selectedWhite.trim() || 'Jugador 1', this.selectedYellow.trim() || 'Jugador 2', 'Managed', crypto.randomUUID());
    this.showStartForm = false;
    this.showFinishConfirm.set(false);
    await this.refresh();
    await this.openTable(tableId);
  }

  finishSummary(): { elapsed: string; consumptionTotal: number; grandTotal: number } | null {
    const t = this.selectedTable();
    if (!t?.activeMatch) {
      return null;
    }
    const started = new Date(t.activeMatch.startedAt).getTime();
    const now = Date.now();
    const secs = Math.max(0, Math.floor((now - started) / 1000));
    const h = String(Math.floor(secs / 3600)).padStart(2, '0');
    const m = String(Math.floor((secs % 3600) / 60)).padStart(2, '0');
    const minutes = Math.floor(secs / 60);
    const timeCost = Math.floor((minutes / 60) * t.hourlyRate);
    const consumptionTotal = t.activeMatch.consumptionTotal;
    return { elapsed: `${h}:${m}`, consumptionTotal, grandTotal: timeCost + consumptionTotal };
  }

  requestFinish(detail?: TableDetail): void {
    if (detail) {
      this.selectedTable.set(detail);
    }
    this.showFinishConfirm.set(true);
  }

  async finishSession(tableId: string): Promise<void> {
    await this.api.finishSession(tableId, crypto.randomUUID());
    this.showFinishConfirm.set(false);
    this.selectedTable.set(null);
    await this.refresh();
  }
}