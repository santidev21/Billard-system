import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { DashboardSummary, TableResponse, TableDetail, TopProduct } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
  standalone: true,
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);

  readonly tables = signal<TableResponse[]>([]);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly topProducts = signal<TopProduct[]>([]);
  readonly details = signal<Record<string, TableDetail>>({});
  readonly notifications = signal<{ id: string; text: string; type: string }[]>([]);

  private notifId = 0;

  async ngOnInit(): Promise<void> {
    await this.refresh();
    effect(() => {
      this.signalr.tableStateUpdated();
      this.refresh();
    });
    effect(() => {
      const n = this.signalr.adminNotification();
      if (n) {
        this.pushNotification(n);
      }
    });
  }

  private async refresh(): Promise<void> {
    const [tables, summary, top, details] = await Promise.all([
      this.api.getTables(),
      this.api.getDashboardSummary(),
      this.api.getTopProducts().catch(() => [] as TopProduct[]),
      this.loadDetails(),
    ]);
    this.tables.set(tables);
    this.summary.set(summary);
    this.topProducts.set(top);
    this.details.set(details);
  }

  private async loadDetails(): Promise<Record<string, TableDetail>> {
    const result: Record<string, TableDetail> = {};
    for (const table of this.tables()) {
      try {
        result[table.id] = await this.api.getTable(table.id);
      } catch {
        // ignore
      }
    }
    return result;
  }

  private pushNotification(n: { type: string; tableId: string; tableName: string; total?: number }): void {
    const text = n.type === 'waiter'
      ? `🔔 ${n.tableName} solicita mesero`
      : `🧾 ${n.tableName} pide cuenta · Total $${n.total ?? 0}`;
    const entry = { id: String(++this.notifId), text, type: n.type };
    this.notifications.update((list) => [entry, ...list].slice(0, 8));
    setTimeout(() => this.notifications.update((list) => list.filter((x) => x.id !== entry.id)), 8000);
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
}