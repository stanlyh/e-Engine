import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApi, ConjuntoReglas, MiembrosGrupo, ReglaEdicion } from '../../core/api/admin-api';
import { mensajeError } from '../../core/http/errores';

interface PasoUi {
  clave: number;
  nombre: string;
  grupoAd: string;
}

interface ReglaUi {
  clave: number;
  id: number | null;
  nombre: string;
  /** Texto del campo; vacío en "hasta" = sin límite. */
  desde: string;
  hasta: string;
  activa: boolean;
  pasos: PasoUi[];
  modificadoPor: string | null;
  fechaModificacion: string | null;
}

type Verificacion = { estado: 'cargando' } | { estado: 'listo'; resultado: MiembrosGrupo } | { estado: 'error'; mensaje: string };

const PESOS = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', minimumFractionDigits: 2 });
const CENTAVO = 0.01;

/** Redondeo a centavos para comparar rangos sin errores de punto flotante. */
const centavos = (n: number) => Math.round(n * 100);

@Component({
  selector: 'app-reglas-editor',
  imports: [DatePipe],
  // La barra de Guardar está fija abajo: el margen evita que un campo o botón quede debajo de ella
  // cuando el navegador lo desplaza a la vista (p. ej. al recorrer el formulario con Tab).
  host: { class: 'block [&_button]:scroll-mb-32 [&_input]:scroll-mb-32' },
  template: `
    @if (errorCarga(); as e) {
      <p class="rounded-md bg-rechazado-50 px-4 py-3 text-sm text-rechazado-700" role="alert">{{ e }}</p>
    } @else if (!cargado()) {
      <p class="text-sm text-slate-500">Cargando…</p>
    } @else {
      <p class="max-w-3xl text-sm text-slate-600">
        Cada rango de monto (en pesos) define quién aprueba y en qué orden. Los cambios aplican a los documentos que se envíen
        <strong>después de guardar</strong>; los que ya están en aprobación conservan la cadena con la que se enviaron.
      </p>

      <section class="mt-4 rounded-lg border p-4" [class]="problemas().length ? 'border-pendiente-200 bg-pendiente-50' : 'border-slate-200 bg-white'" aria-labelledby="titulo-cobertura">
        <h2 id="titulo-cobertura" class="text-sm font-semibold text-slate-800">
          {{ problemas().length ? 'Hay que corregir antes de guardar' : 'Así quedará la aprobación' }}
        </h2>
        @if (problemas().length) {
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-pendiente-800">
            @for (p of problemas(); track $index) {
              <li>{{ p }}</li>
            }
          </ul>
        } @else {
          <ul class="mt-2 space-y-1 text-sm text-slate-700">
            @for (r of cobertura(); track r.clave) {
              <li>
                <span class="font-medium tabular-nums">{{ r.rango }}</span>
                <span class="text-slate-500"> → </span>{{ r.cadena }}
              </li>
            }
          </ul>
        }
      </section>

      <div class="mt-6 flex flex-col gap-4">
        @for (r of reglas(); track r.clave; let i = $index) {
          <section class="tarjeta p-4 sm:p-5" [class.opacity-70]="!r.activa" [attr.aria-label]="'Regla ' + (i + 1)">
            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_auto] lg:items-end">
              <div class="sm:col-span-2 lg:col-span-1">
                <label class="text-xs font-medium text-slate-600" [for]="'nombre-' + r.clave">Nombre</label>
                <input [id]="'nombre-' + r.clave" class="campo mt-1" maxlength="100" [value]="r.nombre" (input)="cambiarRegla(r.clave, { nombre: valor($event) })" />
              </div>
              <div>
                <label class="text-xs font-medium text-slate-600" [for]="'desde-' + r.clave">Desde (MXN)</label>
                <!-- Texto, no type="number": este reporta "" mientras se teclea "50000." y el enlace borraría lo escrito. -->
                <input [id]="'desde-' + r.clave" class="campo mt-1 tabular-nums" type="text" inputmode="decimal" autocomplete="off"
                  [value]="r.desde" (input)="cambiarRegla(r.clave, { desde: valor($event) })" />
              </div>
              <div>
                <label class="text-xs font-medium text-slate-600" [for]="'hasta-' + r.clave">Hasta (MXN)</label>
                <input [id]="'hasta-' + r.clave" class="campo mt-1 tabular-nums" type="text" inputmode="decimal" autocomplete="off"
                  placeholder="Sin límite" [value]="r.hasta" (input)="cambiarRegla(r.clave, { hasta: valor($event) })" />
              </div>
              <div class="flex items-center gap-4 sm:col-span-2 lg:col-span-1 lg:pb-2">
                <label class="inline-flex items-center gap-2 text-sm">
                  <input type="checkbox" class="size-4 accent-primario-600" [checked]="r.activa" (change)="cambiarRegla(r.clave, { activa: marcado($event) })" />
                  Activa
                </label>
                <button type="button" class="text-sm text-rechazado-700 hover:underline" (click)="quitarRegla(r.clave)">Eliminar regla</button>
              </div>
            </div>

            <h3 class="mt-5 text-xs font-semibold tracking-wide text-slate-500 uppercase">Aprobadores, en orden</h3>
            <ol class="mt-2 flex flex-col gap-3">
              @for (p of r.pasos; track p.clave; let j = $index, primero = $first, ultimo = $last) {
                <li class="rounded-md border border-slate-200 p-3">
                  <div class="grid gap-2 sm:grid-cols-[auto_minmax(0,1fr)_minmax(0,1fr)_auto] sm:items-end">
                    <span class="text-sm font-semibold text-slate-500 tabular-nums sm:pb-2">{{ j + 1 }}.</span>
                    <div>
                      <label class="text-xs text-slate-600" [for]="'paso-' + p.clave">Nombre del paso</label>
                      <input [id]="'paso-' + p.clave" class="campo mt-1" maxlength="100" placeholder="Ej. Gerente de Área"
                        [value]="p.nombre" (input)="cambiarPaso(r.clave, p.clave, { nombre: valor($event) })" />
                    </div>
                    <div>
                      <label class="text-xs text-slate-600" [for]="'grupo-' + p.clave">Grupo de AD</label>
                      <input [id]="'grupo-' + p.clave" class="campo mt-1 font-mono text-sm" maxlength="256" placeholder="DOMINIO\\Grupo"
                        [value]="p.grupoAd" (input)="cambiarPaso(r.clave, p.clave, { grupoAd: valor($event) })" />
                    </div>
                    <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm sm:pb-2">
                      <button type="button" class="text-primario-700 hover:underline disabled:text-slate-400 disabled:no-underline"
                        [disabled]="!p.grupoAd.trim()" (click)="verificar(p.grupoAd)">Verificar</button>
                      <button type="button" class="text-slate-600 hover:underline disabled:text-slate-300 disabled:no-underline" [disabled]="primero"
                        (click)="moverPaso(r.clave, j, -1)" [attr.aria-label]="'Subir el paso ' + (j + 1)">↑</button>
                      <button type="button" class="text-slate-600 hover:underline disabled:text-slate-300 disabled:no-underline" [disabled]="ultimo"
                        (click)="moverPaso(r.clave, j, 1)" [attr.aria-label]="'Bajar el paso ' + (j + 1)">↓</button>
                      <button type="button" class="text-rechazado-700 hover:underline" (click)="quitarPaso(r.clave, p.clave)"
                        [attr.aria-label]="'Quitar el paso ' + (j + 1)">Quitar</button>
                    </div>
                  </div>
                  @if (verificaciones()[p.grupoAd.trim().toLowerCase()]; as v) {
                    <p class="mt-2 text-xs" [class]="claseVerificacion(v)" role="status">{{ textoVerificacion(v) }}</p>
                  }
                </li>
              }
            </ol>
            <button type="button" class="mt-3 text-sm text-primario-700 hover:underline" (click)="agregarPaso(r.clave)">+ Agregar paso</button>

            @if (r.modificadoPor) {
              <p class="mt-3 text-xs text-slate-500">Última modificación: {{ r.modificadoPor }} · {{ r.fechaModificacion | date: 'dd/MM/yyyy HH:mm' }}</p>
            }
          </section>
        }
      </div>

      <button type="button" class="btn-secundario mt-4" (click)="agregarRegla()">+ Agregar regla</button>

      <div class="sticky bottom-0 z-10 -mx-4 mt-6 border-t border-slate-200 bg-white/95 px-4 py-3 backdrop-blur md:-mx-8 md:px-8">
        @if (errorGuardar(); as e) {
          <p class="mb-3 rounded-md bg-rechazado-50 px-3 py-2 text-sm whitespace-pre-line text-rechazado-700" role="alert">
            {{ e }}
            @if (conflicto()) {
              <button type="button" class="ml-2 font-semibold underline" (click)="cargar()">Recargar</button>
            }
          </p>
        }
        @if (mensaje(); as m) {
          <p class="mb-3 rounded-md bg-autorizado-50 px-3 py-2 text-sm text-autorizado-700" role="status">{{ m }}</p>
        }
        <div class="flex flex-wrap items-center justify-end gap-3">
          <span class="mr-auto text-sm text-slate-500">{{ hayCambios() ? 'Tienes cambios sin guardar.' : 'Sin cambios.' }}</span>
          <button type="button" class="btn-secundario" [disabled]="!hayCambios() || guardando()" (click)="descartar()">Descartar</button>
          <button type="button" class="btn-primario" [disabled]="!hayCambios() || guardando() || problemas().length > 0" (click)="guardar()">
            {{ guardando() ? 'Guardando…' : 'Guardar reglas' }}
          </button>
        </div>
      </div>
    }
  `,
})
export class ReglasEditor {
  private readonly api = inject(AdminApi);
  private siguienteClave = 1;
  private version = '';
  /** Lo último guardado, para saber si hay cambios y para "Descartar". */
  private readonly original = signal<ReglaUi[]>([]);

