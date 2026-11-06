import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Pagina, PendienteAprobacion, ResueltoPorMi } from '../../core/api/modelos';
import { HaceTiempoPipe } from '../../shared/hace-tiempo.pipe';
import { EstadoBadge } from '../../shared/ui/estado-badge';
import { Paginacion } from '../../shared/ui/paginacion';

type Vista = 'pendientes' | 'resueltos';

@Component({
  selector: 'app-por-aprobar',
  imports: [RouterLink, CurrencyPipe, DatePipe, HaceTiempoPipe, EstadoBadge, Paginacion],
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">Por aprobar</h1>
    <p class="mt-1 text-sm text-slate-500">Documentos que esperan tu decisión según los grupos de aprobación a los que perteneces.</p>

    <div class="mt-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div class="inline-flex self-start rounded-lg border border-slate-200 bg-white p-1" role="tablist">
        @for (t of pestanas; track t.valor) {
          <button
            type="button"
            role="tab"
            class="rounded-md px-4 py-1.5 text-sm font-medium"
            [class]="vista() === t.valor ? 'bg-primario-600 text-white' : 'text-slate-600 hover:bg-slate-100'"
            [attr.aria-selected]="vista() === t.valor"
            (click)="cambiarVista(t.valor)"
          >
            {{ t.etiqueta }}
            @if (t.valor === 'pendientes' && pendientes.value(); as p) {
              <span class="ml-1 rounded-full px-1.5 text-xs" [class]="vista() === 'pendientes' ? 'bg-white/20' : 'bg-pendiente-50 text-pendiente-800'">{{ p.total }}</span>
            }
          </button>
        }
      </div>
      <form class="flex gap-2" (submit)="$event.preventDefault(); aplicarBusqueda()">
        <label class="sr-only" for="buscar-aprobaciones">Buscar</label>
        <input
          id="buscar-aprobaciones"
          type="search"
          class="campo min-w-0 flex-1 lg:w-72 lg:flex-none"
          placeholder="Folio, emisor, RFC o UUID"
          [value]="texto()"
          (input)="texto.set($any($event.target).value)"
        />
        <button type="submit" class="btn-secundario">Buscar</button>
      </form>
    </div>

    <div class="tarjeta mt-4 overflow-hidden">
      @if (vista() === 'pendientes') {
        @if (pendientes.error()) {
          <p class="p-6 text-sm text-rechazado-700">No se pudo cargar la bandeja.</p>
        } @else if (paginaPendientes(); as p) {
          @if (p.elementos.length === 0) {
            <p class="p-10 text-center text-slate-600">{{ busqueda() ? 'Sin resultados.' : 'No tienes documentos pendientes. 🎉' }}</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-600">
                  <tr>
                    <th class="px-4 py-3 font-medium">Folio</th>
                    <th class="hidden px-4 py-3 font-medium sm:table-cell">Descripción</th>
                    <th class="hidden px-4 py-3 font-medium lg:table-cell">Emisor</th>
                    <th class="px-4 py-3 text-right font-medium lg:whitespace-nowrap">Monto (MXN)</th>
                    <th class="hidden px-4 py-3 font-medium sm:table-cell">Tu paso</th>
                    <th class="hidden px-4 py-3 font-medium xl:table-cell">Esperando</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  @for (d of p.elementos; track d.id) {
                    <tr class="hover:bg-slate-50">
                      <td class="px-4 py-3 align-top sm:align-middle">
                        <a [routerLink]="['/documentos', d.id]" [queryParams]="{ desde: 'aprobaciones' }" class="font-medium whitespace-nowrap text-primario-700 hover:underline">{{ d.folio }}</a>
                        <!-- En pantallas angostas, descripción y paso se integran a las dos columnas visibles. -->
                        <span class="mt-1 line-clamp-2 wrap-anywhere text-slate-700 sm:hidden" [title]="d.descripcion">{{ d.descripcion }}</span>
                        <span class="mt-0.5 block wrap-anywhere text-xs text-slate-500 sm:hidden">Cargó {{ d.creadoPor }}</span>
                      </td>
                      <!-- w-1/2 + max-w-0: los textos largos se recortan y reparten el espacio que dejan las demás columnas. -->
                      <td class="hidden w-1/2 max-w-0 px-4 py-3 sm:table-cell">
                        <span class="block truncate" [title]="d.descripcion">{{ d.descripcion }}</span>
                        <span class="block truncate text-xs text-slate-500" [title]="d.creadoPor">Cargó {{ d.creadoPor }}</span>
                      </td>
                      <td class="hidden w-1/2 max-w-0 truncate px-4 py-3 lg:table-cell" [title]="d.nombreEmisor ?? ''">
                        {{ d.nombreEmisor ?? '—' }}<span class="block text-xs text-slate-500">{{ d.rfcEmisor }}</span>
                      </td>
                      <td class="px-4 py-3 text-right align-top whitespace-nowrap tabular-nums sm:align-middle">
                        {{ d.montoMxn === null ? '—' : (d.montoMxn | currency: 'MXN' : 'symbol-narrow') }}
                        @if (d.moneda && d.moneda !== 'MXN') {
                          <span class="block text-xs text-slate-500">{{ d.total | currency: d.moneda : 'symbol-narrow' }} {{ d.moneda }}</span>
                        }
                        <span class="mt-1 block whitespace-normal sm:hidden">
                          {{ d.pasoActual }}<span class="block text-xs text-slate-500">Paso {{ d.ordenPaso }} de {{ d.totalPasos }}</span>
                        </span>
                      </td>
                      <td class="hidden px-4 py-3 whitespace-nowrap sm:table-cell">
                        {{ d.pasoActual }}<span class="block text-xs text-slate-500">Paso {{ d.ordenPaso }} de {{ d.totalPasos }}</span>
                      </td>
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 xl:table-cell" [title]="d.esperaDesde | date: 'dd/MM/yyyy HH:mm'">
                        {{ d.esperaDesde | haceTiempo }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-paginacion [pagina]="p" (cambiar)="numeroPagina.set($event)" />
          }
        } @else {
          <p class="p-6 text-sm text-slate-500">Cargando…</p>
        }
      } @else {
        @if (resueltos.error()) {
          <p class="p-6 text-sm text-rechazado-700">No se pudo cargar el historial.</p>
        } @else if (paginaResueltos(); as p) {
          @if (p.elementos.length === 0) {
            <p class="p-10 text-center text-slate-600">Aún no has resuelto documentos.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead class="bg-slate-50 text-left text-slate-600">
                  <tr>
                    <th class="px-4 py-3 font-medium">Folio</th>
                    <th class="hidden px-4 py-3 font-medium sm:table-cell">Descripción</th>
                    <th class="px-4 py-3 text-right font-medium sm:text-left">Tu decisión</th>
                    <th class="hidden px-4 py-3 font-medium whitespace-nowrap lg:table-cell">Estado actual</th>
                    <th class="hidden px-4 py-3 font-medium xl:table-cell">Fecha</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  @for (r of p.elementos; track $index) {
                    <tr class="hover:bg-slate-50">
                      <td class="px-4 py-3 align-top sm:align-middle">
                        <a [routerLink]="['/documentos', r.id]" [queryParams]="{ desde: 'aprobaciones' }" class="font-medium whitespace-nowrap text-primario-700 hover:underline">{{ r.folio }}</a>
                        <span class="mt-1 line-clamp-2 wrap-anywhere text-slate-700 sm:hidden" [title]="r.descripcion">{{ r.descripcion }}</span>
                        <span class="mt-0.5 line-clamp-2 wrap-anywhere text-xs text-slate-500 sm:hidden">{{ r.nombreEmisor }}</span>
                      </td>
                      <td class="hidden w-1/2 max-w-0 px-4 py-3 sm:table-cell">
                        <span class="block truncate" [title]="r.descripcion">{{ r.descripcion }}</span>
                        <span class="block truncate text-xs text-slate-500" [title]="r.nombreEmisor ?? ''">{{ r.nombreEmisor }}</span>
                      </td>
                      <td class="w-1/2 max-w-0 px-4 py-3 text-right align-top sm:text-left sm:align-middle">
                        <span class="font-medium" [class]="r.miDecision === 'Aprobado' ? 'text-autorizado-700' : 'text-rechazado-700'">
                          {{ r.miDecision === 'Aprobado' ? 'Aprobado' : 'Rechazado' }}
                        </span>
                        <span class="block truncate text-xs text-slate-500" [title]="r.paso">{{ r.paso }}</span>
                        @if (r.comentario) {
                          <span class="block truncate text-xs text-slate-500" [title]="r.comentario">“{{ r.comentario }}”</span>
                        }
                        <span class="mt-1 block lg:hidden"><app-estado-badge [estado]="r.estadoActual" /></span>
                      </td>
                      <td class="hidden px-4 py-3 whitespace-nowrap lg:table-cell"><app-estado-badge [estado]="r.estadoActual" /></td>
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 xl:table-cell">{{ r.fechaResolucion | date: 'dd/MM/yyyy HH:mm' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-paginacion [pagina]="p" nombre="decisiones" (cambiar)="numeroPagina.set($event)" />
          }
        } @else {
          <p class="p-6 text-sm text-slate-500">Cargando…</p>
        }
      }
    </div>
  `,
})
export class PorAprobar {
  protected readonly pestanas: { valor: Vista; etiqueta: string }[] = [
    { valor: 'pendientes', etiqueta: 'Pendientes' },
    { valor: 'resueltos', etiqueta: 'Resueltos por mí' },
  ];
  protected readonly vista = signal<Vista>('pendientes');
  protected readonly texto = signal('');
  protected readonly busqueda = signal('');
  protected readonly numeroPagina = signal(1);

  private readonly params = computed(() => ({
    pagina: this.numeroPagina(),
    ...(this.busqueda() ? { buscar: this.busqueda() } : {}),
  }));

  // El contador de la pestaña se mantiene aunque se vea la otra vista.
  protected readonly pendientes = httpResource<Pagina<PendienteAprobacion>>(() => ({
    url: '/api/aprobaciones/pendientes',
    params: this.vista() === 'pendientes' ? this.params() : { pagina: 1, tamano: 1 },
  }));
  protected readonly resueltos = httpResource<Pagina<ResueltoPorMi>>(() =>
    this.vista() === 'resueltos' ? { url: '/api/aprobaciones/resueltos', params: this.params() } : undefined,
  );

  protected readonly paginaPendientes = linkedSignal<Pagina<PendienteAprobacion> | undefined, Pagina<PendienteAprobacion> | undefined>({
    source: () => (this.vista() === 'pendientes' ? this.pendientes.value() : undefined),
    computation: (nueva, anterior) => nueva ?? anterior?.value,
  });
  protected readonly paginaResueltos = linkedSignal<Pagina<ResueltoPorMi> | undefined, Pagina<ResueltoPorMi> | undefined>({
    source: () => this.resueltos.value(),
    computation: (nueva, anterior) => nueva ?? anterior?.value,
  });

  protected cambiarVista(vista: Vista): void {
    this.vista.set(vista);
    this.numeroPagina.set(1);
  }

  protected aplicarBusqueda(): void {
    this.busqueda.set(this.texto().trim());
    this.numeroPagina.set(1);
  }
}
