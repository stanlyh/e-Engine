import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, input, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ESTADOS, EstadoDocumento, Pagina, DocumentoResumen } from '../../core/api/modelos';
import { EstadoBadge } from '../../shared/ui/estado-badge';

const TAMANO_PAGINA = 20;

@Component({
  selector: 'app-mis-documentos',
  imports: [RouterLink, DatePipe, CurrencyPipe, EstadoBadge],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-semibold tracking-tight">Mis documentos</h1>
      <a routerLink="/documentos/nuevo" class="btn-primario">+ Nuevo documento</a>
    </div>

    <form class="mt-6 flex flex-col gap-3 sm:flex-row" (submit)="$event.preventDefault(); buscar()">
      <label class="sr-only" for="filtro-estado">Estado</label>
      <select id="filtro-estado" class="campo sm:w-48" [value]="estadoFiltro() ?? ''" (change)="cambiarEstado($event)">
        <option value="">Todos los estados</option>
        @for (e of estados; track e.valor) {
          <option [value]="e.valor">{{ e.etiqueta }}</option>
        }
      </select>
      <label class="sr-only" for="filtro-texto">Buscar</label>
      <input
        id="filtro-texto"
        class="campo flex-1"
        type="search"
        placeholder="Folio, descripción, emisor, RFC o UUID"
        [value]="textoBusqueda()"
        (input)="textoBusqueda.set($any($event.target).value)"
      />
      <button type="submit" class="btn-secundario">Buscar</button>
    </form>

    <div class="tarjeta mt-4 overflow-hidden">
      @if (documentos.error()) {
        <p class="p-6 text-sm text-rechazado-700">No se pudieron cargar los documentos.</p>
      } @else if (documentos.isLoading() && !pagina()) {
        <p class="p-6 text-sm text-slate-500">Cargando…</p>
      } @else if (pagina()?.elementos?.length === 0) {
        <div class="p-10 text-center">
          <p class="text-slate-600">No hay documentos{{ hayFiltros() ? ' con esos filtros' : '' }}.</p>
          @if (!hayFiltros()) {
            <a routerLink="/documentos/nuevo" class="mt-3 inline-block text-primario-700 hover:underline">Carga tu primera factura</a>
          }
        </div>
      } @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-left text-slate-600">
              <tr>
                <th class="px-4 py-3 font-medium">Folio</th>
                <th class="px-4 py-3 font-medium">Descripción</th>
                <th class="hidden px-4 py-3 font-medium md:table-cell">Emisor</th>
                <th class="px-4 py-3 text-right font-medium">Total</th>
                <th class="px-4 py-3 font-medium">Estado</th>
                <th class="hidden px-4 py-3 font-medium lg:table-cell">Creado</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (d of pagina()?.elementos; track d.id) {
                <tr class="hover:bg-slate-50">
                  <td class="px-4 py-3 whitespace-nowrap">
                    <a [routerLink]="['/documentos', d.id]" class="font-medium text-primario-700 hover:underline">{{ d.folio }}</a>
                  </td>
                  <td class="max-w-xs truncate px-4 py-3" [title]="d.descripcion">{{ d.descripcion }}</td>
                  <td class="hidden max-w-xs truncate px-4 py-3 md:table-cell">
                    {{ d.nombreEmisor ?? '—' }}
                    @if (d.rfcEmisor) {
                      <span class="block text-xs text-slate-500">{{ d.rfcEmisor }}</span>
                    }
                  </td>
                  <td class="px-4 py-3 text-right whitespace-nowrap tabular-nums">
                    {{ d.total === null ? '—' : (d.total | currency: d.moneda ?? 'MXN' : 'symbol-narrow') }}
                  </td>
                  <td class="px-4 py-3">
                    <app-estado-badge [estado]="d.estado" />
                    @if (d.pasoActual) {
                      <span class="mt-1 block text-xs text-slate-500">{{ d.pasoActual }}</span>
                    }
                  </td>
                  <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 lg:table-cell">
                    {{ d.fechaCreacion | date: 'dd/MM/yyyy' }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (totalPaginas() > 1) {
          <nav class="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm" aria-label="Paginación">
            <span class="text-slate-500">{{ pagina()?.total }} documentos · página {{ numeroPagina() }} de {{ totalPaginas() }}</span>
            <div class="flex gap-2">
              <button type="button" class="btn-secundario" [disabled]="numeroPagina() <= 1" (click)="numeroPagina.set(numeroPagina() - 1)">
                Anterior
              </button>
              <button
                type="button"
                class="btn-secundario"
                [disabled]="numeroPagina() >= totalPaginas()"
                (click)="numeroPagina.set(numeroPagina() + 1)"
              >
                Siguiente
              </button>
            </div>
          </nav>
        }
      }
    </div>
  `,
})
export class MisDocumentos {
  /** Filtro inicial desde la URL (?estado=), p. ej. al venir de un indicador del inicio. */
  readonly estado = input<EstadoDocumento | undefined>();

  protected readonly estados = ESTADOS;
  protected readonly estadoFiltro = linkedSignal<EstadoDocumento | null>(() => this.estado() ?? null);
  protected readonly textoBusqueda = signal('');
  private readonly busquedaAplicada = signal('');
  protected readonly numeroPagina = signal(1);

  protected readonly documentos = httpResource<Pagina<DocumentoResumen>>(() => ({
    url: '/api/documentos',
    params: {
      pagina: this.numeroPagina(),
      tamano: TAMANO_PAGINA,
      ...(this.estadoFiltro() ? { estado: this.estadoFiltro()! } : {}),
      ...(this.busquedaAplicada() ? { buscar: this.busquedaAplicada() } : {}),
    },
  }));

  /** Conserva la página anterior mientras carga la siguiente. */
  protected readonly pagina = linkedSignal<Pagina<DocumentoResumen> | undefined, Pagina<DocumentoResumen> | undefined>({
    source: () => this.documentos.value(),
    computation: (nueva, anterior) => nueva ?? anterior?.value,
  });
  protected readonly totalPaginas = computed(() => {
    const p = this.pagina();
    return p ? Math.max(1, Math.ceil(p.total / p.tamanoPagina)) : 1;
  });
  protected readonly hayFiltros = computed(() => !!this.estadoFiltro() || !!this.busquedaAplicada());

  protected cambiarEstado(evento: Event): void {
    this.estadoFiltro.set(((evento.target as HTMLSelectElement).value || null) as EstadoDocumento | null);
    this.numeroPagina.set(1);
  }

  protected buscar(): void {
    this.busquedaAplicada.set(this.textoBusqueda().trim());
    this.numeroPagina.set(1);
  }
}
