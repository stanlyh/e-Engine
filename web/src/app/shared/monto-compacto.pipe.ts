import { Pipe, PipeTransform } from '@angular/core';

const COMPACTO = new Intl.NumberFormat('es-MX', {
  style: 'currency',
  currency: 'MXN',
  notation: 'compact',
  maximumFractionDigits: 1,
});

/** $1,284 → "$1.3 mil", $4,200,000 → "$4.2 M". Para montos completos usar el pipe currency. */
@Pipe({ name: 'montoCompacto' })
export class MontoCompactoPipe implements PipeTransform {
  transform(monto: number | null | undefined): string {
    return monto == null ? '—' : COMPACTO.format(monto);
  }
}
