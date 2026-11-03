import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { Archivo, TAMANO_MAXIMO_MB } from '../../core/api/modelos';

/** Zona para cargar un archivo de un tipo (XML o PDF): arrastrar y soltar o clic, con progreso y errores. */
@Component({
  selector: 'app-file-slot',
  imports: [DatePipe, DecimalPipe],
  template: `
    <div
      class="relative flex min-h-40 flex-col items-center justify-center rounded-lg border-2 border-dashed p-5 text-center transition-colors"
      [class]="claseZona()"
      (dragover)="alArrastrar($event)"
      (dragleave)="arrastrando.set(false)"
      (drop)="alSoltar($event)"
    >
      <p class="text-sm font-semibold text-slate-800">{{ etiqueta() }}</p>
      <p class="text-xs text-slate-500">{{ descripcion() }}</p>

      @if (progreso() !== null) {
        <div class="mt-4 w-full max-w-xs" role="progressbar" [attr.aria-valuenow]="progreso()" aria-valuemin="0" aria-valuemax="100">
          <div class="h-2 overflow-hidden rounded-full bg-slate-200">
            <div class="h-full rounded-full bg-primario-600 transition-all" [style.width.%]="progreso()"></div>
          </div>
          <p class="mt-1 text-xs text-slate-500">Subiendo y validando… {{ progreso() }}%</p>
        </div>
      } @else if (archivo(); as a) {
        <div class="mt-3 flex flex-col items-center gap-1">
          <span class="inline-flex items-center gap-1 rounded-full bg-autorizado-50 px-3 py-1 text-xs font-medium text-autorizado-700">
            ✓ {{ a.nombreOriginal }}
          </span>
          <span class="text-xs text-slate-500">
            v{{ a.version }} · {{ a.tamanoBytes / 1024 | number: '1.0-0' }} KB · {{ a.fechaSubida | date: 'dd/MM/yyyy HH:mm' }}
          </span>
          @if (urlVer(); as url) {
            <a [href]="url" target="_blank" rel="noopener" class="text-xs text-primario-700 hover:underline">Ver archivo</a>
          }
        </div>
      }

      @if (!deshabilitado() && progreso() === null) {
        <label class="mt-4 cursor-pointer">
          <span class="btn-secundario">{{ archivo() ? 'Reemplazar' : 'Seleccionar archivo' }}</span>
          <input type="file" class="sr-only" [accept]="aceptar()" (change)="alElegir($event)" />
        </label>
        <p class="mt-2 text-xs text-slate-400">o arrástralo aquí · máx. {{ maximoMb }} MB</p>
      }

      @if (errorMostrado(); as e) {
        <p class="mt-3 rounded-md bg-rechazado-50 px-3 py-2 text-xs text-rechazado-700" role="alert">{{ e }}</p>
      }
    </div>
  `,
})
export class FileSlot {
  readonly etiqueta = input.required<string>();
  readonly descripcion = input('');
  /** Extensión esperada, con punto: ".xml" | ".pdf". */
  readonly extension = input.required<string>();
  readonly archivo = input<Archivo | null>(null);
  readonly urlVer = input<string | null>(null);
  readonly progreso = input<number | null>(null);
  readonly error = input<string | null>(null);
  readonly deshabilitado = input(false);

  readonly seleccionado = output<File>();

  protected readonly maximoMb = TAMANO_MAXIMO_MB;
  protected readonly arrastrando = signal(false);
  private readonly errorLocal = signal<string | null>(null);

  protected readonly aceptar = computed(() => this.extension());
  protected readonly errorMostrado = computed(() => this.errorLocal() ?? this.error());
  protected readonly claseZona = computed(() => {
    if (this.errorMostrado()) return 'border-rechazado-200 bg-rechazado-50/40';
    if (this.arrastrando()) return 'border-primario-500 bg-primario-50';
    if (this.archivo()) return 'border-autorizado-200 bg-white';
    return 'border-slate-300 bg-white';
  });

  protected alArrastrar(evento: DragEvent): void {
    if (this.deshabilitado()) return;
    evento.preventDefault();
    this.arrastrando.set(true);
  }

  protected alSoltar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(false);
    const archivo = evento.dataTransfer?.files[0];
    if (archivo && !this.deshabilitado()) this.validarYEmitir(archivo);
  }

  protected alElegir(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = '';
    if (archivo) this.validarYEmitir(archivo);
  }

  private validarYEmitir(archivo: File): void {
    if (!archivo.name.toLowerCase().endsWith(this.extension())) {
      this.errorLocal.set(`Debe ser un archivo ${this.extension().toUpperCase().slice(1)}.`);
      return;
    }
    if (archivo.size > TAMANO_MAXIMO_MB * 1024 * 1024) {
      this.errorLocal.set(`El archivo supera ${TAMANO_MAXIMO_MB} MB.`);
      return;
    }
    this.errorLocal.set(null);
    this.seleccionado.emit(archivo);
  }
}
