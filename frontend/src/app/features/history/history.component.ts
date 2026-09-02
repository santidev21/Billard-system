import { Component, inject, OnInit, signal } from '@angular/core';

import { ApiService } from '../../core/api.service';
import { MatchListItem } from '../../core/models';
import { fmtMoney } from '../../core/format';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-history',
  imports: [SpinnerComponent],
  templateUrl: './history.component.html',
  styleUrls: ['./history.component.css'],
  standalone: true,
})
export class HistoryComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly fmtMoney = fmtMoney;
  readonly matches = signal<MatchListItem[]>([]);
  readonly loading = signal(false);
  readonly selectedMatch = signal<import('../../core/models').MatchDetail | null>(null);
  readonly loadingDetail = signal(false);
  readonly showRounds = signal(false);
  readonly detailError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      this.matches.set(await this.api.getMatches());
    } finally {
      this.loading.set(false);
    }
  }

  gameMode(mode: string): string {
    return mode === 'FreeMode' ? 'Libre' : 'Administrado';
  }

  duration(m: MatchListItem): string {
    if (!m.endedAt) {
      return '—';
    }
    const start = new Date(m.startedAt).getTime();
    const end = new Date(m.endedAt).getTime();
    return formatDuration((end - start) / 1000);
  }

  async openMatch(id: string): Promise<void> {
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.showRounds.set(true);
    this.selectedMatch.set(null);
    try {
      const detail = await this.api.getMatch(id);
      this.selectedMatch.set(detail);
    } catch (e: any) {
      this.detailError.set(e?.error?.message ?? 'No se pudo cargar el detalle de la partida.');
    } finally {
      this.loadingDetail.set(false);
    }
  }

  closeRounds(): void {
    this.showRounds.set(false);
    this.selectedMatch.set(null);
    this.detailError.set(null);
  }

  formatRoundDuration(durationSeconds: number | null | undefined): string {
    if (durationSeconds == null) return '—';
    const s = Math.max(0, Math.floor(durationSeconds));
    const m = String(Math.floor(s / 60)).padStart(2, '0');
    const sec = String(s % 60).padStart(2, '0');
    return `${m}:${sec}`;
  }
}

function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const h = String(Math.floor(s / 3600)).padStart(2, '0');
  const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
  return `${h}:${m}`;
}