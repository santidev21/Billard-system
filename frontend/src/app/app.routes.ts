import { Routes } from '@angular/router';
import { AuthGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: 'play', loadComponent: () => import('./features/player/player.component').then(m => m.PlayerComponent) },
  { path: 'forgot-password', loadComponent: () => import('./features/login/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'reset-password', loadComponent: () => import('./features/login/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: 'force-password', loadComponent: () => import('./features/login/force-password.component').then(m => m.ForcePasswordComponent) },
  { path: 't/:slug/play', loadComponent: () => import('./features/player/player.component').then(m => m.PlayerComponent) },
  { path: 't/:slug/tables/:id', loadComponent: () => import('./features/player/player.component').then(m => m.PlayerComponent) },
  { path: 'admin', canActivate: [AuthGuard], loadComponent: () => import('./features/admin/admin-layout.component').then(m => m.AdminLayoutComponent), children: [
    { path: '', pathMatch: 'full', loadComponent: () => import('./features/admin/dashboard.component').then(m => m.DashboardComponent) },
    { path: 'catalog', loadComponent: () => import('./features/catalog/catalog.component').then(m => m.CatalogComponent) },
    { path: 'history', loadComponent: () => import('./features/history/history.component').then(m => m.HistoryComponent) },
    { path: 'audit', loadComponent: () => import('./features/audit/audit.component').then(m => m.AuditComponent) },
  ]},
  { path: 'super', loadComponent: () => import('./features/super/super-layout.component').then(m => m.SuperLayoutComponent) },
  { path: '**', redirectTo: '/login' },
];
