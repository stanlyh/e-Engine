import { HttpErrorResponse } from '@angular/common/http';

/** Mensaje legible a partir de un ProblemDetails de la API. */
export function mensajeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'No hay conexión con el servidor.';
    const cuerpo = error.error as { detail?: string; title?: string } | null;
    return cuerpo?.detail ?? cuerpo?.title ?? `Error ${error.status}`;
  }
  return 'Ocurrió un error inesperado.';
}
