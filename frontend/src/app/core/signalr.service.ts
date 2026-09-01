import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

import { ConsumptionAmount, TableStatus } from './models';

const TOKEN_KEY = 'billiard-admin-token';

export interface PlayerScoredEvent {
  tableId: string;
  playerColor: 'white' | 'yellow';
  delta: number;
  newScore: number;
  totalCarambolas: number;
}

export interface ConsumptionAddedEvent {
  tableId: string;
  item: ConsumptionAmount;
  consumptionTotal: number;
}

export interface SessionEndedEvent {
  tableId: string;
  matchHistoryId: string;
  tableTotal?: number;
  consumptionTotal?: number;
  grandTotal: number;
  winnerName: string;
}

export interface TableStateUpdatedEvent {
  tableId: string;
  status: TableStatus;
}

export interface AdminNotification {
  type: 'waiter' | 'check';
  tableId: string;
  tableName: string;
  total?: number;
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hub?: signalR.HubConnection;
  private joinedTableId?: string;
  private adminJoined = false;
  readonly connected = signal(false);

  readonly playerScored = signal<PlayerScoredEvent | null>(null);
  readonly playerNamesChanged = signal<{ tableId: string; whitePlayerName: string; yellowPlayerName: string } | null>(null);
  readonly consumptionAdded = signal<ConsumptionAddedEvent | null>(null);
  readonly sessionStarted = signal<{ tableId: string; matchId: string } | null>(null);
  readonly sessionEnded = signal<SessionEndedEvent | null>(null);
  readonly tableStateUpdated = signal<TableStateUpdatedEvent | null>(null);
  readonly adminNotification = signal<AdminNotification | null>(null);

  async connect(): Promise<void> {
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const token = localStorage.getItem(TOKEN_KEY);

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tables', {
        accessTokenFactory: () => token ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.hub.on('PlayerScored', (payload: PlayerScoredEvent) => this.playerScored.set(payload));
    this.hub.on('PlayerNamesChanged', (payload: any) => this.playerNamesChanged.set(payload));
    this.hub.on('ConsumptionAdded', (payload: ConsumptionAddedEvent) => this.consumptionAdded.set(payload));
    this.hub.on('SessionStarted', (payload: any) => this.sessionStarted.set(payload));
    this.hub.on('SessionEnded', (payload: SessionEndedEvent) => this.sessionEnded.set(payload));
    this.hub.on('TableStateUpdated', (payload: TableStateUpdatedEvent) => this.tableStateUpdated.set(payload));
    this.hub.on('AdminNotification', (payload: AdminNotification) => this.adminNotification.set(payload));
    this.hub.on('AdminRequest', (payload: AdminNotification) => this.adminNotification.set(payload));

    this.hub.onreconnecting(() => this.connected.set(false));
    this.hub.onreconnected(() => {
      this.connected.set(true);
      this.rejoinGroups();
    });
    this.hub.onclose(() => this.connected.set(false));

    await this.hub.start();
    this.connected.set(true);
    this.rejoinGroups();
  }

  async joinTable(tableId: string): Promise<void> {
    this.joinedTableId = tableId;
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      await this.hub.invoke('JoinTableGroup', tableId);
    }
  }

  async leaveTable(tableId: string): Promise<void> {
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      await this.hub.invoke('LeaveTableGroup', tableId);
    }
  }

  async joinAdminGroup(): Promise<void> {
    this.adminJoined = true;
    const token = localStorage.getItem(TOKEN_KEY);
    if (this.hub?.state === signalR.HubConnectionState.Connected && token) {
      await this.hub.invoke('JoinAdminGroup', token);
    }
  }

  async leaveAdminGroup(): Promise<void> {
    this.adminJoined = false;
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      await this.hub.invoke('LeaveAdminGroup');
    }
  }

  private async rejoinGroups(): Promise<void> {
    if (this.joinedTableId && this.hub?.state === signalR.HubConnectionState.Connected) {
      await this.hub.invoke('JoinTableGroup', this.joinedTableId);
    }
    if (this.adminJoined) {
      await this.joinAdminGroup();
    }
  }
}
