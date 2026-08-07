import { Component, inject, OnInit, signal } from '@angular/core';

import { ApiService } from '../../core/api.service';
import { AuditLog } from '../../core/models';

@Component({
  selector: 'app-audit',
  templateUrl: './audit.component.html',
  styleUrls: ['./audit.component.css'],
  standalone: true,
})
export class AuditComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly logs = signal<AuditLog[]>([]);

  async ngOnInit(): Promise<void> {
    this.logs.set(await this.api.getAuditLogs());
  }

  time(s: string): string {
    return new Date(s).toLocaleString();
  }
}