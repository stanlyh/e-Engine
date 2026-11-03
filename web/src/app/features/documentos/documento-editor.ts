import { CurrencyPipe, DatePipe, Location } from '@angular/common';
import { HttpEventType, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom, lastValueFrom, tap } from 'rxjs';
import { DocumentosApi } from '../../core/api/documentos-api';
import { DocumentoDetalle, ReglaAprobacion, TipoArchivo, rutaParaMonto } from '../../core/api/modelos';
import { mensajeError } from '../../core/http/errores';
import { EstadoBadge } from '../../shared/ui/estado-badge';
import { FileSlot } from '../../shared/ui/file-slot';

type Slot = 'xml' | 'pdf';

/**
 * Captura y corrección de un documento. En /documentos/nuevo el documento se crea en cuanto se carga el
 * primer archivo; a partir de ahí cada archivo se sube y valida en el servidor al momento.
 */
@Component({
  selector: 'app-documento-editor',
  imports: [RouterLink, CurrencyPipe, DatePipe, EstadoBadge, FileSlot],
  template: `
    <a [routerLink]="documento() ? ['/documentos', documento()!.id] : '/documentos'" class="text-sm text-primario-700 hover:underline">
      ← {{ documento() ? 'Volver al documento' : 'Mis documentos' }}
    </a>

    <div class="mt-2 flex flex-wrap items-center gap-3">
      <h1 class="text-2xl font-semibold tracking-tight">
        {{ documento() ? (documento()!.estado === 'Rechazado' ? 'Corregir ' : 'Editar ') + documento()!.folio : 'Nuevo documento' }}
      </h1>
      @if (documento(); as d) {
        <app-estado-badge [estado]="d.estado" />
      }
    </div>

    @if (cargando()) {
      <p class="mt-6 text-sm text-slate-500">Cargando…</p>
    } @else {
      @if (motivoRechazo(); as r) {
        <div class="mt-6 rounded-lg border border-rechazado-200 bg-rechazado-50 p-4" role="alert">
          <p class="text-sm font-semibold text-rechazado-800">Rechazado por {{ r.quien }} ({{ r.paso }})</p>
          <p class="mt-1 text-sm text-rechazado-700">{{ r.motivo }}</p>
          <p class="mt-2 text-xs text-rechazado-700">Corrige lo necesario y vuelve a enviarlo; la aprobación empezará desde el primer paso.</p>
        </div>
      }

      <section class="tarjeta mt-6 p-5">
        <label for="descripcion" class="text-sm font-medium text-slate-800">Descripción</label>
        <textarea
          id="descripcion"
          rows="2"
          maxlength="500"
          class="campo mt-1"
          placeholder="Ej. Servicio de mantenimiento de octubre"
          [value]="descripcion()"
          (input)="descripcion.set($any($event.target).value)"
        ></textarea>
        <div class="mt-1 flex justify-between text-xs text-slate-500">
          <span>{{ documento() ? '' : 'Escribe la descripción y después carga los archivos.' }}</span>
          <span>{{ descripcion().length }}/500</span>
        </div>
      </section>

      <section class="mt-6 grid gap-4 md:grid-cols-2" aria-label="Archivos">
        <app-file-slot
          etiqueta="1. XML del CFDI"
          descripcion="Factura electrónica 4.0 timbrada"
          extension=".xml"
          [archivo]="archivoVigente('Xml')"
          [urlVer]="urlArchivo('Xml')"
          [progreso]="progreso().xml"
          [error]="errores().xml"
          [deshabilitado]="!puedeCargar()"
          (seleccionado)="subir('xml', $event)"
        />
        <app-file-slot
          etiqueta="2. PDF de la factura"
          descripcion="Representación impresa"
          extension=".pdf"
          [archivo]="archivoVigente('Pdf')"
          [urlVer]="urlArchivo('Pdf')"
          [progreso]="progreso().pdf"
          [error]="errores().pdf"
          [deshabilitado]="!puedeCargar()"
          (seleccionado)="subir('pdf', $event)"
        />
      </section>
      @if (!puedeCargar() && !documento()) {
        <p class="mt-2 text-xs text-slate-500">Escribe la descripción para habilitar la carga.</p>
      }

      @if (documento()?.cfdi; as c) {
        <section class="tarjeta mt-6 p-5">
          <h2 class="text-sm font-semibold text-slate-800">Datos leídos del CFDI</h2>
          <dl class="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <div><dt class="text-slate-500">Emisor</dt><dd>{{ c.nombreEmisor || '—' }} <span class="text-slate-500">{{ c.rfcEmisor }}</span></dd></div>
            <div><dt class="text-slate-500">Serie / folio</dt><dd>{{ c.serie ?? '' }}{{ c.folio ? '-' + c.folio : '' }}</dd></div>
            <div><dt class="text-slate-500">Fecha</dt><dd>{{ c.fecha | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
            <div><dt class="text-slate-500">Total</dt><dd class="tabular-nums">{{ c.total | currency: c.moneda : 'symbol-narrow' }} {{ c.moneda }}</dd></div>
            @if (c.moneda !== 'MXN') {
              <div><dt class="text-slate-500">Monto en pesos</dt><dd class="tabular-nums">{{ documento()!.montoMxn | currency: 'MXN' : 'symbol-narrow' }} (TC {{ c.tipoCambio }})</dd></div>
            }
            <div class="sm:col-span-2 lg:col-span-1"><dt class="text-slate-500">UUID</dt><dd class="font-mono text-xs break-all">{{ c.uuid }}</dd></div>
          </dl>
          @if (rutaEstimada(); as ruta) {
            <p class="mt-4 rounded-md bg-pendiente-50 px-3 py-2 text-sm text-pendiente-800">
              Por su monto, lo aprobarán: <strong>{{ ruta.join(' → ') }}</strong>
            </p>
          }
        </section>
      }

      @if (error(); as e) {
        <p class="mt-6 rounded-md bg-rechazado-50 px-4 py-3 text-sm text-rechazado-700" role="alert">{{ e }}</p>
      }

      <div class="mt-6 flex flex-wrap justify-end gap-3">
        @if (documento()) {
          <button type="button" class="btn-secundario" [disabled]="ocupado() || !descripcionCambiada()" (click)="guardarDescripcion()">
            Guardar borrador
          </button>
        }
        <button type="button" class="btn-primario" [disabled]="ocupado() || !puedeEnviar()" (click)="enviar()">
          {{ documento()?.estado === 'Rechazado' ? 'Reenviar a aprobación' : 'Enviar a aprobación' }}
        </button>
      </div>
      @if (documento() && !puedeEnviar() && !ocupado()) {
        <p class="mt-2 text-right text-xs text-slate-500">Para enviar se necesitan el XML y el PDF.</p>
      }
    }
  `,
})
export class DocumentoEditor {
  /** Parámetro de ruta; ausente en /documentos/nuevo. */
  readonly id = input<string>();

