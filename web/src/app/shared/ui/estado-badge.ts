import { Component, computed, input } from '@angular/core';
import { ETIQUETA_ESTADO, EstadoDocumento } from '../../core/api/modelos';

const CLASES: Record<EstadoDocumento, string> = {
  Borrador: 'bg-slate-100 text-slate-700 ring-slate-200',
  EnAprobacion: 'bg-pendiente-50 text-pendiente-800 ring-pendiente-200',
  Rechazado: 'bg-rechazado-50 text-rechazado-700 ring-rechazado-200',
  EnTesoreria: 'bg-tesoreria-50 text-tesoreria-700 ring-tesoreria-200',
  Procesado: 'bg-procesado-50 text-procesado-700 ring-procesado-500/30',
};

@Component({
  selector: 'app-estado-badge',
  template: `
    <span
      class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset"
      [class]="clases()"
    >
      {{ etiqueta() }}
    </span>
  `,
})
export class EstadoBadge {
  readonly estado = input.required<EstadoDocumento>();
  protected readonly clases = computed(() => CLASES[this.estado()]);
  protected readonly etiqueta = computed(() => ETIQUETA_ESTADO[this.estado()]);
}
