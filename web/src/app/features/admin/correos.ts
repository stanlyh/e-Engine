import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AdminApi,
  ETIQUETA_ESTADO_NOTIFICACION,
  ETIQUETA_EVENTO,
  EstadoNotificacion,
  NotificacionAdmin,
  ResumenCorreo,
} from '../../core/api/admin-api';
import { Pagina } from '../../core/api/modelos';
import { mensajeError } from '../../core/http/errores';
import { Paginacion } from '../../shared/ui/paginacion';

const CLASE_ESTADO: Record<EstadoNotificacion, string> = {
  Pendiente: 'bg-pendiente-50 text-pendiente-800 ring-pendiente-200',
  Enviada: 'bg-autorizado-50 text-autorizado-700 ring-autorizado-200',
  Fallida: 'bg-rechazado-50 text-rechazado-700 ring-rechazado-200',
  SinDestinatarios: 'bg-rechazado-50 text-rechazado-700 ring-rechazado-200',
};

type FiltroCorreo = 'pendientes' | 'enviadas' | 'problemas';

/** Las mismas tres categorías de las tarjetas; "con problemas" agrupa fallidas y sin destinatarios. */
const FILTROS: { clave: FiltroCorreo; etiqueta: string; estados: EstadoNotificacion[] }[] = [
  { clave: 'pendientes', etiqueta: 'Pendientes', estados: ['Pendiente'] },
  { clave: 'enviadas', etiqueta: 'Enviadas', estados: ['Enviada'] },
  { clave: 'problemas', etiqueta: 'Con problemas (fallidas o sin destinatarios)', estados: ['Fallida', 'SinDestinatarios'] },
];

const MODO: Record<ResumenCorreo['modo'], { texto: string; clase: string }> = {
  Smtp: { texto: 'Se envían por el servidor SMTP.', clase: 'text-autorizado-700' },
  Carpeta: { texto: 'Modo de pruebas: se guardan como archivos .eml en el servidor, no se envían.', clase: 'text-pendiente-800' },
  Desactivado: { texto: 'El envío está desactivado: los avisos se acumulan como pendientes.', clase: 'text-rechazado-700' },
};