  private readonly api = inject(DocumentosApi);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  protected readonly documento = signal<DocumentoDetalle | null>(null);
  protected readonly descripcion = signal('');
  protected readonly cargando = signal(false);
  protected readonly ocupado = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly progreso = signal<Record<Slot, number | null>>({ xml: null, pdf: null });
  protected readonly errores = signal<Record<Slot, string | null>>({ xml: null, pdf: null });

  private readonly reglas = httpResource<ReglaAprobacion[]>(() => '/api/reglas-aprobacion', { defaultValue: [] });

  protected readonly descripcionCambiada = computed(
    () => !!this.documento() && this.descripcion().trim() !== this.documento()!.descripcion,
  );
  protected readonly puedeCargar = computed(
    () => this.descripcion().trim().length > 0 && (!this.documento() || this.documento()!.permisos.editar),
  );
  protected readonly puedeEnviar = computed(
    () => !!this.documento()?.permisos.enviar && this.descripcion().trim().length > 0,
  );
  protected readonly rutaEstimada = computed(() => {
    const monto = this.documento()?.montoMxn;
    return monto == null ? null : rutaParaMonto(this.reglas.value(), monto);
  });
  protected readonly motivoRechazo = computed(() => {
    const d = this.documento();
    if (d?.estado !== 'Rechazado') return null;
    const paso = d.aprobaciones.find((a) => a.ronda === d.ronda && a.estado === 'Rechazado');
    return paso ? { quien: paso.resueltoPor?.nombre ?? '', paso: paso.nombre, motivo: paso.comentario ?? '' } : null;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) void this.cargar(Number(id));
    });
  }

  protected archivoVigente(tipo: TipoArchivo) {
    return this.documento()?.archivos.find((a) => a.tipo === tipo && a.vigente) ?? null;
  }

  protected urlArchivo(tipo: TipoArchivo): string | null {
    const archivo = this.archivoVigente(tipo);
    return archivo ? this.api.urlArchivo(this.documento()!.id, archivo.id) : null;
  }

  protected async subir(slot: Slot, archivo: File): Promise<void> {
    this.error.set(null);
    this.errores.update((e) => ({ ...e, [slot]: null }));
    try {
      const id = await this.asegurarDocumento();
      this.progreso.update((p) => ({ ...p, [slot]: 0 }));
      await lastValueFrom(
        this.api.subirArchivo(id, slot, archivo).pipe(
          tap((evento) => {
            if (evento.type === HttpEventType.UploadProgress && evento.total) {
              this.progreso.update((p) => ({ ...p, [slot]: Math.round((100 * evento.loaded) / evento.total!) }));
            }
          }),
        ),
      );
      this.documento.set(await firstValueFrom(this.api.obtener(id)));
    } catch (e) {
      this.errores.update((err) => ({ ...err, [slot]: mensajeError(e) }));
    } finally {
      this.progreso.update((p) => ({ ...p, [slot]: null }));
    }
  }

  protected async guardarDescripcion(): Promise<void> {
    await this.ejecutar(async () => {
      await this.persistirDescripcion();
    });
  }

  protected async enviar(): Promise<void> {
    await this.ejecutar(async () => {
      await this.persistirDescripcion();
      const id = this.documento()!.id;
      await firstValueFrom(this.api.enviar(id));
      await this.router.navigate(['/documentos', id]);
    });
  }

  private async cargar(id: number): Promise<void> {
    this.cargando.set(true);
    try {
      const documento = await firstValueFrom(this.api.obtener(id));
      if (!documento.permisos.editar) {
        await this.router.navigate(['/documentos', id], { replaceUrl: true });
        return;
      }
      this.documento.set(documento);
      this.descripcion.set(documento.descripcion);
    } catch (e) {
      this.error.set(mensajeError(e));
    } finally {
      this.cargando.set(false);
    }
  }

  /** Crea el borrador la primera vez y cambia la URL a /documentos/{id}/editar sin recargar la pantalla. */
  private async asegurarDocumento(): Promise<number> {
    const actual = this.documento();
    if (actual) return actual.id;
    const { id } = await firstValueFrom(this.api.crear(this.descripcion().trim()));
    this.location.replaceState(`/documentos/${id}/editar`);
    const creado = await firstValueFrom(this.api.obtener(id));
    this.documento.set(creado);
    return id;
  }

  private async persistirDescripcion(): Promise<void> {
    if (!this.descripcionCambiada()) return;
    const id = this.documento()!.id;
    await firstValueFrom(this.api.editar(id, this.descripcion().trim()));
    this.documento.set(await firstValueFrom(this.api.obtener(id)));
  }

  private async ejecutar(accion: () => Promise<void>): Promise<void> {
    this.ocupado.set(true);
    this.error.set(null);
    try {
      await accion();
    } catch (e) {
      this.error.set(mensajeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }
}
