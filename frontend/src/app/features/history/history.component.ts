import { Component, inject, OnInit, signal } from '@angular/core';

import { ApiService } from '../../core/api.service';
import { MatchListItem } from '../../core/models';

@Component({
  selector: 'app-history',
  templateUrl: './history.component.html',
  styleUrls: ['./history.component.css'],
  standalone: true,
})
export class HistoryComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly matches = signal<MatchListItem[]>([]);

  async ngOnInit(): Promise<void> {
    this.matches.set(await this.api.getMatches());
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
  const h = String(Math.floor(s / 3600));
  const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
  const sec = String(s % 60).padStart(2, '0');
  return `${h}:${m}:${sec}`;
}