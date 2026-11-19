import { Injectable, signal } from '@angular/core';

/** Mensaje de una sola lectura para la pantalla a la que se navega (p. ej. "Se eliminó el borrador…"). */
@Injectable({ providedIn: 'root' })
export class Avisos {
  private readonly pendiente = signal<string | null>(null);

  mostrar(texto: string): void {
    this.pendiente.set(texto);
  }

  /** Devuelve el aviso y lo descarta, para que no reaparezca al volver a la pantalla. */
  tomar(): string | null {
    const texto = this.pendiente();
    this.pendiente.set(null);
    return texto;
  }
}
