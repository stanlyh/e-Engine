import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Archivo, DocumentoDetalle, DocumentoResumen, EstadoDocumento, Pagina } from './modelos';

export interface FiltroDocumentos {
  estado?: EstadoDocumento | null;
  buscar?: string | null;
  pagina?: number;
  tamano?: number;
}

@Injectable({ providedIn: 'root' })
export class DocumentosApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/documentos';

  listar(filtro: FiltroDocumentos): Observable<Pagina<DocumentoResumen>> {
    let params = new HttpParams().set('pagina', filtro.pagina ?? 1).set('tamano', filtro.tamano ?? 20);
    if (filtro.estado) params = params.set('estado', filtro.estado);
    if (filtro.buscar?.trim()) params = params.set('buscar', filtro.buscar.trim());
    return this.http.get<Pagina<DocumentoResumen>>(this.base, { params });
  }

  obtener(id: number): Observable<DocumentoDetalle> {
    return this.http.get<DocumentoDetalle>(`${this.base}/${id}`);
  }

  crear(descripcion: string): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, { descripcion });
  }

  editar(id: number, descripcion: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, { descripcion });
  }

  /** Emite eventos de progreso de carga; el último trae el archivo registrado. */
  subirArchivo(id: number, tipo: 'xml' | 'pdf', archivo: File): Observable<HttpEvent<Archivo>> {
    const datos = new FormData();
    datos.append('archivo', archivo, archivo.name);
    return this.http.post<Archivo>(`${this.base}/${id}/archivos/${tipo}`, datos, {
      reportProgress: true,
      observe: 'events',
    });
  }

  enviar(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/enviar`, null);
  }

  aprobar(id: number, comentario: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/aprobar`, { comentario });
  }

  rechazar(id: number, motivo: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/rechazar`, { motivo });
  }

  procesar(id: number, referencia: string, comentario: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/procesar`, { referencia, comentario });
  }

  /** URL absoluta para enlaces e iframes (no pasan por el interceptor). */
  urlArchivo(id: number, archivoId: number, descargar = false): string {
    return `${environment.apiUrl}${this.base}/${id}/archivos/${archivoId}${descargar ? '?descargar=true' : ''}`;
  }
}
