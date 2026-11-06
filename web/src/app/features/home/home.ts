import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Sesion } from '../../core/auth/sesion';
import { AccionHistorial, Dashboard, ETIQUETA_ACCION, Indicador, ReglaAprobacion } from '../../core/api/modelos';
import { HaceTiempoPipe } from '../../shared/hace-tiempo.pipe';
import { MontoCompactoPipe } from '../../shared/monto-compacto.pipe';

type Periodo = 'todo' | 'anio' | 'mes' | '30d';
type ClaveIndicador = keyof Dashboard['indicadores'];

const PERIODOS: { valor: Periodo; etiqueta: string }[] = [
  { valor: 'todo', etiqueta: 'Todo el tiempo' },
  { valor: 'anio', etiqueta: 'Este año' },
  { valor: 'mes', etiqueta: 'Este mes' },
  { valor: '30d', etiqueta: 'Últimos 30 días' },
];

/** Cada tarjeta abre la lista con el mismo filtro con que se contó. */
const TARJETAS: { clave: ClaveIndicador; etiqueta: string; ayuda: string; filtro: string; marca: string }[] = [
  { clave: 'enviados', etiqueta: 'Enviados', ayuda: 'Todo lo que se ha enviado a aprobación', filtro: 'enviados', marca: 'bg-enviado-500' },
  { clave: 'pendientes', etiqueta: 'Pendientes', ayuda: 'En la cadena de aprobación', filtro: 'EnAprobacion', marca: 'bg-pendiente-500' },
  { clave: 'autorizados', etiqueta: 'Autorizados', ayuda: 'Aprobados por toda la cadena', filtro: 'autorizados', marca: 'bg-autorizado-500' },
  { clave: 'rechazados', etiqueta: 'Rechazados', ayuda: 'Esperan corrección', filtro: 'Rechazado', marca: 'bg-rechazado-500' },
  { clave: 'enTesoreria', etiqueta: 'En Tesorería', ayuda: 'Llegaron a Tesorería y esperan pago', filtro: 'EnTesoreria', marca: 'bg-tesoreria-500' },
  { clave: 'procesados', etiqueta: 'Procesados', ayuda: 'Cerrados por Tesorería', filtro: 'Procesado', marca: 'bg-procesado-500' },
];

const PUNTO_ACCION: Partial<Record<AccionHistorial, string>> = {
  Enviado: 'bg-enviado-500',
  Reenviado: 'bg-enviado-500',
  PasoAprobado: 'bg-autorizado-500',
  Autorizado: 'bg-autorizado-500',
  Rechazado: 'bg-rechazado-500',
  Procesado: 'bg-procesado-500',
};

