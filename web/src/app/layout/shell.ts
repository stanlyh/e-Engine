import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Rol, Sesion } from '../core/auth/sesion';

interface ItemMenu {
  ruta: string;
  etiqueta: string;
  roles?: Rol[];
}

const MENU: ItemMenu[] = [
  { ruta: '/', etiqueta: 'Inicio' },
  { ruta: '/documentos', etiqueta: 'Mis documentos', roles: ['Usuario'] },
  { ruta: '/documentos/nuevo', etiqueta: 'Nuevo documento', roles: ['Usuario'] },
  { ruta: '/aprobaciones', etiqueta: 'Por aprobar', roles: ['Aprobador'] },
  { ruta: '/tesoreria', etiqueta: 'Tesorería', roles: ['Tesoreria'] },
  { ruta: '/admin', etiqueta: 'Administración', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-dvh bg-slate-50 text-slate-900 md:flex">
      <aside class="border-b border-slate-200 bg-white md:w-60 md:shrink-0 md:border-r md:border-b-0">
        <div class="flex items-center justify-between px-5 py-4">
          <a routerLink="/" class="text-lg font-semibold tracking-tight text-primario-700">Aprobaciones</a>
          <button
            type="button"
            class="rounded-md p-2 text-slate-600 hover:bg-slate-100 md:hidden"
            (click)="menuAbierto.set(!menuAbierto())"
            [attr.aria-expanded]="menuAbierto()"
            aria-label="Menú"
          >
            ☰
          </button>
        </div>
        <nav class="flex-col gap-1 px-3 pb-4 md:flex" [class.hidden]="!menuAbierto()" [class.flex]="menuAbierto()">
          @for (item of menu(); track item.ruta) {
            <a
              [routerLink]="item.ruta"
              routerLinkActive="bg-primario-50 text-primario-700 font-medium"
              [routerLinkActiveOptions]="{ exact: true }"
              class="rounded-md px-3 py-2 text-sm text-slate-700 hover:bg-slate-100"
              (click)="menuAbierto.set(false)"
            >
              {{ item.etiqueta }}
            </a>
          }
        </nav>
      </aside>

      <div class="flex min-w-0 flex-1 flex-col">
        <header class="flex items-center justify-end gap-3 border-b border-slate-200 bg-white px-6 py-3">
          @if (sesion.usuario(); as u) {
            <div class="text-right">
              <p class="text-sm font-medium">{{ u.nombre }}</p>
              <p class="text-xs text-slate-500">{{ u.roles.join(' · ') }}</p>
            </div>
          }
        </header>
        <main class="flex-1 p-4 md:p-8">
          @if (sesion.error(); as error) {
            <div class="rounded-lg border border-rechazado-200 bg-rechazado-50 p-4 text-rechazado-800">{{ error }}</div>
          } @else {
            <router-outlet />
          }
        </main>
      </div>
    </div>
  `,
})
export class Shell {
  protected readonly sesion = inject(Sesion);
  protected readonly menuAbierto = signal(false);
  protected readonly menu = computed(() =>
    this.sesion.usuario() ? MENU.filter((i) => !i.roles || this.sesion.tieneRol(...i.roles)) : [],
  );
}
