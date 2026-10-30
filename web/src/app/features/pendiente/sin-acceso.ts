import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sin-acceso',
  imports: [RouterLink],
  template: `
    <h1 class="text-2xl font-semibold tracking-tight">Sin acceso</h1>
    <p class="mt-2 text-slate-600">Tu usuario no pertenece al grupo de Active Directory requerido para esta sección.</p>
    <a routerLink="/" class="mt-4 inline-block text-primario-700 hover:underline">Volver al inicio</a>
  `,
})
export class SinAcceso {}
