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
}

function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const h = String(Math.floor(s / 3600)).padStart(2, '0');
  const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
  return `${h}:${m}`;
}