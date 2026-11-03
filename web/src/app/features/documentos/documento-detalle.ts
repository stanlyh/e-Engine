import { CurrencyPipe, DatePipe, DecimalPipe, UpperCasePipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DocumentosApi } from '../../core/api/documentos-api';
import { Archivo, DocumentoDetalle } from '../../core/api/modelos';
import { mensajeError } from '../../core/http/errores';
import { EstadoBadge } from '../../shared/ui/estado-badge';
import { HistorialTimeline } from '../../shared/ui/historial-timeline';
import { Seguimiento } from '../../shared/ui/seguimiento';

type Accion = 'aprobar' | 'rechazar' | 'procesar';

const DIALOGO: Record<Accion, { titulo: string; campo: string; obligatorio: boolean; boton: string; clase: string }> = {
  aprobar: { titulo: 'Aprobar documento', campo: 'Comentario (opcional)', obligatorio: false, boton: 'Aprobar', clase: 'btn-exito' },
  rechazar: { titulo: 'Rechazar documento', campo: 'Motivo del rechazo', obligatorio: true, boton: 'Rechazar', clase: 'btn-peligro' },
  procesar: { titulo: 'Procesar en Tesorería', campo: 'Referencia (transferencia, póliza…)', obligatorio: true, boton: 'Marcar como procesado', clase: 'btn-primario' },
};

