import { Routes } from '@angular/router';
import { requiereRol } from './core/auth/rol.guard';
import { Shell } from './layout/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', title: 'Inicio', loadComponent: () => import('./features/home/home').then((m) => m.Home) },
      {
        path: 'documentos',
        title: 'Mis documentos',
        canMatch: [requiereRol('Usuario')],
        loadComponent: () => import('./features/pendiente/pendiente').then((m) => m.Pendiente),
        data: { titulo: 'Mis documentos', fase: 5 },
      },
      {
        path: 'documentos/nuevo',
        title: 'Nuevo documento',
        canMatch: [requiereRol('Usuario')],
        loadComponent: () => import('./features/pendiente/pendiente').then((m) => m.Pendiente),
        data: { titulo: 'Nuevo documento', fase: 5 },
      },
      {
        path: 'aprobaciones',
        title: 'Por aprobar',
        canMatch: [requiereRol('Aprobador')],
        loadComponent: () => import('./features/pendiente/pendiente').then((m) => m.Pendiente),
        data: { titulo: 'Documentos por aprobar', fase: 6 },
      },
      {
        path: 'tesoreria',
        title: 'Tesorería',
        canMatch: [requiereRol('Tesoreria')],
        loadComponent: () => import('./features/pendiente/pendiente').then((m) => m.Pendiente),
        data: { titulo: 'Bandeja de Tesorería', fase: 6 },
      },
      {
        path: 'admin',
        title: 'Administración',
        canMatch: [requiereRol('Admin')],
        loadComponent: () => import('./features/pendiente/pendiente').then((m) => m.Pendiente),
        data: { titulo: 'Administración', fase: 8 },
      },
      {
        path: 'sin-acceso',
        title: 'Sin acceso',
        loadComponent: () => import('./features/pendiente/sin-acceso').then((m) => m.SinAcceso),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
