import { Component, ElementRef, input, output, viewChild } from '@angular/core';

/**
 * Diálogo de confirmación para acciones que no se pueden deshacer.
 * El padre ejecuta la acción en (confirmado), muestra el error en [error] y llama a cerrar() al terminar.
 */
@Component({
  selector: 'app-confirmar-dialogo',
  template: `
    <dialog
      #dialogo
      class="m-auto w-[min(28rem,calc(100vw-2rem))] rounded-lg p-0 shadow-xl backdrop:bg-slate-900/40"
      [attr.aria-labelledby]="'titulo-' + idUnico"
    >
      <form class="p-6" (submit)="$event.preventDefault(); confirmado.emit()">
        <h2 [id]="'titulo-' + idUnico" class="text-lg font-semibold">{{ titulo() }}</h2>
        <p class="mt-2 text-sm text-slate-600">{{ mensaje() }}</p>
        @if (error(); as e) {
          <p class="mt-3 rounded-md bg-rechazado-50 px-3 py-2 text-sm text-rechazado-700" role="alert">{{ e }}</p>
        }
        <div class="mt-6 flex justify-end gap-3">
          <button type="button" class="btn-secundario" (click)="cerrar()">Cancelar</button>
          <button type="submit" class="btn-peligro" [disabled]="ocupado()">{{ textoBoton() }}</button>
        </div>
      </form>
    </dialog>
  `,
})
export class ConfirmarDialogo {
  private static siguiente = 0;
  protected readonly idUnico = ++ConfirmarDialogo.siguiente;

  readonly titulo = input.required<string>();
  readonly mensaje = input.required<string>();
  readonly textoBoton = input.required<string>();
  readonly ocupado = input(false);
  readonly error = input<string | null>(null);
  readonly confirmado = output<void>();

  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  abrir(): void {
    this.dialogo().nativeElement.showModal();
  }

  cerrar(): void {
    this.dialogo().nativeElement.close();
  }
}
