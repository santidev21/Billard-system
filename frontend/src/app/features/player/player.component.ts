import { Component, computed, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { OfflineQueueService } from '../../core/offline-queue.service';
import { GameMode, TableDetail } from '../../core/models';
import { CameraViewComponent } from './camera-view.component';

interface UiConsumption {
  id: string;
  name: string;
  qty: number;
  total: number;
  at: string;
}

@Component({
  selector: 'app-player',
  imports: [FormsModule, CameraViewComponent],
  templateUrl: './player.component.html',
  styleUrls: ['./player.component.css'],
  standalone: true,
})
export class PlayerComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);
  private readonly queue = inject(OfflineQueueService);
  private readonly route = inject(ActivatedRoute);

  readonly gameMode = signal<GameMode>((localStorage.getItem('tableMode') as GameMode) ?? 'Managed');
  readonly whiteName = signal('Jugador 1');
  readonly yellowName = signal('Jugador 2');
  readonly whiteScore = signal(0);
  readonly yellowScore = signal(0);
  readonly elapsedSeconds = signal(0);
  readonly consumptionTotal = signal(0);
  readonly consumptions = signal<UiConsumption[]>([]);
  readonly tableId = signal('');
  readonly tableName = signal('Mesa');
  readonly running = signal(false);
  readonly showConfirm = signal(false);
  readonly showConsumptionLog = signal(false);
  readonly roundNumber = signal(0);
  readonly lastRound = signal<string | null>(null);
  readonly requestSent = signal<'waiter' | 'check' | null>(null);
  readonly products = signal<{ id: string; name: string; price: number }[]>([]);

  readonly totalCarambolas = computed(() => this.whiteScore() + this.yellowScore());
  readonly tableNumber = computed(() => {
    const match = this.tableName().match(/\d+/);
    return match ? match[0] : this.tableName();
  });
  readonly elapsed = computed(() => {
    const s = this.elapsedSeconds();
    const h = String(Math.floor(s / 3600));
    const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
    const sec = String(s % 60).padStart(2, '0');
    return `${h}:${m}:${sec}`;
  });

  readonly hourlyRate = signal(String(localStorage.getItem('defaultRate') ?? '12000'));

  readonly timeCost = computed(() => {
    const rate = Number(this.hourlyRate());
    return (this.elapsedSeconds() / 3600) * rate;
  });
  readonly grandTotal = computed(() => this.timeCost() + this.consumptionTotal());
  readonly timeCostText = computed(() => this.fmtMoney(this.timeCost()));
  readonly consumptionTotalText = computed(() => this.fmtMoney(this.consumptionTotal()));
  readonly grandTotalText = computed(() => this.fmtMoney(this.grandTotal()));

  private fmtMoney(value: number): string {
    return value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  }

  readonly fmt = (value: number): string => this.fmtMoney(value);

  renameName(event: Event, color: 'white' | 'yellow'): void {
    const input = event.target as HTMLInputElement;
    void this.renamePlayer(color, input.value);
  }

  whiteInput = 'Jugador 1';
  yellowInput = 'Jugador 2';
  selectedProductId = '';

  private tickTimer: ReturnType<typeof setInterval> | undefined;

  constructor(
    private readonly router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    this.route.params.subscribe(async (params) => {
      const id = params['id'];
      this.tableId.set(id ?? '');
      if (id) {
        await this.loadTable(id);
        await this.signalr.joinTable(id);
      } else {
        await this.autoSelectTable();
      }
    });
    await this.loadProducts();
  }

  private async autoSelectTable(): Promise<void> {
    try {
      const tables = await this.api.getTables();
      const pick = tables.find((t) => t.status === 'Available') ?? tables[0];
      if (pick) {
        this.tableId.set(pick.id);
        this.tableName.set(pick.name);
        await this.loadTable(pick.id);
        await this.signalr.joinTable(pick.id);
      }
    } catch {
      // offline
    }
  }

  private async loadProducts(): Promise<void> {
    try {
      const cats = await this.api.getProducts();
      this.products.set(cats.flatMap((c) => c.products.map((p) => ({ id: p.id, name: p.name, price: p.price }))));
    } catch {
      // offline
    }
  }

  ngOnDestroy(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
  }

  private async loadTable(id: string): Promise<void> {
    try {
      const detail = await this.api.getTable(id);
      this.tableName.set(detail.name);
      localStorage.setItem('tableName', detail.name);
      this.applyDetail(detail);
    } catch {
      // offline: rely on cached defaults
    }
  }

  private applyDetail(detail: TableDetail): void {
    if (!detail.activeMatch) {
      this.running.set(false);
      this.whiteScore.set(0);
      this.yellowScore.set(0);
      this.elapsedSeconds.set(0);
      return;
    }
    const m = detail.activeMatch;
    this.running.set(true);
    this.whiteName.set(m.whitePlayerName);
    this.yellowName.set(m.yellowPlayerName);
    this.whiteScore.set(m.whiteScore);
    this.yellowScore.set(m.yellowScore);
    this.consumptionTotal.set(m.consumptionTotal);
    this.consumptions.set(m.consumptions.map((c) => ({ id: c.id, name: c.productName, qty: c.quantity, total: c.total, at: c.createdAt })));
    this.roundNumber.set(m.roundNumber);
    this.startTimer();
  }

  private startTimer(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
    this.tickTimer = setInterval(() => this.elapsedSeconds.update((v) => v + 1), 1000);
  }

  private genTx(): string {
    return crypto.randomUUID();
  }

  selectMode(mode: GameMode): void {
    this.gameMode.set(mode);
    localStorage.setItem('tableMode', mode);
  }

  saveNames(): void {
    this.whiteName.set(this.whiteInput.trim() || 'Jugador 1');
    this.yellowName.set(this.yellowInput.trim() || 'Jugador 2');
    if (navigator.onLine) {
      this.api.renamePlayers(this.tableId(), this.whiteName(), this.yellowName(), this.genTx()).catch(() => undefined);
    }
  }

  async renamePlayer(color: 'white' | 'yellow', name: string): Promise<void> {
    const value = name.trim();
    if (!value) {
      return;
    }
    if (color === 'white') {
      this.whiteName.set(value);
    } else {
      this.yellowName.set(value);
    }
    if (navigator.onLine) {
      await this.api
        .renamePlayers(this.tableId(), this.whiteName(), this.yellowName(), this.genTx())
        .catch(() => undefined);
    }
  }

  async finishRound(): Promise<void> {
    const tx = this.genTx();
    if (navigator.onLine) {
      try {
        const r = await this.api.finishRound(this.tableId(), tx);
        this.roundNumber.set(r.roundNumber);
        this.lastRound.set(r.winnerName ? `Ronda ${r.roundNumber}: gana ${r.winnerName}` : `Ronda ${r.roundNumber}: empate`);
        setTimeout(() => this.lastRound.set(null), 4000);
      } catch {
        await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'round', tableId: this.tableId(), payload: {} });
      }
    } else {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'round', tableId: this.tableId(), payload: {} });
    }
    this.whiteScore.set(0);
    this.yellowScore.set(0);
  }

  formatAt(value: string | undefined): string {
    if (!value) {
      return '';
    }
    const d = new Date(value);
    return `${d.toLocaleDateString('es', { day: '2-digit', month: 'short' })} ${d.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' })}`;
  }

  async addScore(color: 'white' | 'yellow', delta: number): Promise<void> {
    const tx = this.genTx();
    const current = color === 'white' ? this.whiteScore() : this.yellowScore();
    const resulting = Math.max(0, current + delta);
    if (color === 'white') {
      this.whiteScore.set(resulting);
    } else {
      this.yellowScore.set(resulting);
    }

    if (!navigator.onLine) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'score', tableId: this.tableId(), payload: { playerColor: color, delta } });
      return;
    }
    try {
      await this.api.score(this.tableId(), color, delta, tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'score', tableId: this.tableId(), payload: { playerColor: color, delta } });
    }
  }

  async startSession(): Promise<void> {
    const tx = this.genTx();
    this.saveNames();
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.elapsedSeconds.set(0);
    this.consumptionTotal.set(0);
    this.consumptions.set([]);
    this.running.set(true);
    this.startTimer();

    const payload = { whitePlayerName: this.whiteName(), yellowPlayerName: this.yellowName(), gameMode: this.gameMode() };
    if (!navigator.onLine) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'start', tableId: this.tableId(), payload });
      return;
    }
    try {
      await this.api.startSession(this.tableId(), this.whiteName(), this.yellowName(), this.gameMode(), tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'start', tableId: this.tableId(), payload });
    }
  }

  async callWaiter(): Promise<void> {
    this.requestSent.set('waiter');
    if (navigator.onLine) {
      await this.api.callWaiter(this.tableId()).catch(() => undefined);
    }
    setTimeout(() => this.requestSent.set(null), 2500);
  }

  async requestCheck(): Promise<void> {
    this.requestSent.set('check');
    if (navigator.onLine) {
      await this.api.requestCheck(this.tableId()).catch(() => undefined);
    }
    setTimeout(() => this.requestSent.set(null), 2500);
  }

  async addConsumption(productId: string): Promise<void> {
    const product = this.products().find((p) => p.id === productId);
    if (!product) {
      return;
    }
    const tx = this.genTx();
    this.consumptions.update((list) => [...list, { id: crypto.randomUUID(), name: product.name, qty: 1, total: product.price, at: new Date().toISOString() }]);
    this.consumptionTotal.update((t) => t + product.price);

    if (!navigator.onLine) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'consumption', tableId: this.tableId(), payload: { productId, quantity: 1 } });
      return;
    }
    try {
      await this.api.addConsumption(this.tableId(), productId, 1, tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'consumption', tableId: this.tableId(), payload: { productId, quantity: 1 } });
    }
  }

  onRequestClose(): void {
    this.showConfirm.set(true);
  }

  onConfirmCancelled(): void {
    this.showConfirm.set(false);
  }

  async finishConfirmed(): Promise<void> {
    this.showConfirm.set(false);
    const tx = this.genTx();
    if (navigator.onLine) {
      try {
        await this.api.finishSession(this.tableId(), tx);
      } catch {
        await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'finish', tableId: this.tableId(), payload: {} });
      }
    } else {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'finish', tableId: this.tableId(), payload: {} });
    }
    this.resetUi();
  }

  private resetUi(): void {
    this.running.set(false);
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.elapsedSeconds.set(0);
    this.consumptionTotal.set(0);
    this.consumptions.set([]);
    this.roundNumber.set(0);
    this.whiteName.set('Jugador 1');
    this.yellowName.set('Jugador 2');
    this.whiteInput = 'Jugador 1';
    this.yellowInput = 'Jugador 2';
    if (this.tableId()) {
      this.router.navigate(['/play']);
    }
  }
}