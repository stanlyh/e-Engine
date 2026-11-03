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

    <div class="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div class="inline-flex rounded-lg border border-slate-200 bg-white p-1" role="tablist">
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
          class="campo sm:w-72"
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
                  <th class="px-4 py-3 font-medium">Emisor</th>
                  <th class="hidden px-4 py-3 font-medium md:table-cell">Descripción</th>
                  <th class="px-4 py-3 text-right font-medium">Monto (MXN)</th>
                  @if (procesados()) {
                    <th class="hidden px-4 py-3 font-medium sm:table-cell">Referencia</th>
                    <th class="hidden px-4 py-3 font-medium lg:table-cell">Procesado</th>
                  } @else {
                    <th class="hidden px-4 py-3 font-medium lg:table-cell">Autorizado</th>
                  }
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (d of p.elementos; track d.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="px-4 py-3 whitespace-nowrap">
                      <a [routerLink]="['/documentos', d.id]" [queryParams]="{ desde: 'tesoreria' }" class="font-medium text-primario-700 hover:underline">{{ d.folio }}</a>
                      @if (procesados()) {
                        <span class="block text-xs text-slate-500 sm:hidden">Ref. {{ d.referenciaTesoreria }}</span>
                      }
                    </td>
                    <td class="max-w-xs px-4 py-3">
                      <span class="block truncate">{{ d.nombreEmisor ?? '—' }}</span>
                      <span class="text-xs text-slate-500">{{ d.rfcEmisor }}</span>
                    </td>
                    <td class="hidden max-w-xs px-4 py-3 md:table-cell">
                      <span class="block truncate" [title]="d.descripcion">{{ d.descripcion }}</span>
                      <span class="text-xs text-slate-500">Cargó {{ d.creadoPor }}</span>
                    </td>
                    <td class="px-4 py-3 text-right whitespace-nowrap tabular-nums">
                      {{ d.montoMxn === null ? '—' : (d.montoMxn | currency: 'MXN' : 'symbol-narrow') }}
                      @if (d.moneda && d.moneda !== 'MXN') {
                        <span class="block text-xs text-slate-500">{{ d.total | currency: d.moneda : 'symbol-narrow' }} {{ d.moneda }}</span>
                      }
                    </td>
                    @if (procesados()) {
                      <td class="hidden px-4 py-3 whitespace-nowrap sm:table-cell">{{ d.referenciaTesoreria }}</td>
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 lg:table-cell">{{ d.fechaProcesado | date: 'dd/MM/yyyy HH:mm' }}</td>
                    } @else {
                      <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 lg:table-cell" [title]="d.fechaAutorizacion | date: 'dd/MM/yyyy HH:mm'">
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
