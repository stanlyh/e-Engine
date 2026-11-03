import { Component, computed, input, output } from '@angular/core';
import { Pagina } from '../../core/api/modelos';

@Component({
  selector: 'app-paginacion',
  template: `
    @if (totalPaginas() > 1) {
      <nav class="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 px-4 py-3 text-sm" aria-label="Paginación">
        <span class="text-slate-500">{{ pagina().total }} {{ nombre() }} · página {{ pagina().numeroPagina }} de {{ totalPaginas() }}</span>
        <div class="flex gap-2">
          <button type="button" class="btn-secundario" [disabled]="pagina().numeroPagina <= 1" (click)="cambiar.emit(pagina().numeroPagina - 1)">
            Anterior
          </button>
          <button
            type="button"
            class="btn-secundario"
            [disabled]="pagina().numeroPagina >= totalPaginas()"
            (click)="cambiar.emit(pagina().numeroPagina + 1)"
          >
            Siguiente
          </button>
        </div>
      </nav>
    }
  `,
})
export class Paginacion {
  readonly pagina = input.required<Pagina<unknown>>();
  readonly nombre = input('documentos');
  readonly cambiar = output<number>();

  protected readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.pagina().total / this.pagina().tamanoPagina)));
}
