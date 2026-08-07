import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: '/play', pathMatch: 'full' },
  {
    path: 'play',
    loadComponent: () => import('./features/player/player.component').then((m) => m.PlayerComponent),
  },
  {
    path: 'tables/:id',
    loadComponent: () => import('./features/player/player.component').then((m) => m.PlayerComponent),
  },
  {
    path: 'admin',
    loadComponent: () => import('./features/admin/dashboard.component').then((m) => m.DashboardComponent),
  },
  {
    path: 'catalog',
    loadComponent: () => import('./features/catalog/catalog.component').then((m) => m.CatalogComponent),
  },
  {
    path: 'history',
    loadComponent: () => import('./features/history/history.component').then((m) => m.HistoryComponent),
  },
  {
    path: 'audit',
    loadComponent: () => import('./features/audit/audit.component').then((m) => m.AuditComponent),
  },
  { path: '**', redirectTo: '/play' },
];