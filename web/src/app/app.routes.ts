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
        loadComponent: () => import('./features/documentos/mis-documentos').then((m) => m.MisDocumentos),
      },
      {
        path: 'documentos/nuevo',
        title: 'Nuevo documento',
        canMatch: [requiereRol('Usuario')],
        loadComponent: () => import('./features/documentos/documento-editor').then((m) => m.DocumentoEditor),
      },
      {
        path: 'documentos/:id/editar',
        title: 'Editar documento',
        canMatch: [requiereRol('Usuario')],
        loadComponent: () => import('./features/documentos/documento-editor').then((m) => m.DocumentoEditor),
      },
      {
        // Lo ven también aprobadores y Tesorería; la API decide el acceso a cada documento.
        path: 'documentos/:id',
        title: 'Documento',
        loadComponent: () => import('./features/documentos/documento-detalle').then((m) => m.DocumentoDetallePagina),
      },
      {
        path: 'aprobaciones',
        title: 'Por aprobar',
        canMatch: [requiereRol('Aprobador')],
        loadComponent: () => import('./features/bandejas/por-aprobar').then((m) => m.PorAprobar),
      },
      {
        path: 'tesoreria',
        title: 'Tesorería',
        canMatch: [requiereRol('Tesoreria')],
        loadComponent: () => import('./features/bandejas/bandeja-tesoreria').then((m) => m.BandejaTesoreria),
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
