import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { Sesion } from '../../core/auth/sesion';

interface ReglaAprobacion {
  id: number;
  nombre: string;
  montoDesde: number;
  montoHasta: number | null;
  activa: boolean;
  pasos: { orden: number; nombre: string; grupoAd: string }[];
}

interface Indicador {
  etiqueta: string;
  clase: string;
}

/** Los valores llegan en la Fase 7 (GET /api/dashboard/indicadores). */
const INDICADORES: Indicador[] = [
  { etiqueta: 'Enviados', clase: 'border-l-enviado-500' },
  { etiqueta: 'Pendientes', clase: 'border-l-pendiente-500' },
  { etiqueta: 'Autorizados', clase: 'border-l-autorizado-500' },
  { etiqueta: 'Rechazados', clase: 'border-l-rechazado-500' },
  { etiqueta: 'En Tesorería', clase: 'border-l-tesoreria-500' },
  { etiqueta: 'Procesados', clase: 'border-l-procesado-500' },
];

@Component({
  selector: 'app-home',
  imports: [CurrencyPipe],
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">Hola, {{ sesion.nombre() }}</h1>
    <p class="mt-1 text-sm text-slate-500">Resumen de documentos</p>

    <section class="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-6" aria-label="Indicadores">
      @for (i of indicadores; track i.etiqueta) {
        <div class="rounded-lg border border-l-4 border-slate-200 bg-white p-4 shadow-xs" [class]="i.clase">
          <p class="text-sm text-slate-500">{{ i.etiqueta }}</p>
          <p class="mt-2 text-3xl font-semibold tabular-nums text-slate-300">—</p>
        </div>
      }
    </section>

    <section class="mt-10">
      <h2 class="text-lg font-semibold">Cadena de aprobación por monto</h2>
      <p class="text-sm text-slate-500">El monto se toma del CFDI (Total × Tipo de cambio).</p>

      @if (reglas.isLoading()) {
        <p class="mt-4 text-sm text-slate-500">Cargando…</p>
      } @else if (reglas.error()) {
        <p class="mt-4 text-sm text-rechazado-700">No se pudieron cargar las reglas.</p>
      } @else {
        <div class="mt-4 overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-left text-slate-600">
              <tr>
                <th class="px-4 py-3 font-medium">Rango (MXN)</th>
                <th class="px-4 py-3 font-medium">Aprobadores, en orden</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (r of reglas.value(); track r.id) {
                <tr [class.opacity-50]="!r.activa">
                  <td class="px-4 py-3 whitespace-nowrap tabular-nums">
                    {{ r.montoDesde | currency: 'MXN' : 'symbol-narrow' }} –
                    {{ r.montoHasta === null ? 'en adelante' : (r.montoHasta | currency: 'MXN' : 'symbol-narrow') }}
                  </td>
                  <td class="px-4 py-3">
                    <ol class="flex flex-wrap items-center gap-2">
                      @for (p of r.pasos; track p.orden; let ultimo = $last) {
                        <li class="rounded-full bg-pendiente-50 px-3 py-1 text-pendiente-800">{{ p.orden }}. {{ p.nombre }}</li>
                        @if (!ultimo) {
                          <li aria-hidden="true" class="text-slate-400">→</li>
                        }
                      }
                    </ol>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
})
export class Home {
  protected readonly sesion = inject(Sesion);
  protected readonly indicadores = INDICADORES;
  protected readonly reglas = httpResource<ReglaAprobacion[]>(() => '/api/reglas-aprobacion', { defaultValue: [] });
}