@Component({
  selector: 'app-documento-detalle',
  imports: [RouterLink, CurrencyPipe, DatePipe, DecimalPipe, UpperCasePipe, EstadoBadge, Seguimiento, HistorialTimeline],
  template: `
    <a routerLink="/documentos" class="text-sm text-primario-700 hover:underline">← Mis documentos</a>

    @if (error() && !documento()) {
      <p class="mt-6 rounded-md bg-rechazado-50 px-4 py-3 text-sm text-rechazado-700" role="alert">{{ error() }}</p>
    } @else if (!documento()) {
      <p class="mt-6 text-sm text-slate-500">Cargando…</p>
    } @else {
      @let d = documento()!;
      <div class="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex flex-wrap items-center gap-3">
            <h1 class="text-2xl font-semibold tracking-tight">{{ d.folio }}</h1>
            <app-estado-badge [estado]="d.estado" />
          </div>
          <p class="mt-1 text-slate-600">{{ d.descripcion }}</p>
          <p class="mt-1 text-xs text-slate-500">Cargado por {{ d.creadoPor.nombre }} · {{ d.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}</p>
        </div>

        <div class="flex flex-wrap gap-2">
          @if (d.permisos.editar) {
            <a [routerLink]="['/documentos', d.id, 'editar']" class="btn-secundario">
              {{ d.estado === 'Rechazado' ? 'Corregir' : 'Editar' }}
            </a>
          }
          @if (d.permisos.enviar) {
            <button type="button" class="btn-primario" [disabled]="ocupado()" (click)="enviar()">
              {{ d.estado === 'Rechazado' ? 'Reenviar' : 'Enviar a aprobación' }}
            </button>
          }
          @if (d.permisos.resolver) {
            <button type="button" class="btn-peligro" (click)="abrir('rechazar')">Rechazar</button>
            <button type="button" class="btn-exito" (click)="abrir('aprobar')">Aprobar</button>
          }
          @if (d.permisos.procesar) {
            <button type="button" class="btn-primario" (click)="abrir('procesar')">Procesar</button>
          }
        </div>
      </div>

      @if (mensaje(); as m) {
        <p class="mt-4 rounded-md bg-autorizado-50 px-4 py-3 text-sm text-autorizado-700" role="status">{{ m }}</p>
      }
      @if (error(); as e) {
        <p class="mt-4 rounded-md bg-rechazado-50 px-4 py-3 text-sm text-rechazado-700" role="alert">{{ e }}</p>
      }

      <section class="tarjeta mt-6 p-5" aria-labelledby="titulo-seguimiento">
        <h2 id="titulo-seguimiento" class="mb-4 text-sm font-semibold text-slate-800">Seguimiento</h2>
        <app-seguimiento [documento]="d" />
      </section>

      <div class="mt-6 grid gap-6 lg:grid-cols-3">
        <div class="flex flex-col gap-6 lg:col-span-2">
          @if (d.cfdi; as c) {
            <section class="tarjeta p-5">
              <h2 class="text-sm font-semibold text-slate-800">Factura (CFDI)</h2>
              <dl class="mt-3 grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
                <div><dt class="text-slate-500">Emisor</dt><dd>{{ c.nombreEmisor || '—' }}<span class="block text-xs text-slate-500">{{ c.rfcEmisor }}</span></dd></div>
                <div><dt class="text-slate-500">Receptor</dt><dd>{{ c.rfcReceptor }}</dd></div>
                <div><dt class="text-slate-500">Serie / folio</dt><dd>{{ c.serie ?? '' }}{{ c.folio ? '-' + c.folio : '' }}</dd></div>
                <div><dt class="text-slate-500">Fecha de emisión</dt><dd>{{ c.fecha | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
                <div><dt class="text-slate-500">Subtotal</dt><dd class="tabular-nums">{{ c.subTotal | currency: c.moneda : 'symbol-narrow' }}</dd></div>
                <div>
                  <dt class="text-slate-500">Total</dt>
                  <dd class="text-base font-semibold tabular-nums">{{ c.total | currency: c.moneda : 'symbol-narrow' }} {{ c.moneda }}</dd>
                  @if (c.moneda !== 'MXN') {
                    <dd class="text-xs text-slate-500">{{ d.montoMxn | currency: 'MXN' : 'symbol-narrow' }} MXN (TC {{ c.tipoCambio }})</dd>
                  }
                </div>
                <div class="sm:col-span-2"><dt class="text-slate-500">UUID</dt><dd class="font-mono text-xs break-all">{{ c.uuid }}</dd></div>
              </dl>
            </section>
          }

          <section class="tarjeta p-5">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h2 class="text-sm font-semibold text-slate-800">Archivos</h2>
              @if (versionesAnteriores().length) {
                <button type="button" class="text-xs text-primario-700 hover:underline" (click)="verAnteriores.set(!verAnteriores())">
                  {{ verAnteriores() ? 'Ocultar' : 'Ver' }} versiones anteriores ({{ versionesAnteriores().length }})
                </button>
              }
            </div>
            <ul class="mt-3 divide-y divide-slate-100">
              @for (a of archivosMostrados(); track a.id) {
                <li class="flex flex-wrap items-center justify-between gap-2 py-2 text-sm" [class.opacity-60]="!a.vigente">
                  <div class="min-w-0">
                    <span class="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs font-semibold text-slate-600">{{ a.tipo | uppercase }}</span>
                    <span class="break-all">{{ a.nombreOriginal }}</span>
                    <span class="block text-xs text-slate-500">
                      v{{ a.version }}{{ a.vigente ? '' : ' (reemplazado)' }} · {{ a.tamanoBytes / 1024 | number: '1.0-0' }} KB ·
                      {{ a.fechaSubida | date: 'dd/MM/yyyy HH:mm' }}
                    </span>
                  </div>
                  <div class="flex gap-3 text-xs">
                    @if (a.tipo === 'Pdf') {
                      <a [href]="api.urlArchivo(d.id, a.id)" target="_blank" rel="noopener" class="text-primario-700 hover:underline">Abrir</a>
                    }
                    <a [href]="api.urlArchivo(d.id, a.id, true)" class="text-primario-700 hover:underline">Descargar</a>
                  </div>
                </li>
              } @empty {
                <li class="py-2 text-sm text-slate-500">Sin archivos.</li>
              }
            </ul>
          </section>

          @if (urlPdf(); as url) {
            <!-- Los navegadores móviles no muestran PDF dentro de la página; ahí se usa el enlace Abrir. -->
            <section class="tarjeta hidden overflow-hidden md:block">
              <h2 class="px-5 pt-4 text-sm font-semibold text-slate-800">Vista previa del PDF</h2>
              <iframe [src]="url" title="Vista previa del PDF" class="mt-3 h-[70vh] w-full border-t border-slate-200"></iframe>
            </section>
          }
        </div>

        <section class="tarjeta h-fit p-5" aria-labelledby="titulo-historial">
          <h2 id="titulo-historial" class="mb-4 text-sm font-semibold text-slate-800">Historial</h2>
          <app-historial-timeline [historial]="d.historial" />
        </section>
      </div>
    }

    <dialog
      #dialogo
      class="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-lg p-0 shadow-xl backdrop:bg-slate-900/40"
      (close)="accion.set(null)"
    >
      @if (accion(); as a) {
        <form class="p-6" (submit)="$event.preventDefault(); confirmar()">
          <h2 class="text-lg font-semibold">{{ textos[a].titulo }}</h2>
          <p class="mt-1 text-sm text-slate-500">{{ documento()?.folio }} · {{ documento()?.descripcion }}</p>
          <label for="texto-accion" class="mt-4 block text-sm font-medium text-slate-800">{{ textos[a].campo }}</label>
          @if (a === 'procesar') {
            <input id="texto-accion" class="campo mt-1" maxlength="100" [value]="texto()" (input)="texto.set($any($event.target).value)" />
            <label for="comentario-accion" class="mt-3 block text-sm font-medium text-slate-800">Comentario (opcional)</label>
            <input id="comentario-accion" class="campo mt-1" maxlength="500" [value]="comentario()" (input)="comentario.set($any($event.target).value)" />
          } @else {
            <textarea id="texto-accion" rows="3" maxlength="1000" class="campo mt-1" [value]="texto()" (input)="texto.set($any($event.target).value)"></textarea>
          }
          @if (errorDialogo(); as e) {
            <p class="mt-3 rounded-md bg-rechazado-50 px-3 py-2 text-sm text-rechazado-700" role="alert">{{ e }}</p>
          }
          <div class="mt-6 flex justify-end gap-3">
            <button type="button" class="btn-secundario" (click)="cerrar()">Cancelar</button>
            <button type="submit" [class]="textos[a].clase" [disabled]="ocupado() || (textos[a].obligatorio && !texto().trim())">
              {{ textos[a].boton }}
            </button>
          </div>
        </form>
      }
    </dialog>
  `,
})
export class DocumentoDetallePagina {
  readonly id = input.required<string>();

