import { Injectable, signal } from '@angular/core';

export type OfflineCommandType = 'start' | 'score' | 'players' | 'call-waiter' | 'request-check' | 'consumption' | 'finish';

export interface OfflineCommand {
  id?: string;
  transactionId: string;
  type: OfflineCommandType;
  tableId: string;
  payload: Record<string, unknown>;
  queuedAt: number;
}

const DB_NAME = 'billiard-offline';
const STORE = 'commands';

@Injectable({ providedIn: 'root' })
export class OfflineQueueService {
  private db?: IDBDatabase;
  readonly pendingCount = signal(0);

  async open(): Promise<void> {
    if (this.db) {
      return;
    }
    await new Promise<void>((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, 1);
      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains(STORE)) {
          const store = db.createObjectStore(STORE, { keyPath: 'id' });
          store.createIndex('transactionId', 'transactionId', { unique: true });
          store.createIndex('queuedAt', 'queuedAt');
        }
      };
      request.onsuccess = () => {
        this.db = request.result;
        this.db.onversionchange = () => this.db?.close();
        this.refreshCount();
        resolve();
      };
      request.onerror = () => reject(request.error);
    });
  }

  async enqueue(command: Omit<OfflineCommand, 'queuedAt'>): Promise<void> {
    await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = this.db!.transaction(STORE, 'readwrite');
      const store = tx.objectStore(STORE);
      const index = store.index('transactionId');
      const exists = index.get(command.transactionId);
      exists.onsuccess = () => {
        if (!exists.result) {
          store.add({ ...command, id: command.id ?? crypto.randomUUID(), queuedAt: Date.now() });
        }
      };
      tx.oncomplete = () => {
        this.refreshCount();
        resolve();
      };
      tx.onerror = () => reject(tx.error);
    });
  }

  async has(transactionId: string): Promise<boolean> {
    await this.open();
    return new Promise((resolve, reject) => {
      const tx = this.db!.transaction(STORE, 'readonly');
      const index = tx.objectStore(STORE).index('transactionId');
      const req = index.get(transactionId);
      req.onsuccess = () => resolve(!!req.result);
      req.onerror = () => reject(req.error);
    });
  }

  async all(): Promise<OfflineCommand[]> {
    await this.open();
    return new Promise((resolve, reject) => {
      const tx = this.db!.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).index('queuedAt').getAll();
      req.onsuccess = () => resolve(req.result ?? []);
      req.onerror = () => reject(req.error);
    });
  }

  async remove(id: string): Promise<void> {
    await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = this.db!.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).delete(id);
      tx.oncomplete = () => {
        this.refreshCount();
        resolve();
      };
      tx.onerror = () => reject(tx.error);
    });
  }

  private refreshCount(): void {
    if (!this.db) {
      return;
    }
    const tx = this.db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).count();
    req.onsuccess = () => this.pendingCount.set(req.result);
  }
}
