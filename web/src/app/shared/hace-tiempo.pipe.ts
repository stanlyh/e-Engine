import { Pipe, PipeTransform } from '@angular/core';

const FORMATO = new Intl.RelativeTimeFormat('es-MX', { numeric: 'auto' });

/** "hace 3 horas", "ayer", "hace 2 días". Se calcula al renderizar; no se actualiza solo. */
@Pipe({ name: 'haceTiempo' })
export class HaceTiempoPipe implements PipeTransform {
  transform(fecha: string | null | undefined): string {
    if (!fecha) return '—';
    const minutos = Math.round((new Date(fecha).getTime() - Date.now()) / 60_000);
    if (Math.abs(minutos) < 60) return FORMATO.format(minutos, 'minute');
    const horas = Math.round(minutos / 60);
    if (Math.abs(horas) < 24) return FORMATO.format(horas, 'hour');
    return FORMATO.format(Math.round(horas / 24), 'day');
  }
}
