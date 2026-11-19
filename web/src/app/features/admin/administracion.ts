import { Component, computed, inject, input, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Correos } from './correos';
import { ReglasEditor } from './reglas-editor';

type Pestana = 'reglas' | 'correos';

const PESTANAS: { valor: Pestana; etiqueta: string }[] = [
  { valor: 'reglas', etiqueta: 'Reglas de aprobación' },
  { valor: 'correos', etiqueta: 'Correos' },
];

/** La pestaña vive en la URL (?pestana=correos) para poder enlazarla y conservarla al recargar. */
@Component({
  selector: 'app-administracion',
  imports: [ReglasEditor, Correos],
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">Administración</h1>

    <div class="mt-6 inline-flex rounded-lg border border-slate-200 bg-white p-1" role="tablist">
      @for (p of pestanas; track p.valor) {
        <button
          type="button"
          role="tab"
          class="rounded-md px-4 py-1.5 text-sm font-medium"
          [class]="activa() === p.valor ? 'bg-primario-600 text-white' : 'text-slate-600 hover:bg-slate-100'"
          [attr.aria-selected]="activa() === p.valor"
          (click)="cambiar(p.valor)"
        >
          {{ p.etiqueta }}
        </button>
      }
    </div>

    <div class="mt-6">
      @if (activa() === 'reglas') {
        <app-reglas-editor />
      } @else {
        <app-correos />
      }
    </div>
  `,
})
export class Administracion {
  readonly pestana = input<string>();

  private readonly router = inject(Router);
  private readonly editor = viewChild(ReglasEditor);

  protected readonly pestanas = PESTANAS;
  protected readonly activa = computed<Pestana>(() => (this.pestana() === 'correos' ? 'correos' : 'reglas'));

  /** Guard de salida: cambiar de pestaña o de pantalla descartaría las reglas sin guardar. */
  puedeSalir(): boolean {
    return !this.editor()?.tieneCambiosSinGuardar() || confirm('Tienes cambios sin guardar en las reglas. ¿Salir y descartarlos?');
  }

  protected cambiar(pestana: Pestana): void {
    void this.router.navigate([], { queryParams: { pestana: pestana === 'reglas' ? null : pestana }, queryParamsHandling: 'merge' });
  }
}
