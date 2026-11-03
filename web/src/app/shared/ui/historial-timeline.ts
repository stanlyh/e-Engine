import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { AccionHistorial, ETIQUETA_ACCION, Historial } from '../../core/api/modelos';

const COLOR: Partial<Record<AccionHistorial, string>> = {
  Enviado: 'bg-enviado-500',
  Reenviado: 'bg-enviado-500',
  PasoAprobado: 'bg-autorizado-500',
  Autorizado: 'bg-autorizado-500',
  Rechazado: 'bg-rechazado-500',
  Procesado: 'bg-procesado-500',
};

/** Historial del documento, del evento más reciente al más antiguo. */
@Component({
  selector: 'app-historial-timeline',
  imports: [DatePipe],
  template: `
    <ol class="relative border-l border-slate-200 pl-6">
      @for (h of eventos(); track $index) {
        <li class="mb-5 last:mb-0">
          <span
            class="absolute -left-1.5 mt-1.5 size-3 rounded-full ring-4 ring-white"
            [class]="color[h.accion] ?? 'bg-slate-300'"
          ></span>
          <p class="text-sm font-medium text-slate-900">{{ etiqueta[h.accion] }}</p>
          <p class="text-xs text-slate-500">{{ h.usuario.nombre }} · {{ h.fecha | date: 'dd/MM/yyyy HH:mm' }}</p>
          @if (h.comentario) {
            <p
              class="mt-1 text-sm break-words"
              [class]="h.accion === 'Rechazado' ? 'text-rechazado-700' : 'text-slate-600'"
            >
              {{ h.comentario }}
            </p>
          }
        </li>
      }
    </ol>
  `,
})
export class HistorialTimeline {
  readonly historial = input.required<Historial[]>();
  protected readonly etiqueta = ETIQUETA_ACCION;
  protected readonly color = COLOR;
  protected readonly eventos = computed(() => [...this.historial()].reverse());
}