@Component({
  selector: 'app-home',
  imports: [RouterLink, CurrencyPipe, DatePipe, HaceTiempoPipe, MontoCompactoPipe],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight">Hola, {{ sesion.nombre() }}</h1>
        <p class="mt-1 text-sm text-slate-500">
          {{ verTodos() ? 'Todos los documentos' : 'Tus documentos' }} · enviados {{ etiquetaPeriodo().toLowerCase() }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        @if (esAdmin()) {
          <div class="inline-flex rounded-lg border border-slate-200 bg-white p-1" role="group" aria-label="Alcance">
            @for (op of [false, true]; track op) {
              <button
                type="button"
                class="rounded-md px-3 py-1.5 text-sm font-medium"
                [class]="verTodos() === op ? 'bg-primario-600 text-white' : 'text-slate-600 hover:bg-slate-100'"
                [attr.aria-pressed]="verTodos() === op"
                (click)="actualizarUrl({ todos: op ? 'true' : null })"
              >
                {{ op ? 'Todos' : 'Míos' }}
              </button>
            }
          </div>
        }
        <label class="sr-only" for="periodo">Periodo</label>
        <select id="periodo" class="campo w-auto" (change)="cambiarPeriodo($any($event.target).value)">
          @for (p of periodos; track p.valor) {
            <option [value]="p.valor" [selected]="p.valor === periodoActivo()">{{ p.etiqueta }}</option>
          }
        </select>
      </div>
    </div>

    @if (dashboard.error()) {
      <p class="mt-6 rounded-md bg-rechazado-50 px-4 py-3 text-sm text-rechazado-700" role="alert">No se pudieron cargar los indicadores.</p>
    }

    @if (datos(); as d) {
      @if (d.esperanMiAprobacion !== null || d.porProcesar) {
        <section class="mt-6 grid gap-4 sm:grid-cols-2" aria-label="Tu trabajo pendiente">
          @if (d.esperanMiAprobacion !== null) {
            <a routerLink="/aprobaciones" class="tarjeta group flex items-center justify-between gap-4 p-5 hover:border-pendiente-500">
              <div>
                <p class="text-sm font-medium text-slate-600">Esperan tu aprobación</p>
                <p class="mt-1 text-4xl font-semibold tabular-nums text-slate-900">{{ d.esperanMiAprobacion }}</p>
              </div>
              <span class="text-sm font-medium text-primario-700 group-hover:underline">Ir a Por aprobar →</span>
            </a>
          }
          @if (d.porProcesar; as p) {
            <a routerLink="/tesoreria" class="tarjeta group flex items-center justify-between gap-4 p-5 hover:border-tesoreria-500">
              <div>
                <p class="text-sm font-medium text-slate-600">Por procesar en Tesorería</p>
                <p class="mt-1 text-4xl font-semibold tabular-nums text-slate-900">{{ p.cantidad }}</p>
                <p class="text-sm text-slate-500" [title]="p.monto | currency: 'MXN' : 'symbol-narrow'">{{ p.monto | montoCompacto }} por pagar</p>
              </div>
              <span class="text-sm font-medium text-primario-700 group-hover:underline">Ir a Tesorería →</span>
            </a>
          }
        </section>
      }
    }

    <section class="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-6" aria-label="Indicadores">
      @for (t of tarjetas; track t.clave) {
        @let ind = indicador(t.clave);
        <a
          [routerLink]="'/documentos'"
          [queryParams]="parametrosLista(t.filtro)"
          class="tarjeta group relative overflow-hidden p-4 pl-5 transition-shadow hover:shadow-md focus-visible:outline-2 focus-visible:outline-primario-500"
          [attr.aria-label]="t.etiqueta + ': ' + (ind?.cantidad ?? 'cargando') + '. ' + t.ayuda"
          [title]="t.ayuda"
        >
          <span aria-hidden="true" class="absolute inset-y-0 left-0 w-1" [class]="t.marca"></span>
          <p class="text-sm font-medium text-slate-600">{{ t.etiqueta }}</p>
          <p class="mt-2 text-3xl font-semibold tabular-nums text-slate-900">{{ ind?.cantidad ?? '—' }}</p>
          <p class="mt-1 text-xs text-slate-500 tabular-nums" [title]="ind ? (ind.monto | currency: 'MXN' : 'symbol-narrow') : ''">
            {{ ind ? (ind.monto | montoCompacto) : ' ' }}
          </p>
        </a>
      }
    </section>

    <div class="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-5">
      <section class="tarjeta p-5 lg:col-span-3" aria-labelledby="titulo-movimientos">
        <h2 id="titulo-movimientos" class="text-sm font-semibold text-slate-800">Últimos movimientos</h2>
        @if (datos(); as d) {
          @if (d.movimientos.length === 0) {
            <p class="mt-4 text-sm text-slate-500">
              Aún no hay movimientos.
              <a routerLink="/documentos/nuevo" class="text-primario-700 hover:underline">Carga tu primera factura</a>
            </p>
          } @else {
            <ul class="mt-3 divide-y divide-slate-100">
              @for (m of d.movimientos; track $index) {
                <li class="flex gap-3 py-3">
                  <span aria-hidden="true" class="mt-1.5 size-2.5 shrink-0 rounded-full" [class]="puntoAccion[m.accion] ?? 'bg-slate-300'"></span>
                  <div class="min-w-0 flex-1">
                    <p class="text-sm">
                      <a [routerLink]="['/documentos', m.documentoId]" class="font-medium text-primario-700 hover:underline">{{ m.folio }}</a>
                      <span class="text-slate-700"> · {{ etiquetaAccion[m.accion] }}</span>
                    </p>
                    <p class="truncate text-xs text-slate-500" [title]="m.descripcion">{{ m.descripcion }}</p>
                    @if (m.comentario && (m.accion === 'Rechazado' || m.accion === 'PasoAprobado' || m.accion === 'Autorizado' || m.accion === 'Procesado')) {
                      <p class="mt-0.5 truncate text-xs text-slate-600" [title]="m.comentario">{{ m.comentario }}</p>
                    }
                  </div>
                  <div class="shrink-0 text-right text-xs text-slate-500">
                    <p [title]="m.fecha | date: 'dd/MM/yyyy HH:mm'">{{ m.fecha | haceTiempo }}</p>
                    <p class="max-w-32 truncate">{{ m.usuario }}</p>
                  </div>
                </li>
              }
            </ul>
          }
        } @else {
          <p class="mt-4 text-sm text-slate-500">Cargando…</p>
        }
      </section>

      <section class="tarjeta p-5 lg:col-span-2" aria-labelledby="titulo-reglas">
        <h2 id="titulo-reglas" class="text-sm font-semibold text-slate-800">Quién aprueba según el monto</h2>
        <p class="text-xs text-slate-500">Monto del CFDI en pesos (Total × tipo de cambio).</p>
        @if (reglas.error()) {
          <p class="mt-4 text-sm text-rechazado-700">No se pudieron cargar las reglas.</p>
        } @else {
          <ul class="mt-3 divide-y divide-slate-100">
            @for (r of reglas.value(); track r.id) {
              <li class="py-3" [class.opacity-50]="!r.activa">
                <p class="text-sm font-medium tabular-nums">
                  {{ r.montoDesde | currency: 'MXN' : 'symbol-narrow' : '1.0-2' }}
                  {{ r.montoHasta === null ? 'en adelante' : '– ' + (r.montoHasta | currency: 'MXN' : 'symbol-narrow' : '1.0-2') }}
                </p>
                <p class="mt-0.5 text-sm text-slate-600">{{ nombresPasos(r) }}</p>
              </li>
            }
          </ul>
        }
      </section>
    </div>
  `,
})
export class Home {
  /** Periodo y alcance viven en la URL para conservarse al regresar de una lista o recargar. */
  readonly periodo = input<string>();
  readonly todos = input<string>();

  protected readonly sesion = inject(Sesion);
  private readonly router = inject(Router);
  protected readonly periodos = PERIODOS;
  protected readonly tarjetas = TARJETAS;
  protected readonly etiquetaAccion = ETIQUETA_ACCION;
  protected readonly puntoAccion = PUNTO_ACCION;

  protected readonly esAdmin = computed(() => this.sesion.tieneRol('Admin'));
  /** Un valor manual en la URL no debe romper la pantalla: sin Admin se ignora `todos`, y un periodo desconocido es "todo". */
  protected readonly verTodos = computed(() => this.esAdmin() && this.todos() === 'true');
  protected readonly periodoActivo = computed<Periodo>(() => PERIODOS.find((p) => p.valor === this.periodo())?.valor ?? 'todo');
  protected readonly etiquetaPeriodo = computed(() => PERIODOS.find((p) => p.valor === this.periodoActivo())!.etiqueta);

  /** Inicio del periodo en hora local, enviado como ISO (UTC). */
  private readonly desde = computed<string | null>(() => {
    const hoy = new Date();
    switch (this.periodoActivo()) {
      case 'anio':
        return new Date(hoy.getFullYear(), 0, 1).toISOString();
      case 'mes':
        return new Date(hoy.getFullYear(), hoy.getMonth(), 1).toISOString();
      case '30d':
        return new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - 30).toISOString();
      default:
        return null;
    }
  });

  protected readonly dashboard = httpResource<Dashboard>(() => ({
    url: '/api/dashboard/indicadores',
    params: { ...(this.verTodos() ? { todos: true } : {}), ...(this.desde() ? { desde: this.desde()! } : {}) },
  }));
  /** Conserva los números anteriores mientras se recalculan, para que las tarjetas no parpadeen. */
  private ultimo: Dashboard | undefined;
  protected readonly datos = computed(() => (this.ultimo = this.dashboard.value() ?? this.ultimo));

  protected readonly reglas = httpResource<ReglaAprobacion[]>(() => '/api/reglas-aprobacion', { defaultValue: [] });

  protected indicador(clave: ClaveIndicador): Indicador | undefined {
    return this.datos()?.indicadores[clave];
  }

  protected parametrosLista(filtro: string): Record<string, string> {
    return {
      estado: filtro,
      ...(this.verTodos() ? { todos: 'true' } : {}),
      ...(this.desde() ? { desde: this.desde()! } : {}),
    };
  }

  protected cambiarPeriodo(valor: Periodo): void {
    this.actualizarUrl({ periodo: valor === 'todo' ? null : valor });
  }

  /** replaceUrl: cambiar el filtro no agrega pasos al botón Atrás. */
  protected actualizarUrl(parametros: { periodo?: string | null; todos?: string | null }): void {
    void this.router.navigate([], { queryParams: parametros, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected nombresPasos(regla: ReglaAprobacion): string {
    return [...regla.pasos].sort((a, b) => a.orden - b.orden).map((p) => p.nombre).join(' → ');
  }
}
