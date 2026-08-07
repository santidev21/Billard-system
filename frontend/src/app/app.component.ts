import { Component, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { signal } from '@angular/core';

import { SignalRService } from './core/signalr.service';
import { OfflineQueueService } from './core/offline-queue.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  standalone: true,
})
export class AppComponent implements OnInit {
  readonly connected = this.signalr.connected;
  readonly pendingCommands = this.queue.pendingCount;
  readonly online = signal(navigator.onLine);

  constructor(
    private readonly signalr: SignalRService,
    private readonly queue: OfflineQueueService
  ) {}

  async ngOnInit(): Promise<void> {
    window.addEventListener('online', () => this.online.set(true));
    window.addEventListener('offline', () => this.online.set(false));
    await this.queue.open();
    await this.signalr.connect();
  }
}