@Component({
  selector: 'app-correos',
  imports: [DatePipe, RouterLink, Paginacion],
  template: `
    @if (resumen.value(); as r) {
      <section class="grid grid-cols-2 gap-3 sm:grid-cols-4" aria-label="Resumen de correos">
        <div class="tarjeta col-span-2 p-4 sm:col-span-1">
          <p class="text-sm font-medium text-slate-600">Envío</p>
          <p class="mt-1 text-lg font-semibold">{{ r.modo === 'Smtp' ? 'SMTP' : r.modo }}</p>
          <p class="mt-1 text-xs" [class]="modo[r.modo].clase">{{ modo[r.modo].texto }}</p>
        </div>
        @for (t of tarjetas(); track t.etiqueta) {
          <button type="button" class="tarjeta p-4 text-left hover:shadow-md" [class.ring-2]="filtro() === t.filtro" [class.ring-primario-500]="filtro() === t.filtro"
            (click)="filtrar(filtro() === t.filtro ? null : t.filtro)" [attr.aria-pressed]="filtro() === t.filtro">
            <p class="text-sm font-medium text-slate-600">{{ t.etiqueta }}</p>
            <p class="mt-1 text-2xl font-semibold tabular-nums" [class.text-rechazado-700]="t.alerta && t.valor > 0">{{ t.valor }}</p>
          </button>
        }
      </section>

      <div class="mt-4 flex flex-wrap items-center gap-3">
        <button type="button" class="btn-secundario" [disabled]="enviandoPrueba() || !r.miCorreo" (click)="enviarPrueba()">Enviarme un correo de prueba</button>
        <span class="text-sm text-slate-500">
          {{ r.miCorreo ? 'Llegará a ' + r.miCorreo + '.' : 'Tu usuario no tiene correo en Active Directory.' }}
        </span>
        <button type="button" class="ml-auto text-sm text-primario-700 hover:underline" (click)="actualizar()">Actualizar</button>
      </div>
    }

    @if (mensaje(); as m) {
      <p class="mt-3 rounded-md bg-autorizado-50 px-3 py-2 text-sm text-autorizado-700" role="status">{{ m }}</p>
    }
    @if (error(); as e) {
      <p class="mt-3 rounded-md bg-rechazado-50 px-3 py-2 text-sm text-rechazado-700" role="alert">{{ e }}</p>
    }

    <div class="mt-4 flex items-center gap-2">
      <label for="filtro-correos" class="text-sm text-slate-600">Mostrar</label>
      <select id="filtro-correos" class="campo w-auto" (change)="filtrar($any($event.target).value || null)">
        <option value="" [selected]="!filtro()">Todos</option>
        @for (f of filtros; track f.clave) {
          <option [value]="f.clave" [selected]="filtro() === f.clave">{{ f.etiqueta }}</option>
        }
      </select>
    </div>

    <div class="tarjeta mt-3 overflow-hidden">
      @if (lista.error()) {
        <p class="p-6 text-sm text-rechazado-700">No se pudo cargar la lista de correos.</p>
      } @else if (pagina(); as p) {
        @if (p.elementos.length === 0) {
          <p class="p-10 text-center text-slate-600">{{ filtro() ? 'No hay correos con este filtro.' : 'Aún no se ha generado ningún aviso.' }}</p>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 text-left text-slate-600">
                <tr>
                  <th class="px-4 py-3 font-medium">Aviso</th>
                  <th class="hidden px-4 py-3 font-medium lg:table-cell">Para</th>
                  <th class="px-4 py-3 text-right font-medium sm:text-left">Estado</th>
                  <th class="hidden px-4 py-3 font-medium xl:table-cell">Creado</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (n of p.elementos; track n.id) {
                  <tr class="align-top">
                    <!-- w-full + max-w-0: asunto y destinatarios largos se recortan en lugar de ensanchar la tabla. -->
                    <td class="w-full max-w-0 px-4 py-3">
                      <p class="truncate font-medium text-slate-800" [title]="n.asunto">{{ n.asunto }}</p>
                      <p class="mt-0.5 text-xs text-slate-500">
                        {{ etiquetaEvento[n.evento] }}
                        @if (n.documentoId && n.folio) {
                          · <a [routerLink]="['/documentos', n.documentoId]" class="text-primario-700 hover:underline">{{ n.folio }}</a>
                        }
                      </p>
                      <p class="mt-0.5 truncate text-xs text-slate-500 lg:hidden" [title]="destino(n)">Para: {{ destino(n) }}</p>
                      <p class="mt-0.5 text-xs text-slate-400 xl:hidden">{{ n.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}</p>
                    </td>
                    <td class="hidden max-w-0 min-w-48 px-4 py-3 lg:table-cell">
                      <p class="truncate" [title]="destino(n)">{{ destino(n) }}</p>
                      @if (n.destinatarios) {
                        <p class="truncate text-xs text-slate-500" [title]="n.destinatarios">{{ n.destinatarios }}</p>
                      }
                    </td>
                    <td class="px-4 py-3 text-right sm:text-left">
                      <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset" [class]="claseEstado[n.estado]">
                        {{ etiquetaEstado[n.estado] }}
                      </span>
                      @if (n.estado === 'Pendiente' && n.intentos > 0) {
                        <p class="mt-1 text-xs whitespace-nowrap text-slate-500">Reintento {{ n.proximoIntento | date: 'HH:mm' }}</p>
                      }
                      @if (n.ultimoError) {
                        <p class="mt-1 max-w-56 text-xs text-rechazado-700 sm:max-w-72">{{ n.ultimoError }}</p>
                      }
                      @if (n.estado === 'Fallida' || n.estado === 'SinDestinatarios') {
                        <button type="button" class="mt-1 text-xs font-medium text-primario-700 hover:underline" [disabled]="ocupado()" (click)="reintentar(n.id)">
                          Reintentar
                        </button>
                      }
                    </td>
                    <td class="hidden px-4 py-3 whitespace-nowrap text-slate-500 xl:table-cell">
                      {{ n.fechaCreacion | date: 'dd/MM/yyyy HH:mm' }}
                      @if (n.fechaEnvio) {
                        <span class="block text-xs">Enviado {{ n.fechaEnvio | date: 'HH:mm' }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-paginacion [pagina]="p" nombre="correos" (cambiar)="numeroPagina.set($event)" />
        }
      } @else {
        <p class="p-6 text-sm text-slate-500">Cargando…</p>
      }
    </div>
  `,
})
export class Correos {
  private readonly api = inject(AdminApi);

