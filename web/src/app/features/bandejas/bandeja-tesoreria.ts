import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DocumentoTesoreria, Pagina } from '../../core/api/modelos';
import { HaceTiempoPipe } from '../../shared/hace-tiempo.pipe';
import { Paginacion } from '../../shared/ui/paginacion';

@Component({
  selector: 'app-bandeja-tesoreria',
  imports: [RouterLink, CurrencyPipe, DatePipe, HaceTiempoPipe, Paginacion],
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">Tesorería</h1>
    <p class="mt-1 text-sm text-slate-500">Documentos autorizados por toda su cadena de aprobación.</p>

    <div class="mt-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div class="inline-flex self-start rounded-lg border border-slate-200 bg-white p-1" role="tablist">
        @for (t of pestanas; track t.procesados) {
          <button
            type="button"
            role="tab"
            class="rounded-md px-4 py-1.5 text-sm font-medium"
            [class]="procesados() === t.procesados ? 'bg-primario-600 text-white' : 'text-slate-600 hover:bg-slate-100'"
            [attr.aria-selected]="procesados() === t.procesados"
            (click)="cambiarVista(t.procesados)"
          >
            {{ t.etiqueta }}
          </button>
        }
      </div>
      <form class="flex gap-2" (submit)="$event.preventDefault(); aplicarBusqueda()">
        <label class="sr-only" for="buscar-tesoreria">Buscar</label>
        <input
          id="buscar-tesoreria"
          type="search"
          class="campo min-w-0 flex-1 lg:w-72 lg:flex-none"
          placeholder="Folio, emisor, RFC o UUID"
          [value]="texto()"
          (input)="texto.set($any($event.target).value)"
        />
        <button type="submit" class="btn-secundario">Buscar</button>
      </form>
    </div>

    @if (!procesados() && pagina(); as p) {
      @if (p.total > 0) {
        <p class="mt-4 text-sm text-slate-600">
          <strong>{{ p.total }}</strong> por procesar{{ p.elementos.length < p.total ? ' (mostrando ' + p.elementos.length + ')' : '' }} ·
          total en esta página <strong class="tabular-nums">{{ totalPagina() | currency: 'MXN' : 'symbol-narrow' }}</strong>
        </p>
      }
    }

    <div class="tarjeta mt-4 overflow-hidden">
      @if (documentos.error()) {
        <p class="p-6 text-sm text-rechazado-700">No se pudo cargar la bandeja.</p>
      } @else if (pagina(); as p) {
        @if (p.elementos.length === 0) {
          <p class="p-10 text-center text-slate-600">
            {{ busqueda() ? 'Sin resultados.' : procesados() ? 'Aún no hay documentos procesados.' : 'No hay documentos por procesar.' }}
          </p>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-left text-slate-600">
                <tr>
                  <th class="px-4 py-3 font-medium">Folio</th>
                  <th class="hidden px-4 py-3 font-medium sm:table-cell">Emisor</th>
                  <th class="hidden px-4 py-3 font-medium xl:table-cell">Descripción</th>
                  <th class="px-4 py-3 text-right font-medium lg:whitespace-nowrap">Monto (MXN)</th>
                  @if (procesados()) {
                    <th class="hidden px-4 py-3 font-medium sm:table-cell">Referencia</th>
                    <th class="hidden px-4 py-3 font-medium xl:table-cell">Procesado</th>
                  } @else {
                    <th class="hidden px-4 py-3 font-medium xl:table-cell">Autorizado</th>
                  }
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (d of p.elementos; track d.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="px-4 py-3 align-top sm:align-middle">
                      <a [routerLink]="['/documentos', d.id]" [queryParams]="{ desde: 'tesoreria' }" class="font-medium whitespace-nowrap text-primario-700 hover:underline">{{ d.folio }}</a>
                      <!-- En pantallas angostas, emisor, descripción y referencia se integran a la primera columna. -->
                      <span class="mt-1 line-clamp-2 wrap-anywhere text-slate-700 sm:hidden" [title]="d.nombreEmisor ?? ''">{{ d.nombreEmisor ?? '—' }}</span>
                      <span class="block text-xs text-slate-500 sm:hidden">{{ d.rfcEmisor }}</span>
                      <span class="mt-0.5 line-clamp-2 wrap-anywhere text-xs text-slate-500 sm:hidden" [title]="d.descripcion">{{ d.descripcion }}</span>
                      @if (procesados()) {
                        <span class="mt-0.5 line-clamp-2 wrap-anywhere text-xs text-slate-500 sm:hidden" [title]="d.referenciaTesoreria ?? ''">Ref. {{ d.referenciaTesoreria }}</span>
                      }
                    </td>
                    <!-- w-1/2 + max-w-0: los textos largos se recortan y reparten el espacio que dejan las demás columnas. -->
                    <td class="hidden w-1/2 max-w-0 px-4 py-3 sm:table-cell">
                      <span class="block truncate" [title]="d.nombreEmisor ?? ''">{{ d.nombreEmisor ?? '—' }}</span>
                      <span class="block text-xs text-slate-500">{{ d.rfcEmisor }}</span>
                      <span class="block truncate text-xs text-slate-500 xl:hidden" [title]="d.descripcion">{{ d.descripcion }}</span>
                    </td>
                    <td class="hidden w-1/2 max-w-0 px-4 py-3 xl:table-cell">
                      <span class="block truncate" [title]="d.descripcion">{{ d.descripcion }}</span>
                      <span class="block truncate text-xs text-slate-500" [title]="d.creadoPor">Cargó {{ d.creadoPor }}</span>
                    </td>
                    <td class="px-4 py-3 text-right align-top whitespace-nowrap tabular-nums sm:align-middle">
                      {{ d.montoMxn === null ? '—' : (d.montoMxn | currency: 'MXN' : 'symbol-narrow') }}
                      @if (d.moneda && d.moneda !== 'MXN') {
                        <span class="block text-xs text-slate-500">{{ d.total | currency: d.moneda : 'symbol-narrow' }} {{ d.moneda }}</span>
                      }
                    </td>
                    @if (procesados()) {
                      <td class="hidden max-w-36 px-4 py-3 sm:table-cell">
                        <span class="block truncate" [title]="d.referenciaTesoreria ?? ''">{{ d.referenciaTesoreria }}</span>
                      </td>
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 xl:table-cell">{{ d.fechaProcesado | date: 'dd/MM/yyyy HH:mm' }}</td>
                    } @else {
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 xl:table-cell" [title]="d.fechaAutorizacion | date: 'dd/MM/yyyy HH:mm'">
                        {{ d.fechaAutorizacion | haceTiempo }}
                      </td>
                    }
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
    </div>
  `,
})
export class BandejaTesoreria {
  protected readonly pestanas = [
    { procesados: false, etiqueta: 'Por procesar' },
    { procesados: true, etiqueta: 'Procesados' },
  ];
  protected readonly procesados = signal(false);
  protected readonly texto = signal('');
  protected readonly busqueda = signal('');
  protected readonly numeroPagina = signal(1);

  protected readonly documentos = httpResource<Pagina<DocumentoTesoreria>>(() => ({
    url: '/api/tesoreria/bandeja',
    params: {
      procesados: this.procesados(),
      pagina: this.numeroPagina(),
      ...(this.busqueda() ? { buscar: this.busqueda() } : {}),
    },
  }));

  protected readonly pagina = linkedSignal<Pagina<DocumentoTesoreria> | undefined, Pagina<DocumentoTesoreria> | undefined>({
    source: () => this.documentos.value(),
    computation: (nueva, anterior) => nueva ?? anterior?.value,
  });
  protected readonly totalPagina = computed(() => (this.pagina()?.elementos ?? []).reduce((s, d) => s + (d.montoMxn ?? 0), 0));

  protected cambiarVista(procesados: boolean): void {
    this.procesados.set(procesados);
    this.numeroPagina.set(1);
  }

  protected aplicarBusqueda(): void {
    this.busqueda.set(this.texto().trim());
    this.numeroPagina.set(1);
  }
}
