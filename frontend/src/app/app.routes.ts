import { Routes } from '@angular/router';
import { authGuard, loginGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'carga' },
  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'carga',
    canActivate: [authGuard],
    loadComponent: () => import('./features/upload/upload.component').then((m) => m.UploadComponent),
  },
  {
    path: 'registros',
    canActivate: [authGuard],
    loadComponent: () => import('./features/records/records.component').then((m) => m.RecordsComponent),
  },
  {
    path: 'cuarentena',
    canActivate: [authGuard],
    loadComponent: () => import('./features/quarantine/quarantine.component').then((m) => m.QuarantineComponent),
  },
  { path: '**', redirectTo: 'carga' },
];
