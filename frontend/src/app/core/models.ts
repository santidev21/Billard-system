export type TableStatus = 'Available' | 'Occupied' | 'WaitingForWaiter' | 'WaitingForCheck' | 'OutOfService';
export type GameMode = 'Managed' | 'FreeMode';
export type PlayerColor = 'white' | 'yellow';

export interface TableResponse {
  id: string;
  name: string;
  code: string;
  status: TableStatus;
  hourlyRate: number;
  isActive: boolean;
  activeMatchId: string | null;
}

export interface ConsumptionAmount {
  id: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  total: number;
  createdAt: string;
}

export interface MatchDetail {
  id: string;
  whitePlayerName: string;
  yellowPlayerName: string;
  whiteScore: number;
  yellowScore: number;
  gameMode: GameMode;
  startedAt: string;
  elapsed: string;
  consumptionTotal: number;
  roundNumber: number;
  consumptions: ConsumptionAmount[];
  rounds: { roundNumber: number; whiteScore: number; yellowScore: number; winnerName: string | null; endedAt: string; durationSeconds: number }[];
}

export interface TableDetail {
  id: string;
  name: string;
  code: string;
  status: TableStatus;
  hourlyRate: number;
  isActive: boolean;
  activeMatchId: string | null;
  activeMatch: MatchDetail | null;
}

export interface Product {
  id: string;
  name: string;
  price: number;
}

export interface DashboardSummary {
  totalTables: number;
  availableTables: number;
  occupiedTables: number;
  salesToday: number;
  salesByGame: number;
  salesByConsumption: number;
}

export interface TopProduct {
  name: string;
  quantity: number;
  total: number;
}

export interface AuditLog {
  id: string;
  actionType: string;
  description: string;
  userId: string | null;
  tableId: string | null;
  matchId: string | null;
  transactionId: string | null;
  createdAt: string;
}

export interface MatchListItem {
  id: string;
  tableId: string;
  whitePlayerName: string;
  yellowPlayerName: string;
  whiteScore: number;
  yellowScore: number;
  totalCarambolas: number;
  gameMode: GameMode;
  startedAt: string;
  endedAt: string | null;
  grandTotal: number;
}

export type Settings = Record<string, string>;

export interface TableLiveState {
  table: TableResponse;
  detail: TableDetail | null;
  lastUpdate: number;
}

export interface UserInfo {
  name: string;
  role: string;
  tenantName: string | null;
  tenantSlug: string | null;
  mustChangePassword: boolean;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  userName: string;
  role: string;
  tenantName: string | null;
  tenantSlug: string | null;
  mustChangePassword: boolean;
}

export interface LocalInfo {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  tableCount: number;
  userCount: number;
}

export interface RecoveryCode {
  id: string;
  tenantName: string;
  userName: string;
  createdAt: string;
  expiresAt: string;
}