  protected readonly api = inject(DocumentosApi);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly elementoDialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  protected readonly textos = DIALOGO;
  protected readonly documento = signal<DocumentoDetalle | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly mensaje = signal<string | null>(null);
  protected readonly ocupado = signal(false);
  protected readonly verAnteriores = signal(false);
  protected readonly accion = signal<Accion | null>(null);
  protected readonly texto = signal('');
  protected readonly comentario = signal('');
  protected readonly errorDialogo = signal<string | null>(null);

  protected readonly versionesAnteriores = computed(() => this.documento()?.archivos.filter((a) => !a.vigente) ?? []);
  protected readonly archivosMostrados = computed<Archivo[]>(() => {
    const archivos = this.documento()?.archivos ?? [];
    return this.verAnteriores() ? archivos : archivos.filter((a) => a.vigente);
  });
  protected readonly urlPdf = computed<SafeResourceUrl | null>(() => {
    const d = this.documento();
    const pdf = d?.archivos.find((a) => a.tipo === 'Pdf' && a.vigente);
    // La URL se arma solo con ids numéricos de la API.
    return d && pdf ? this.sanitizer.bypassSecurityTrustResourceUrl(this.api.urlArchivo(d.id, pdf.id)) : null;
  });

  constructor() {
    effect(() => void this.cargar(Number(this.id())));
  }

  protected abrir(accion: Accion): void {
    this.accion.set(accion);
    this.texto.set('');
    this.comentario.set('');
    this.errorDialogo.set(null);
    this.elementoDialogo().nativeElement.showModal();
  }

  protected cerrar(): void {
    this.elementoDialogo().nativeElement.close();
  }

  protected async confirmar(): Promise<void> {
    const accion = this.accion();
    const d = this.documento();
    if (!accion || !d) return;
    this.ocupado.set(true);
    this.errorDialogo.set(null);
    try {
      const texto = this.texto().trim();
      const peticion =
        accion === 'aprobar'
          ? this.api.aprobar(d.id, texto || null)
          : accion === 'rechazar'
            ? this.api.rechazar(d.id, texto)
            : this.api.procesar(d.id, texto, this.comentario().trim() || null);
      await firstValueFrom(peticion);
      this.cerrar();
      this.mensaje.set(
        accion === 'aprobar' ? 'Documento aprobado.' : accion === 'rechazar' ? 'Documento rechazado; regresa a quien lo cargó.' : 'Documento procesado.',
      );
      await this.cargar(d.id);
    } catch (e) {
      this.errorDialogo.set(mensajeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }

  protected async enviar(): Promise<void> {
    const d = this.documento();
    if (!d) return;
    this.ocupado.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.enviar(d.id));
      this.mensaje.set('Documento enviado a aprobación.');
      await this.cargar(d.id);
    } catch (e) {
      this.error.set(mensajeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }

  private async cargar(id: number): Promise<void> {
    try {
      this.documento.set(await firstValueFrom(this.api.obtener(id)));
      this.error.set(null);
    } catch (e) {
      this.error.set(mensajeError(e));
    }
  }
}
