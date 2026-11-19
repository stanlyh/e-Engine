import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Pagina } from './modelos';

export interface PasoRegla {
  nombre: string;
  grupoAd: string;
}

export interface ReglaAdmin {
  id: number;
  nombre: string;
  montoDesde: number;
  montoHasta: number | null;
  activa: boolean;
  pasos: PasoRegla[];
  modificadoPor: string | null;
  fechaModificacion: string | null;
}

export interface ConjuntoReglas {
  /** Huella del contenido; si cambió al guardar, la API responde 409. */
  version: string;
  reglas: ReglaAdmin[];
}

export interface ReglaEdicion {
  /** null = regla nueva. Las existentes que no se envían se eliminan. */
  id: number | null;
  nombre: string;
  montoDesde: number;
  montoHasta: number | null;
  activa: boolean;
  pasos: PasoRegla[];
}

export interface MiembrosGrupo {
  grupo: string;
  directorioDisponible: boolean;
  existe: boolean;
  miembros: { cuenta: string; nombre: string; email: string | null }[];
}

export type EventoNotificacion = 'PorAprobar' | 'Rechazado' | 'Autorizado' | 'ParaTesoreria' | 'Procesado' | 'Prueba';
export type EstadoNotificacion = 'Pendiente' | 'Enviada' | 'Fallida' | 'SinDestinatarios';

export interface ResumenCorreo {
  modo: 'Smtp' | 'Carpeta' | 'Desactivado';
  pendientes: number;
  enviadas: number;
  conProblemas: number;
  miCorreo: string | null;
}

export interface NotificacionAdmin {
  id: number;
  evento: EventoNotificacion;
  estado: EstadoNotificacion;
  asunto: string;
  documentoId: number | null;
  folio: string | null;
  /** Grupo de AD o nombre del usuario. */
  destino: string | null;
  esGrupo: boolean;
  destinatarios: string | null;
  intentos: number;
  fechaCreacion: string;
  proximoIntento: string;
  fechaEnvio: string | null;
  ultimoError: string | null;
}

export const ETIQUETA_EVENTO: Record<EventoNotificacion, string> = {
  PorAprobar: 'Por aprobar',
  Rechazado: 'Rechazado',
  Autorizado: 'Autorizado',
  ParaTesoreria: 'Para Tesorería',
  Procesado: 'Procesado',
  Prueba: 'Prueba',
};

export const ETIQUETA_ESTADO_NOTIFICACION: Record<EstadoNotificacion, string> = {
  Pendiente: 'Pendiente',
  Enviada: 'Enviada',
  Fallida: 'Fallida',
  SinDestinatarios: 'Sin destinatarios',
};

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  reglas(): Observable<ConjuntoReglas> {
    return this.http.get<ConjuntoReglas>('/api/admin/reglas');
  }

  guardarReglas(version: string, reglas: ReglaEdicion[]): Observable<ConjuntoReglas> {
    return this.http.put<ConjuntoReglas>('/api/admin/reglas', { version, reglas });
  }

  miembros(grupo: string): Observable<MiembrosGrupo> {
    return this.http.get<MiembrosGrupo>('/api/admin/grupos/miembros', { params: { grupo } });
  }

  resumenCorreo(): Observable<ResumenCorreo> {
    return this.http.get<ResumenCorreo>('/api/admin/correos/resumen');
  }

  reintentar(id: number): Observable<void> {
    return this.http.post<void>(`/api/admin/correos/${id}/reintentar`, null);
  }

  enviarPrueba(): Observable<{ id: number }> {
    return this.http.post<{ id: number }>('/api/admin/correos/prueba', null);
  }
}
