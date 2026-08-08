import { Routes } from '@angular/router';
import { AuthGuard } from './core/auth.guard';

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
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'admin',
    canActivate: [AuthGuard],
    loadComponent: () => import('./features/admin/admin-layout.component').then((m) => m.AdminLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./features/admin/dashboard.component').then((m) => m.DashboardComponent) },
      { path: 'catalog', loadComponent: () => import('./features/catalog/catalog.component').then((m) => m.CatalogComponent) },
      { path: 'history', loadComponent: () => import('./features/history/history.component').then((m) => m.HistoryComponent) },
      { path: 'audit', loadComponent: () => import('./features/audit/audit.component').then((m) => m.AuditComponent) },
    ],
  },
  { path: 'catalog', redirectTo: '/admin/catalog', pathMatch: 'full' },
  { path: 'history', redirectTo: '/admin/history', pathMatch: 'full' },
  { path: 'audit', redirectTo: '/admin/audit', pathMatch: 'full' },
  { path: '**', redirectTo: '/play' },
];
