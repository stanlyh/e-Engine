import { Component, input } from '@angular/core';

/** Marcador para las pantallas que se construyen en fases posteriores. */
@Component({
  selector: 'app-pendiente',
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">{{ titulo() }}</h1>
    <div class="mt-6 rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-slate-500">
      Esta pantalla se construye en la fase {{ fase() }}.
    </div>
  `,
})
export class Pendiente {
  readonly titulo = input.required<string>();
  readonly fase = input.required<number>();
}