  protected readonly reglas = signal<ReglaUi[]>([]);
  protected readonly cargado = signal(false);
  protected readonly errorCarga = signal<string | null>(null);
  protected readonly errorGuardar = signal<string | null>(null);
  protected readonly conflicto = signal(false);
  protected readonly mensaje = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly verificaciones = signal<Record<string, Verificacion>>({});

  protected readonly hayCambios = computed(() => JSON.stringify(this.paraGuardar(this.reglas())) !== JSON.stringify(this.paraGuardar(this.original())));

  /** Misma revisión que hace la API al guardar, para avisar mientras se edita. */
  protected readonly problemas = computed(() => {
    const errores: string[] = [];
    const reglas = this.reglas();
    reglas.forEach((r, i) => {
      const nombre = r.nombre.trim() || `Regla ${i + 1}`;
      if (!r.nombre.trim()) errores.push(`La regla ${i + 1} no tiene nombre.`);
      const desde = this.numero(r.desde);
      const hasta = r.hasta.trim() === '' ? null : this.numero(r.hasta);
      if (desde === undefined || desde < 0) errores.push(`'${nombre}': el monto inicial debe ser un número de 0 en adelante.`);
      if (hasta === undefined) errores.push(`'${nombre}': el monto final debe ser un número o quedar vacío (sin límite).`);
      if (desde !== undefined && hasta != null && hasta < desde) errores.push(`'${nombre}': el monto final es menor que el inicial.`);
      if ([desde, hasta].some((m) => m != null && centavos(m) / 100 !== m)) errores.push(`'${nombre}': los montos admiten como máximo dos decimales.`);
      if (r.pasos.length === 0) errores.push(`'${nombre}' necesita al menos un paso.`);
      r.pasos.forEach((p, j) => {
        if (!p.nombre.trim() || !p.grupoAd.trim()) errores.push(`'${nombre}', paso ${j + 1}: faltan el nombre o el grupo.`);
      });
      const grupos = r.pasos.map((p) => p.grupoAd.trim().toLowerCase()).filter(Boolean);
      const repetido = grupos.find((g, k) => grupos.indexOf(g) !== k);
      if (repetido) errores.push(`'${nombre}': el grupo ${repetido} aparece en más de un paso.`);
    });
    const nombres = reglas.map((r) => r.nombre.trim().toLowerCase()).filter(Boolean);
    if (nombres.some((n, k) => nombres.indexOf(n) !== k)) errores.push('Hay dos reglas con el mismo nombre.');
    if (errores.length) return errores;

    const activas = this.activasOrdenadas();
    if (activas.length === 0) return ['Debe haber al menos una regla activa; sin ella ningún documento se puede enviar.'];
    if (activas[0].desde !== 0) errores.push(`Los montos de ${PESOS.format(0)} a ${PESOS.format(activas[0].desde - CENTAVO)} no tienen regla: la primera debe empezar en $0.`);
    for (let k = 1; k < activas.length; k++) {
      const anterior = activas[k - 1];
      const actual = activas[k];
      if (anterior.hasta === null) {
        errores.push(`'${anterior.nombre}' no tiene límite superior y se traslapa con '${actual.nombre}'.`);
        continue;
      }
      const esperado = (centavos(anterior.hasta) + 1) / 100;
      if (centavos(actual.desde) < centavos(esperado)) errores.push(`'${anterior.nombre}' y '${actual.nombre}' se traslapan: '${actual.nombre}' debe empezar en ${PESOS.format(esperado)}.`);
      else if (centavos(actual.desde) > centavos(esperado)) errores.push(`Los montos de ${PESOS.format(esperado)} a ${PESOS.format(actual.desde - CENTAVO)} no tienen regla.`);
    }
    const ultima = activas[activas.length - 1];
    if (ultima.hasta !== null) errores.push(`Los montos mayores a ${PESOS.format(ultima.hasta)} no tienen regla: la última debe quedar sin límite.`);
    return errores;
  });

