import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type Rol = 'Usuario' | 'Aprobador' | 'Tesoreria' | 'Admin';

export interface UsuarioSesion {
  id: number;
  cuenta: string;
  nombre: string;
  email: string | null;
  roles: Rol[];
  grupos: string[];
}

/** Usuario autenticado con Windows Authentication; se carga una vez al iniciar la app. */
@Injectable({ providedIn: 'root' })
export class Sesion {
  private readonly http = inject(HttpClient);
  private readonly _usuario = signal<UsuarioSesion | null>(null);
  private readonly _error = signal<string | null>(null);

  readonly usuario = this._usuario.asReadonly();
  readonly error = this._error.asReadonly();
  readonly nombre = computed(() => this._usuario()?.nombre ?? '');

  /** También recibe la cookie XSRF-TOKEN que exigen las peticiones de escritura. */
  async cargar(): Promise<void> {
    try {
      this._usuario.set(await firstValueFrom(this.http.get<UsuarioSesion>('/api/me')));
    } catch {
      this._error.set('No fue posible identificar tu usuario de Windows. Verifica que estés en la red de la empresa.');
    }
  }

  tieneRol(...roles: Rol[]): boolean {
    const actuales = this._usuario()?.roles ?? [];
    return roles.some((r) => actuales.includes(r));
  }
}
