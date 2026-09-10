import { computed, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PlayerUiService {
  readonly tableName = signal('Mesa');
  readonly tableNumber = computed(() => {
    const match = this.tableName().match(/\d+/);
    return match ? match[0] : this.tableName();
  });

  setTableName(name: string): void {
    this.tableName.set(name);
  }

  reset(): void {
    this.tableName.set('Mesa');
  }
}
