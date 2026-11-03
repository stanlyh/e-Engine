import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { DocumentoDetalle, EstadoPaso } from '../../core/api/modelos';

type EstadoEtapa = 'hecho' | 'actual' | 'pendiente' | 'rechazado' | 'cancelado';

interface Etapa {
  nombre: string;
  estado: EstadoEtapa;
  detalle: string | null;
  fecha: string | null;
}

const DESDE_PASO: Record<EstadoPaso, EstadoEtapa> = {
  Aprobado: 'hecho',
  Pendiente: 'actual',
  EnEspera: 'pendiente',
  Rechazado: 'rechazado',
  Cancelado: 'cancelado',
};

const ESTILO: Record<EstadoEtapa, { punto: string; texto: string; icono: string; leyenda: string }> = {
  hecho: { punto: 'bg-autorizado-500 text-white', texto: 'text-slate-900', icono: '✓', leyenda: 'Completado' },
  actual: { punto: 'bg-pendiente-500 text-white ring-4 ring-pendiente-200', texto: 'text-slate-900 font-semibold', icono: '•', leyenda: 'Aquí está' },
  pendiente: { punto: 'bg-slate-200 text-slate-500', texto: 'text-slate-500', icono: '', leyenda: 'Pendiente' },
  rechazado: { punto: 'bg-rechazado-500 text-white', texto: 'text-rechazado-700 font-semibold', icono: '✕', leyenda: 'Rechazado' },
  cancelado: { punto: 'bg-slate-100 text-slate-400', texto: 'text-slate-400 line-through', icono: '–', leyenda: 'Cancelado' },
};

/** Muestra dónde está el documento: captura → cadena de aprobadores → Tesorería → procesado. */
@Component({
  selector: 'app-seguimiento',
  imports: [DatePipe],
  template: `
    <ol class="flex flex-col gap-4 md:flex-row md:gap-0">
      @for (etapa of etapas(); track $index; let ultimo = $last) {
        <li class="relative flex gap-3 md:flex-1 md:flex-col md:items-center md:text-center">
          @if (!ultimo) {
            <span
              aria-hidden="true"
              class="absolute top-8 left-4 h-[calc(100%-1rem)] w-0.5 md:top-4 md:left-[calc(50%+1.25rem)] md:h-0.5 md:w-[calc(100%-2.5rem)]"
              [class]="etapa.estado === 'hecho' ? 'bg-autorizado-500' : 'bg-slate-200'"
            ></span>
          }
          <span
            class="relative z-10 flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-bold"
            [class]="estilo[etapa.estado].punto"
            >{{ estilo[etapa.estado].icono }}</span
          >
          <div class="min-w-0 md:mt-2 md:px-2">
            <p class="text-sm" [class]="estilo[etapa.estado].texto">{{ etapa.nombre }}</p>
            <p class="text-xs text-slate-500">
              <span class="sr-only">{{ estilo[etapa.estado].leyenda }}. </span>
              @if (etapa.detalle) {
                {{ etapa.detalle }}
              }
              @if (etapa.fecha) {
                · {{ etapa.fecha | date: 'dd/MM/yy HH:mm' }}
              }
            </p>
          </div>
        </li>
      }
    </ol>
  `,
})
export class Seguimiento {
  readonly documento = input.required<DocumentoDetalle>();
  protected readonly estilo = ESTILO;

  protected readonly etapas = computed<Etapa[]>(() => {
    const d = this.documento();
    const enCaptura = d.estado === 'Borrador' || d.estado === 'Rechazado';
    const etapas: Etapa[] = [
      {
        nombre: d.estado === 'Rechazado' ? 'Corrección' : 'Captura',
        estado: enCaptura ? 'actual' : 'hecho',
        detalle: d.creadoPor.nombre,
        fecha: d.estado === 'Rechazado' ? d.fechaRechazo : d.fechaEnvio,
      },
    ];

    const ronda = d.aprobaciones.filter((a) => a.ronda === d.ronda).sort((a, b) => a.orden - b.orden);
    if (ronda.length === 0) {
      etapas.push({ nombre: 'Aprobación', estado: 'pendiente', detalle: 'Según el monto', fecha: null });
    }
    for (const paso of ronda) {
      // Tras un rechazo, la ronda anterior se muestra como referencia; la nueva empezará desde el paso 1.
      etapas.push({
        nombre: paso.nombre,
        estado: DESDE_PASO[paso.estado],
        detalle: paso.resueltoPor?.nombre ?? null,
        fecha: paso.fechaResolucion,
      });
    }

    etapas.push({
      nombre: 'Tesorería',
      estado: d.estado === 'EnTesoreria' ? 'actual' : d.estado === 'Procesado' ? 'hecho' : 'pendiente',
      detalle: null,
      fecha: d.fechaAutorizacion,
    });
    etapas.push({
      nombre: 'Procesado',
      estado: d.estado === 'Procesado' ? 'hecho' : 'pendiente',
      detalle: d.referenciaTesoreria ? `Ref. ${d.referenciaTesoreria}` : null,
      fecha: d.fechaProcesado,
    });
    return etapas;
  });
}
