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

  readonly gameMode = signal<GameMode>('FreeMode');
  readonly blockedMsg = signal<string | null>(null);
  readonly loading = signal(false);
  readonly showEnded = signal(false);
  readonly endedSummary = signal<{ time: string; consumptionTotal: number; grandTotal: number; whiteScore?: number; yellowScore?: number; whiteName?: string; yellowName?: string } | null>(null);
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
  readonly rounds = signal<{ whiteRounds: number; yellowRounds: number; currentRoundNumber: number; rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string; durationSeconds: number }[] } | null>(null);
  readonly roundStartedAt = signal<number | null>(null);
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
  readonly roundElapsedSeconds = computed(() => {
    const start = this.roundStartedAt();
    return start ? Math.max(0, Math.floor((this.now() - start) / 1000)) : 0;
  });
  readonly roundElapsed = computed(() => {
    const s = this.roundElapsedSeconds();
    const m = String(Math.floor(s / 60)).padStart(2, '0');
    const sec = String(s % 60).padStart(2, '0');
    return `${m}:${sec}`;
  });

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
  private slug = '';

  constructor(
    private readonly router: Router
  ) {
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
        this.signalr.clearSessionEnded();
        if (this.gameMode() === 'FreeMode') {
          return;
        }
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
        this.signalr.clearSessionEnded();
        this.showEnded.set(false);
        this.endedSummary.set(null);
        void this.refreshFromServer();
      }
    });
  }

  async ngOnInit(): Promise<void> {
    this.route.params.subscribe(async (params) => {
      this.loading.set(true);
      this.resetState();
      const slug = params['slug'];
      const id = params['id'];

      if (slug) {
        this.slug = slug;
        this.tableId.set(id ?? '');
        this.gameMode.set(id ? 'Managed' : 'FreeMode');
        if (id) {
          await this.resolveAndSetTable(slug, id);
          if (this.tableId()) {
            await this.signalr.joinTable(this.tableId());
          }
        } else {
          await this.autoSelectTable(slug);
          await this.loadProductsForSlug(slug);
          if (this.gameMode() === 'FreeMode' && this.tableId()) {
            const tableId = this.tableId();
            try {
              const detail = await this.api.getTenantTable(slug, tableId);
              const shouldStart = !detail.activeMatch || detail.activeMatch.gameMode === 'FreeMode';
              if (shouldStart) {
                await this.startSession();
              }
            } catch {}
          }
        }
      } else {
        this.slug = 'demo';
        this.tableId.set('');
        this.gameMode.set('FreeMode');
        await this.autoSelectTable('demo');
        await this.loadProductsForSlug('demo');
        if (this.tableId()) {
          try {
            const detail = await this.api.getTenantTable('demo', this.tableId());
            const shouldStart = !detail.activeMatch || detail.activeMatch.gameMode === 'FreeMode';
            if (shouldStart) {
              await this.startSession();
            }
          } catch {}
        }
      }

      this.loading.set(false);
      this.startPolling();
    });
  }

  closeEnded(): void {
    this.showEnded.set(false);
    this.endedSummary.set(null);
    this.signalr.clearSessionEnded();
  }

  async startNewFreeGame(): Promise<void> {
    this.closeEnded();
    await this.startSession();
  }

  private resetState(): void {
    this.running.set(false);
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.startedAt.set(null);
    this.roundStartedAt.set(null);
    this.consumptionTotal.set(0);
    this.consumptions.set([]);
    this.roundNumber.set(0);
    this.lastRound.set(null);
    this.showRounds.set(false);
    this.rounds.set(null);
    this.showConsumptionLog.set(false);
    this.showConfirm.set(false);
    this.showEnded.set(false);
    this.endedSummary.set(null);
    this.requestSent.set(null);
    this.whiteName.set('Jugador 1');
    this.yellowName.set('Jugador 2');
    this.whiteInput = 'Jugador 1';
    this.yellowInput = 'Jugador 2';
    if (this.tickTimer) clearInterval(this.tickTimer);
    if (this.pollTimer) clearInterval(this.pollTimer);
  }

  private async refreshFromServer(): Promise<void> {
    if (!this.tableId() || !this.slug) {
      return;
    }
    try {
      const detail = await this.api.getTenantTable(this.slug, this.tableId());
      if (detail.activeMatch) {
        if (this.gameMode() === 'FreeMode' && detail.activeMatch.gameMode !== 'FreeMode') {
          this.running.set(false);
          this.whiteScore.set(0);
          this.yellowScore.set(0);
          this.startedAt.set(null);
          this.roundStartedAt.set(null);
          this.blockedMsg.set(`La mesa ${this.tableName()} está ocupada por una partida administrada.`);
          return;
        }
        this.blockedMsg.set(null);
        this.applyDetail(detail);
      } else if (this.running() && this.startedAt()) {
        const elapsedMs = Date.now() - this.startedAt()!;
        if (elapsedMs < 5000) {
          return;
        }
        if (this.gameMode() === 'FreeMode') {
          this.running.set(false);
          this.whiteScore.set(0);
          this.yellowScore.set(0);
          this.startedAt.set(null);
          this.roundStartedAt.set(null);
          try {
            await this.startSession();
          } catch {
            // ignore, will retry on next poll
          }
          return;
        }
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
        this.roundStartedAt.set(null);
      } else {
        this.running.set(false);
        this.whiteScore.set(0);
        this.yellowScore.set(0);
        this.startedAt.set(null);
        this.roundStartedAt.set(null);
        if (this.gameMode() === 'FreeMode') {
          this.blockedMsg.set(null);
        }
      }
    } catch {
      // offline
    }
  }

  private async resolveAndSetTable(slug: string, identifier: string): Promise<void> {
    try {
      const detail = await this.api.getTenantTable(slug, identifier);
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

  private async autoSelectTable(slug: string): Promise<void> {
    try {
      const tables = await this.api.getTenantTables(slug);
      const pick = tables.find((t) => t.status === 'Available' && t.isActive) ?? tables.find((t) => t.isActive) ?? tables[0];
      if (pick) {
        this.tableId.set(pick.id);
        this.tableName.set(pick.name);
        this.hourlyRate.set(String(pick.hourlyRate));
        localStorage.setItem('tableName', pick.name);
        localStorage.setItem('defaultRate', String(pick.hourlyRate));
        await this.signalr.joinTable(pick.id);
      }
    } catch {
      // offline
    }
  }

  private async loadProductsForSlug(slug: string): Promise<void> {
    try {
      this.products.set(await this.api.getTenantProducts(slug));
    } catch {
      // offline
    }
  }

  private async loadProducts(): Promise<void> {
    try {
      if (this.slug) {
        this.products.set(await this.api.getTenantProducts(this.slug));
      } else {
        this.products.set(await this.api.getProducts());
      }
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

  private applyDetail(detail: TableDetail): void {
    if (!detail.activeMatch) {
      this.running.set(false);
      this.whiteScore.set(0);
      this.yellowScore.set(0);
      this.startedAt.set(null);
      this.roundStartedAt.set(null);
      return;
    }
    const m = detail.activeMatch;
    this.running.set(true);
    this.whiteName.set(m.whitePlayerName);
    this.yellowName.set(m.yellowPlayerName);
    this.whiteScore.set(m.whiteScore);
    this.yellowScore.set(m.yellowScore);
    const serverStart = new Date(m.startedAt).getTime();
    const currentStart = this.startedAt();
    // monotonic guard: don't let a stale poll (old StartedAt) overwrite a just-started session (00:00 -> old time bug in FreeMode)
    if (currentStart === null || serverStart > currentStart || m.roundNumber > this.roundNumber()) {
      this.startedAt.set(serverStart);
    } else if (serverStart !== currentStart && currentStart !== null && Math.abs(serverStart - currentStart) < 5000) {
      // small clock skew (server vs client Date.now) — sync to server
      this.startedAt.set(serverStart);
    }
    // round timer: start from last round's end or match start — monotonic to avoid stale poll overwriting a just-closed round (00:00 -> 00:41 bug)
    const lastRoundEnd = m.rounds.length > 0 ? new Date(m.rounds[m.rounds.length - 1].endedAt).getTime() : serverStart;
    const currentRoundStart = this.roundStartedAt();
    if (currentRoundStart === null || lastRoundEnd > currentRoundStart) {
      this.roundStartedAt.set(lastRoundEnd);
    } else if (m.roundNumber > this.roundNumber() && lastRoundEnd !== currentRoundStart) {
      // roundNumber grew but computed start didn't advance (edge), force update
      this.roundStartedAt.set(lastRoundEnd);
    }
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
    if (navigator.onLine && this.slug) {
      this.api.renamePlayers(this.slug, this.tableId(), this.whiteName(), this.yellowName(), this.genTx()).catch(() => undefined);
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
    if (navigator.onLine && this.slug) {
      await this.api
        .renamePlayers(this.slug, this.tableId(), this.whiteName(), this.yellowName(), this.genTx())
        .catch(() => undefined);
    }
  }

  async finishRound(): Promise<void> {
    const tx = this.genTx();
    if (navigator.onLine && this.slug) {
      try {
        const r = await this.api.finishRound(this.slug, this.tableId(), tx);
        this.roundNumber.set(r.roundNumber);
        this.lastRound.set(r.winnerName ? `Ronda ${r.roundNumber}: gana ${r.winnerName}` : `Ronda ${r.roundNumber}: empate`);
        setTimeout(() => this.lastRound.set(null), 4000);
      } catch {
        await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'round', slug: this.slug, tableId: this.tableId(), payload: {} });
      }
    } else {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'round', slug: this.slug, tableId: this.tableId(), payload: {} });
    }
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.roundStartedAt.set(Date.now());
  }

  async showRoundHistory(): Promise<void> {
    if (!this.slug) {
      return;
    }
    try {
      const data = await this.api.getRounds(this.slug, this.tableId());
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

  formatDuration(durationSeconds: number | null | undefined): string {
    if (durationSeconds == null) return '—';
    const s = Math.max(0, Math.floor(durationSeconds));
    const m = String(Math.floor(s / 60)).padStart(2, '0');
    const sec = String(s % 60).padStart(2, '0');
    return `${m}:${sec}`;
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

    if (!navigator.onLine || !this.slug) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'score', slug: this.slug, tableId: this.tableId(), payload: { playerColor: color, delta } });
      return;
    }
    try {
      await this.api.score(this.slug, this.tableId(), color, delta, tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'score', slug: this.slug, tableId: this.tableId(), payload: { playerColor: color, delta } });
    }
  }

  async startSession(): Promise<void> {
    if (!this.tableId()) {
      await this.autoSelectTable(this.slug);
      if (!this.tableId()) {
        this.blockedMsg.set('No hay mesa disponible. Esperá a que el mesero asigne una.');
        setTimeout(() => this.blockedMsg.set(null), 4000);
        return;
      }
    }

    const tx = this.genTx();
    this.saveNames();
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.startedAt.set(Date.now());
    this.roundStartedAt.set(Date.now());
    this.consumptionTotal.set(0);
    this.consumptions.set([]);
    this.running.set(true);
    this.startTimer();

    const payload = { whitePlayerName: this.whiteName(), yellowPlayerName: this.yellowName(), gameMode: this.gameMode() };
    if (!navigator.onLine || !this.slug) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'start', slug: this.slug, tableId: this.tableId(), payload });
      return;
    }
    try {
      await this.api.startSession(this.slug, this.tableId(), this.whiteName(), this.yellowName(), this.gameMode(), tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'start', slug: this.slug, tableId: this.tableId(), payload });
    }
  }

  async callWaiter(): Promise<void> {
    this.requestSent.set('waiter');
    if (navigator.onLine && this.slug) {
      await this.api.callWaiter(this.slug, this.tableId()).catch(() => undefined);
    }
    setTimeout(() => this.requestSent.set(null), 2500);
  }

  async requestCheck(): Promise<void> {
    this.requestSent.set('check');
    if (navigator.onLine && this.slug) {
      await this.api.requestCheck(this.slug, this.tableId()).catch(() => undefined);
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

    if (!navigator.onLine || !this.slug) {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'consumption', slug: this.slug, tableId: this.tableId(), payload: { productId, quantity: 1 } });
      return;
    }
    try {
      await this.api.addConsumption(this.slug, this.tableId(), productId, 1, tx);
    } catch {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'consumption', slug: this.slug, tableId: this.tableId(), payload: { productId, quantity: 1 } });
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
    const finalWhite = this.whiteScore();
    const finalYellow = this.yellowScore();
    const finalWhiteName = this.whiteName();
    const finalYellowName = this.yellowName();
    const finalTime = this.elapsed();
    const finalConsumption = this.consumptionTotal();
    const finalGrand = this.grandTotal();
    if (navigator.onLine && this.slug) {
      try {
        await this.api.finishSession(this.slug, this.tableId(), tx);
      } catch {
        await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'finish', slug: this.slug, tableId: this.tableId(), payload: {} });
      }
    } else {
      await this.queue.enqueue({ id: crypto.randomUUID(), transactionId: tx, type: 'finish', slug: this.slug, tableId: this.tableId(), payload: {} });
    }
    this.endedSummary.set({
      time: finalTime,
      consumptionTotal: finalConsumption,
      grandTotal: finalGrand,
      whiteScore: finalWhite,
      yellowScore: finalYellow,
      whiteName: finalWhiteName,
      yellowName: finalYellowName,
    });
    this.showEnded.set(true);
    if (this.gameMode() === 'FreeMode') {
      // keep modal visible with final score, just reset running state
      this.running.set(false);
      this.whiteScore.set(0);
      this.yellowScore.set(0);
      this.startedAt.set(null);
      this.roundStartedAt.set(null);
      this.consumptionTotal.set(0);
      this.consumptions.set([]);
      this.roundNumber.set(0);
    } else {
      this.resetUi();
    }
  }

  private resetUi(): void {
    this.running.set(false);
    this.whiteScore.set(0);
    this.yellowScore.set(0);
    this.startedAt.set(null);
    this.roundStartedAt.set(null);
    this.consumptionTotal.set(0);
    this.consumptions.set([]);
    this.roundNumber.set(0);
    this.whiteName.set('Jugador 1');
    this.yellowName.set('Jugador 2');
    this.whiteInput = 'Jugador 1';
    this.yellowInput = 'Jugador 2';
    if (this.slug) {
      this.router.navigate([`/t/${this.slug}/play`]);
    } else {
      this.router.navigate(['/play']);
    }
  }
}
