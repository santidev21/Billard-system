import { Injectable, inject } from '@angular/core';
import { OfflineQueueService, OfflineCommand } from './offline-queue.service';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class OfflineSyncService {
  private readonly queue = inject(OfflineQueueService);
  private readonly api = inject(ApiService);
  private flushing = false;

  async flush(): Promise<void> {
    if (this.flushing) return;
    if (!navigator.onLine) return;

    this.flushing = true;
    try {
      await this.queue.open();
      const commands = await this.queue.all();
      if (commands.length === 0) return;

      const ordered = [...commands].sort((a, b) => a.queuedAt - b.queuedAt);

      for (const cmd of ordered) {
        if (!cmd.tableId || cmd.tableId === '') {
          if (cmd.id) await this.queue.remove(cmd.id);
          continue;
        }
        try {
          await this.replayCommand(cmd);
          if (cmd.id) await this.queue.remove(cmd.id);
        } catch (e: any) {
          const status = e?.status;
          if (status && status >= 400 && status < 500) {
            if (cmd.id) await this.queue.remove(cmd.id);
            continue;
          }
          break;
        }
      }
    } finally {
      this.flushing = false;
    }
  }

  private async replayCommand(cmd: OfflineCommand): Promise<void> {
    const slug = cmd.slug || 'demo';
    const { type, tableId, transactionId, payload } = cmd;

    switch (type) {
      case 'start':
        await this.api.startSession(slug, tableId, payload['whitePlayerName'] as string || 'Jugador 1', payload['yellowPlayerName'] as string || 'Jugador 2', (payload['gameMode'] as 'Managed' | 'FreeMode') || 'FreeMode', transactionId);
        break;
      case 'score':
        await this.api.score(slug, tableId, payload['playerColor'] as 'white' | 'yellow', payload['delta'] as number, transactionId);
        break;
      case 'players':
        await this.api.renamePlayers(slug, tableId, payload['whitePlayerName'] as string, payload['yellowPlayerName'] as string, transactionId);
        break;
      case 'call-waiter':
        await this.api.callWaiter(slug, tableId);
        break;
      case 'request-check':
        await this.api.requestCheck(slug, tableId);
        break;
      case 'consumption':
        await this.api.addConsumption(slug, tableId, payload['productId'] as string, payload['quantity'] as number, transactionId);
        break;
      case 'finish':
        await this.api.finishSession(slug, tableId, transactionId);
        break;
      case 'round':
        await this.api.finishRound(slug, tableId, transactionId);
        break;
    }
  }
}