  protected readonly modo = MODO;
  protected readonly claseEstado = CLASE_ESTADO;
  protected readonly etiquetaEstado = ETIQUETA_ESTADO_NOTIFICACION;
  protected readonly etiquetaEvento = ETIQUETA_EVENTO;
  protected readonly filtros = FILTROS;

  protected readonly filtro = signal<FiltroCorreo | null>(null);
  protected readonly numeroPagina = linkedSignal({ source: this.filtro, computation: () => 1 });
  protected readonly mensaje = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly ocupado = signal(false);
  protected readonly enviandoPrueba = signal(false);

  protected readonly resumen = httpResource<ResumenCorreo>(() => '/api/admin/correos/resumen');
  protected readonly lista = httpResource<Pagina<NotificacionAdmin>>(() => ({
    url: '/api/admin/correos',
    params: { pagina: this.numeroPagina(), estado: FILTROS.find((f) => f.clave === this.filtro())?.estados ?? [] },
  }));
  /** Conserva la página anterior mientras carga la siguiente. */
  protected readonly pagina = linkedSignal<Pagina<NotificacionAdmin> | undefined, Pagina<NotificacionAdmin> | undefined>({
    source: () => this.lista.value(),
    computation: (nueva, anterior) => nueva ?? anterior?.value,
  });

  protected readonly tarjetas = computed(() => {
    const r = this.resumen.value();
    return r
      ? [
          { etiqueta: 'Pendientes', valor: r.pendientes, filtro: 'pendientes' as FiltroCorreo, alerta: false },
          { etiqueta: 'Enviadas', valor: r.enviadas, filtro: 'enviadas' as FiltroCorreo, alerta: false },
          { etiqueta: 'Con problemas', valor: r.conProblemas, filtro: 'problemas' as FiltroCorreo, alerta: true },
        ]
      : [];
  });

  protected destino(n: NotificacionAdmin): string {
    return n.esGrupo ? `Grupo ${n.destino}` : (n.destino ?? '—');
  }

  protected filtrar(filtro: FiltroCorreo | null): void {
    this.filtro.set(filtro);
  }

  protected actualizar(): void {
    this.mensaje.set(null);
    this.resumen.reload();
    this.lista.reload();
  }

  protected async enviarPrueba(): Promise<void> {
    this.enviandoPrueba.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.enviarPrueba());
      this.mensaje.set('Correo de prueba en cola; se envía en unos segundos. Pulsa Actualizar para ver su estado.');
      this.resumen.reload();
      this.lista.reload();
    } catch (e) {
      this.error.set(mensajeError(e));
    } finally {
      this.enviandoPrueba.set(false);
    }
  }

  protected async reintentar(id: number): Promise<void> {
    this.ocupado.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.reintentar(id));
      this.mensaje.set('El correo volvió a la cola.');
      this.resumen.reload();
      this.lista.reload();
    } catch (e) {
      this.error.set(mensajeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }
}