  protected readonly cobertura = computed(() =>
    this.activasOrdenadas().map((r) => ({
      clave: r.clave,
      rango: r.hasta === null ? `${PESOS.format(r.desde)} en adelante` : `${PESOS.format(r.desde)} a ${PESOS.format(r.hasta)}`,
      cadena: r.pasos.map((p) => p.nombre.trim()).join(' → '),
    })),
  );

  constructor() {
    void this.cargar();
  }

  /** Para el guard de salida de la ruta. */
  tieneCambiosSinGuardar(): boolean {
    return this.cargado() && this.hayCambios();
  }

  protected async cargar(): Promise<void> {
    this.errorGuardar.set(null);
    this.conflicto.set(false);
    try {
      this.aplicar(await firstValueFrom(this.api.reglas()));
      this.cargado.set(true);
    } catch (e) {
      this.errorCarga.set(mensajeError(e));
    }
  }

  protected async guardar(): Promise<void> {
    this.guardando.set(true);
    this.errorGuardar.set(null);
    this.mensaje.set(null);
    try {
      this.aplicar(await firstValueFrom(this.api.guardarReglas(this.version, this.paraGuardar(this.reglas()))));
      this.mensaje.set('Reglas guardadas. Se aplican a los documentos que se envíen a partir de ahora.');
    } catch (e) {
      this.conflicto.set(e instanceof HttpErrorResponse && e.status === 409);
      this.errorGuardar.set(mensajeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected descartar(): void {
    this.reglas.set(structuredClone(this.original()));
    this.errorGuardar.set(null);
    this.mensaje.set(null);
  }

  protected cambiarRegla(clave: number, cambios: Partial<ReglaUi>): void {
    this.mensaje.set(null);
    this.reglas.update((rs) => rs.map((r) => (r.clave === clave ? { ...r, ...cambios } : r)));
  }

  protected cambiarPaso(regla: number, paso: number, cambios: Partial<PasoUi>): void {
    this.mensaje.set(null);
    this.reglas.update((rs) =>
      rs.map((r) => (r.clave === regla ? { ...r, pasos: r.pasos.map((p) => (p.clave === paso ? { ...p, ...cambios } : p)) } : r)),
    );
  }

  protected moverPaso(regla: number, indice: number, direccion: -1 | 1): void {
    this.reglas.update((rs) =>
      rs.map((r) => {
        if (r.clave !== regla) return r;
        const pasos = [...r.pasos];
        [pasos[indice], pasos[indice + direccion]] = [pasos[indice + direccion], pasos[indice]];
        return { ...r, pasos };
      }),
    );
  }

  protected quitarPaso(regla: number, paso: number): void {
    this.reglas.update((rs) => rs.map((r) => (r.clave === regla ? { ...r, pasos: r.pasos.filter((p) => p.clave !== paso) } : r)));
  }

  protected agregarPaso(regla: number): void {
    this.reglas.update((rs) =>
      rs.map((r) => (r.clave === regla ? { ...r, pasos: [...r.pasos, { clave: this.siguienteClave++, nombre: '', grupoAd: '' }] } : r)),
    );
  }

  /** La nueva empieza un centavo después de la última regla activa con límite. */
  protected agregarRegla(): void {
    const activas = this.activasOrdenadas();
    const ultima = activas[activas.length - 1];
    const desde = ultima?.hasta != null ? ((centavos(ultima.hasta) + 1) / 100).toFixed(2) : '';
    this.reglas.update((rs) => [
      ...rs,
      { clave: this.siguienteClave++, id: null, nombre: '', desde, hasta: '', activa: true, pasos: [{ clave: this.siguienteClave++, nombre: '', grupoAd: '' }], modificadoPor: null, fechaModificacion: null },
    ]);
  }

  protected quitarRegla(clave: number): void {
    this.reglas.update((rs) => rs.filter((r) => r.clave !== clave));
  }

  protected async verificar(grupo: string): Promise<void> {
    const clave = grupo.trim().toLowerCase();
    this.verificaciones.update((v) => ({ ...v, [clave]: { estado: 'cargando' } }));
    try {
      const resultado = await firstValueFrom(this.api.miembros(grupo.trim()));
      this.verificaciones.update((v) => ({ ...v, [clave]: { estado: 'listo', resultado } }));
    } catch (e) {
      this.verificaciones.update((v) => ({ ...v, [clave]: { estado: 'error', mensaje: mensajeError(e) } }));
    }
  }

  protected textoVerificacion(v: Verificacion): string {
    if (v.estado === 'cargando') return 'Consultando el directorio…';
    if (v.estado === 'error') return v.mensaje;
    const r = v.resultado;
    if (!r.directorioDisponible) return 'No se pudo consultar Active Directory desde el servidor.';
    if (!r.existe) return `El grupo ${r.grupo} no existe en el directorio.`;
    const conCorreo = r.miembros.filter((m) => m.email);
    const nombres = r.miembros.map((m) => m.nombre + (m.email ? '' : ' (sin correo)')).join(', ');
    return `${r.miembros.length} ${r.miembros.length === 1 ? 'miembro' : 'miembros'}, ${conCorreo.length} con correo: ${nombres}`;
  }

  protected claseVerificacion(v: Verificacion): string {
    if (v.estado !== 'listo') return v.estado === 'error' ? 'text-rechazado-700' : 'text-slate-500';
    const r = v.resultado;
    if (!r.directorioDisponible || !r.existe) return 'text-rechazado-700';
    return r.miembros.some((m) => m.email) ? 'text-autorizado-700' : 'text-pendiente-800';
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected marcado(evento: Event): boolean {
    return (evento.target as HTMLInputElement).checked;
  }

  private aplicar(conjunto: ConjuntoReglas): void {
    this.version = conjunto.version;
    const reglas: ReglaUi[] = conjunto.reglas.map((r) => ({
      clave: this.siguienteClave++,
      id: r.id,
      nombre: r.nombre,
      desde: r.montoDesde.toFixed(2),
      hasta: r.montoHasta === null ? '' : r.montoHasta.toFixed(2),
      activa: r.activa,
      pasos: r.pasos.map((p) => ({ clave: this.siguienteClave++, nombre: p.nombre, grupoAd: p.grupoAd })),
      modificadoPor: r.modificadoPor,
      fechaModificacion: r.fechaModificacion,
    }));
    this.original.set(reglas);
    this.reglas.set(structuredClone(reglas));
  }

  private paraGuardar(reglas: ReglaUi[]): ReglaEdicion[] {
    return reglas.map((r) => ({
      id: r.id,
      nombre: r.nombre.trim(),
      montoDesde: this.numero(r.desde) ?? NaN,
      montoHasta: r.hasta.trim() === '' ? null : (this.numero(r.hasta) ?? NaN),
      activa: r.activa,
      pasos: r.pasos.map((p) => ({ nombre: p.nombre.trim(), grupoAd: p.grupoAd.trim() })),
    }));
  }

  private activasOrdenadas() {
    return this.reglas()
      .filter((r) => r.activa)
      .map((r) => ({ clave: r.clave, nombre: r.nombre.trim(), desde: this.numero(r.desde) ?? 0, hasta: r.hasta.trim() === '' ? null : (this.numero(r.hasta) ?? 0), pasos: r.pasos }))
      .sort((a, b) => a.desde - b.desde || (a.hasta ?? Infinity) - (b.hasta ?? Infinity));
  }

  /** undefined si el texto no es un número. Acepta "$50,000.01": se quitan el signo y las comas de miles. */
  private numero(texto: string): number | undefined {
    const limpio = texto.replace(/[\s$,]/g, '');
    if (limpio === '') return undefined;
    const n = Number(limpio);
    return Number.isFinite(n) ? n : undefined;
  }
}
