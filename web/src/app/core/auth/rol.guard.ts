import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { Rol, Sesion } from './sesion';

/** Permite la ruta si el usuario tiene alguno de los roles indicados. */
export const requiereRol =
  (...roles: Rol[]): CanMatchFn =>
  () =>
    inject(Sesion).tieneRol(...roles) || inject(Router).createUrlTree(['/sin-acceso']);
