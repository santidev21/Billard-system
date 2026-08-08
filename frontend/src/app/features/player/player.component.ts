import { Component, computed, effect, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { SignalRService } from '../../core/signalr.service';
import { OfflineQueueService } from '../../core/offline-queue.service';
import { GameMode, TableDetail } from '../../core/models';
import { CameraViewComponent } from './camera-view.component';
import { SpinnerComponent } from '../../shared/spinner.component';

interface UiConsumption {
  id: string;
  name: string;
  qty: number;
  total: number;
  at: string;
}

@Component({
  selector: 'app-player',
  imports: [FormsModule, CameraViewComponent, SpinnerComponent],
  templateUrl: './player.component.html',
  styleUrls: ['./player.component.css'],
  standalone: true,
})
export class PlayerComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalRService);
  private readonly queue = inject(OfflineQueueService);
  private readonly route = inject(ActivatedRoute);

  readonly gameMode = signal<GameMode>('Managed');
  readonly blockedMsg = signal<string | null>(null);
  readonly loading = signal(false);
  readonly showEnded = signal(false);
  readonly endedSummary = signal<{ time: string; consumptionTotal: number; grandTotal: number } | null>(null);
  readonly whiteName = signal('Jugador 1');
  readonly yellowName = signal('Jugador 2');
  readonly whiteScore = signal(0);
  readonly yellowScore = signal(0);
  readonly startedAt = signal<number | null>(null);
  readonly now = signal(Date.now());
  readonly consumptionTotal = signal(0);
  readonly consumptions = signal<UiConsumption[]>([]);
  readonly tableId = signal('');
  readonly tableName = signal('Mesa');
  readonly running = signal(false);
  readonly showConfirm = signal(false);
  readonly showConsumptionLog = signal(false);
  readonly roundNumber = signal(0);
  readonly lastRound = signal<string | null>(null);
  readonly showRounds = signal(false);
  readonly rounds = signal<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string; duration: string }[] } | null>(null);
  readonly requestSent = signal<'waiter' | 'check' | null>(null);
  readonly products = signal<{ id: string; name: string; price: number }[]>([]);

  readonly totalCarambolas = computed(() => this.whiteScore() + this.yellowScore());
  readonly tableNumber = computed(() => {
    const match = this.tableName().match(/\d+/);
    return match ? match[0] : this.tableName();
  });
  readonly elapsedSeconds = computed(() => {
    const start = this.startedAt();
    return start ? Math.max(0, Math.floor((this.now() - start) / 1000)) : 0;
  });
  readonly elapsed = computed(() => {
    const s = this.elapsedSeconds();
    const h = String(Math.floor(s / 3600)).padStart(2, '0');
    const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
    return `${h}:${m}`;
  });
  readonly elapsedMinutes = computed(() => Math.floor(this.elapsedSeconds() / 60));

  readonly hourlyRate = signal(String(localStorage.getItem('defaultRate') ?? '12000'));

  readonly timeCost = computed(() => {
    const rate = Number(this.hourlyRate());
    return Math.floor((this.elapsedMinutes() / 60) * rate);
  });
  readonly grandTotal = computed(() => this.timeCost() + this.consumptionTotal());
  readonly timeCostText = computed(() => this.fmtMoney(this.timeCost()));
  readonly consumptionTotalText = computed(() => this.fmtMoney(this.consumptionTotal()));
  readonly grandTotalText = computed(() => this.fmtMoney(this.grandTotal()));

  private fmtMoney(value: number): string {
    return String(Math.round(value)).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
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
  private pollTimer: ReturnType<typeof setInterval> | undefined;

  constructor(
    private readonly router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    this.route.params.subscribe(async (params) => {
      this.loading.set(true);
      const id = params['id'];
      this.tableId.set(id ?? '');
      // /tables/:id siempre es modo administrado; /play sin id es modo libre.
      this.gameMode.set(id ? 'Managed' : 'FreeMode');

      if (id) {
        await this.resolveAndSetTable(id);
        if (this.tableId()) {
          await this.signalr.joinTable(this.tableId());
        }
      } else {
        await this.autoSelectTable();
      }
      this.loading.set(false);

      this.startPolling();
    });
    await this.loadProducts();

    effect(() => {
      this.signalr.tableStateUpdated();
      if (this.tableId()) {
        void this.refreshFromServer();
      }
    });
    effect(() => {
      const c = this.signalr.consumptionAdded();
      if (c && c.tableId === this.tableId()) {
        this.consumptionTotal.set(c.consumptionTotal);
      }
    });
    effect(() => {
      const p = this.signalr.playerScored();
      if (p && p.tableId === this.tableId()) {
        if (p.playerColor === 'white') {
          this.whiteScore.set(p.newScore);
        } else {
          this.yellowScore.set(p.newScore);
        }
      }
    });
    effect(() => {
      const n = this.signalr.playerNamesChanged();
      if (n && n.tableId === this.tableId()) {
        this.whiteName.set(n.whitePlayerName);
        this.yellowName.set(n.yellowPlayerName);
      }
    });
    effect(() => {
      const e = this.signalr.sessionEnded();
      if (e && e.tableId === this.tableId()) {
        this.endedSummary.set({
          time: this.elapsed(),
          consumptionTotal: e.consumptionTotal ?? 0,
          grandTotal: e.grandTotal ?? 0,
        });
        this.showEnded.set(true);
      }
    });
    effect(() => {
      const s = this.signalr.sessionStarted();
      if (s && s.tableId === this.tableId()) {
        this.showEnded.set(false);
        this.endedSummary.set(null);
        void this.refreshFromServer();
      }
    });
  }

  closeEnded(): void {
    this.showEnded.set(false);
    this.endedSummary.set(null);
  }

  private async refreshFromServer(): Promise<void> {
    if (!this.tableId()) {
      return;
    }
    try {
      const detail = await this.api.getTable(this.tableId());
      if (detail.activeMatch) {
        this.applyDetail(detail);
      } else if (this.running() && this.startedAt()) {
        this.endedSummary.set({
          time: this.elapsed(),
          consumptionTotal: this.consumptionTotal(),
          grandTotal: this.grandTotal(),
        });
        this.showEnded.set(true);
        this.running.set(false);
        this.whiteScore.set(0);
        this.yellowScore.set(0);
        this.startedAt.set(null);
      } else {
        this.running.set(false);
        this.whiteScore.set(0);
        this.yellowScore.set(0);
        this.startedAt.set(null);
      }
    } catch {
      // offline
    }
  }

  private async resolveAndSetTable(identifier: string): Promise<void> {
    try {
      const detail = await this.api.getTable(identifier);
      this.tableId.set(detail.id);
      this.tableName.set(detail.name);
      this.hourlyRate.set(String(detail.hourlyRate));
      localStorage.setItem('tableName', detail.name);
      localStorage.setItem('defaultRate', String(detail.hourlyRate));
      this.applyDetail(detail);
    } catch {
      // offline: rely on cached defaults
    }
  }

  private async autoSelectTable(): Promise<void> {
    try {
      const tables = await this.api.getTables();
      const pick = tables.find((t) => t.status === 'Available' && t.isActive) ?? tables.find((t) => t.isActive) ?? tables[0];
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
      this.products.set(await this.api.getProducts());
    } catch {
      // offline
    }
  }

  ngOnDestroy(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  private async loadTable(id: string): Promise<void> {
    try {
      const detail = await this.api.getTable(id);
      this.tableName.set(detail.name);
      this.hourlyRate.set(String(detail.hourlyRate));
      localStorage.setItem('tableName', detail.name);
      localStorage.setItem('defaultRate', String(detail.hourlyRate));
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
      this.startedAt.set(null);
      return;
    }
    const m = detail.activeMatch;
    this.running.set(true);
    this.whiteName.set(m.whitePlayerName);
    this.yellowName.set(m.yellowPlayerName);
    this.whiteScore.set(m.whiteScore);
    this.yellowScore.set(m.yellowScore);
    this.startedAt.set(new Date(m.startedAt).getTime());
    this.consumptionTotal.set(m.consumptionTotal);
    this.consumptions.set(m.consumptions.map((c) => ({ id: c.id, name: c.productName, qty: c.quantity, total: c.total, at: c.createdAt })));
    this.roundNumber.set(m.roundNumber);
    this.startTimer();
  }

  private startTimer(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
    this.now.set(Date.now());
    this.tickTimer = setInterval(() => this.now.set(Date.now()), 1000);
  }

  private startPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
    this.pollTimer = setInterval(() => {
      if (this.tableId()) {
        void this.refreshFromServer();
      }
    }, 8000);
  }

  private genTx(): string {
    return crypto.randomUUID();
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

  async showRoundHistory(): Promise<void> {
    try {
      const data = await this.api.getRounds(this.tableId());
      this.rounds.set(data);
      this.showRounds.set(true);
    } catch {
      // offline
    }
  }

  closeRounds(): void {
    this.showRounds.set(false);
    this.rounds.set(null);
  }

  formatDuration(duration: string | null | undefined): string {
    if (!duration) {
      return '—';
    }
    const matchNum = duration.match(/(\d+):(\d+):(\d+)/);
    if (matchNum) {
      const h = Number(matchNum[1]);
      const m = Number(matchNum[2]);
      const s = Number(matchNum[3]);
      const mm = h * 60 + m;
      return `${String(mm).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    }
    return duration;
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
    this.startedAt.set(Date.now());
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
    this.endedSummary.set({
      time: this.elapsed(),
      consumptionTotal: this.consumptionTotal(),
      grandTotal: this.grandTotal(),
    });
    this.showEnded.set(true);
    this.resetUi();
  }

  private resetUi(): void {
    this.running.set(false);
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.startedAt.set(null);
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