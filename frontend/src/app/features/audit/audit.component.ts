import { Component, inject, OnInit, signal } from '@angular/core';

import { ApiService } from '../../core/api.service';
import { AuditLog } from '../../core/models';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-audit',
  imports: [SpinnerComponent],
  templateUrl: './audit.component.html',
  styleUrls: ['./audit.component.css'],
  standalone: true,
})
export class AuditComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly logs = signal<AuditLog[]>([]);
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      this.logs.set(await this.api.getAuditLogs());
    } finally {
      this.loading.set(false);
    }
  }

  time(s: string): string {
    return new Date(s).toLocaleString();
  }
}