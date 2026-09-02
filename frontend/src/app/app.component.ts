import { Component, OnInit, signal } from '@angular/core';
import { RouterLink, RouterOutlet, Router } from '@angular/router';
import { NavigationEnd } from '@angular/router';

import { SignalRService } from './core/signalr.service';
import { OfflineQueueService } from './core/offline-queue.service';
import { OfflineSyncService } from './core/offline-sync.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  standalone: true,
})
export class AppComponent implements OnInit {
  readonly connected = this.signalr.connected;
  readonly pendingCommands = this.queue.pendingCount;
  readonly online = signal(navigator.onLine);
  readonly isAdminArea = signal(false);

  constructor(
    private readonly signalr: SignalRService,
    private readonly queue: OfflineQueueService,
    private readonly sync: OfflineSyncService,
    private readonly router: Router
  ) {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        const url = event.urlAfterRedirects;
        this.isAdminArea.set(url.startsWith('/admin') || url.startsWith('/super'));
      }
    });
  }

  async ngOnInit(): Promise<void> {
    window.addEventListener('online', () => {
      this.online.set(true);
      void this.sync.flush();
    });
    window.addEventListener('offline', () => this.online.set(false));
    await this.queue.open();
    await this.signalr.connect();
    void this.sync.flush();
  }
}