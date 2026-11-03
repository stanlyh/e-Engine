/** Tipos que devuelve la API (/api/documentos). Los enums viajan como texto. */

export type EstadoDocumento = 'Borrador' | 'EnAprobacion' | 'Rechazado' | 'EnTesoreria' | 'Procesado';
export type TipoArchivo = 'Xml' | 'Pdf';
export type EstadoPaso = 'EnEspera' | 'Pendiente' | 'Aprobado' | 'Rechazado' | 'Cancelado';
export type AccionHistorial =
  | 'Creado'
  | 'ArchivoCargado'
  | 'Enviado'
  | 'Reenviado'
  | 'PasoAprobado'
  | 'Autorizado'
  | 'Rechazado'
  | 'Procesado'
  | 'Editado';

export interface UsuarioRef {
  id: number;
  nombre: string;
}

export interface DatosCfdi {
  uuid: string;
  rfcEmisor: string;
  nombreEmisor: string;
  rfcReceptor: string;
  serie: string | null;
  folio: string | null;
  fecha: string;
  subTotal: number;
  total: number;
  moneda: string;
  tipoCambio: number | null;
  tipoComprobante: string;
}

export interface Archivo {
  id: number;
  tipo: TipoArchivo;
  version: number;
  vigente: boolean;
  nombreOriginal: string;
  tamanoBytes: number;
  fechaSubida: string;
}

export interface Aprobacion {
  ronda: number;
  orden: number;
  nombre: string;
  estado: EstadoPaso;
  resueltoPor: UsuarioRef | null;
  fechaResolucion: string | null;
  comentario: string | null;
}

export interface Historial {
  accion: AccionHistorial;
  estadoAnterior: EstadoDocumento | null;
  estadoNuevo: EstadoDocumento;
  usuario: UsuarioRef;
  comentario: string | null;
  fecha: string;
}

export interface Permisos {
  editar: boolean;
  enviar: boolean;
  resolver: boolean;
  procesar: boolean;
}

export interface DocumentoDetalle {
  id: number;
  folio: string;
  descripcion: string;
  estado: EstadoDocumento;
  creadoPor: UsuarioRef;
  cfdi: DatosCfdi | null;
  montoMxn: number | null;
  ronda: number;
  fechaCreacion: string;
  fechaEnvio: string | null;
  fechaAutorizacion: string | null;
  fechaRechazo: string | null;
  fechaProcesado: string | null;
  referenciaTesoreria: string | null;
  archivos: Archivo[];
  aprobaciones: Aprobacion[];
  historial: Historial[];
  permisos: Permisos;
}

export interface DocumentoResumen {
  id: number;
  folio: string;
  descripcion: string;
  estado: EstadoDocumento;
  nombreEmisor: string | null;
  rfcEmisor: string | null;
  total: number | null;
  moneda: string | null;
  montoMxn: number | null;
  pasoActual: string | null;
  fechaCreacion: string;
  fechaEnvio: string | null;
}

export interface Pagina<T> {
  elementos: T[];
  total: number;
  numeroPagina: number;
  tamanoPagina: number;
}

export interface ReglaAprobacion {
  id: number;
  nombre: string;
  montoDesde: number;
  montoHasta: number | null;
  activa: boolean;
  pasos: { orden: number; nombre: string; grupoAd: string }[];
}

export const ESTADOS: { valor: EstadoDocumento; etiqueta: string }[] = [
  { valor: 'Borrador', etiqueta: 'Borrador' },
  { valor: 'EnAprobacion', etiqueta: 'En aprobación' },
  { valor: 'Rechazado', etiqueta: 'Rechazado' },
  { valor: 'EnTesoreria', etiqueta: 'En Tesorería' },
  { valor: 'Procesado', etiqueta: 'Procesado' },
];

export const ETIQUETA_ESTADO: Record<EstadoDocumento, string> = Object.fromEntries(
  ESTADOS.map((e) => [e.valor, e.etiqueta]),
) as Record<EstadoDocumento, string>;

export const ETIQUETA_ACCION: Record<AccionHistorial, string> = {
  Creado: 'Documento creado',
  ArchivoCargado: 'Archivo cargado',
  Enviado: 'Enviado a aprobación',
  Reenviado: 'Reenviado tras corrección',
  PasoAprobado: 'Paso aprobado',
  Autorizado: 'Autorizado, pasa a Tesorería',
  Rechazado: 'Rechazado',
  Procesado: 'Procesado por Tesorería',
  Editado: 'Descripción editada',
};

/** Debe coincidir con Documentos:TamanoMaximoMb del servidor. */
export const TAMANO_MAXIMO_MB = 10;

/** Cadena de aprobación que corresponde a un monto según las reglas activas. */
export function rutaParaMonto(reglas: ReglaAprobacion[], monto: number): string[] | null {
  const regla = reglas.find(
    (r) => r.activa && monto >= r.montoDesde && (r.montoHasta === null || monto <= r.montoHasta),
  );
  return regla ? [...regla.pasos].sort((a, b) => a.orden - b.orden).map((p) => p.nombre) : null;
}

export interface PendienteAprobacion {
  id: number;
  folio: string;
  descripcion: string;
  creadoPor: string;
  nombreEmisor: string | null;
  rfcEmisor: string | null;
  total: number | null;
  moneda: string | null;
  montoMxn: number | null;
  pasoActual: string;
  ordenPaso: number;
  totalPasos: number;
  esperaDesde: string | null;
}

export interface ResueltoPorMi {
  id: number;
  folio: string;
  descripcion: string;
  nombreEmisor: string | null;
  total: number | null;
  moneda: string | null;
  paso: string;
  miDecision: EstadoPaso;
  comentario: string | null;
  fechaResolucion: string;
  estadoActual: EstadoDocumento;
}

export interface DocumentoTesoreria {
  id: number;
  folio: string;
  descripcion: string;
  creadoPor: string;
  nombreEmisor: string | null;
  rfcEmisor: string | null;
  uuid: string | null;
  total: number | null;
  moneda: string | null;
  montoMxn: number | null;
  fechaAutorizacion: string;
  fechaProcesado: string | null;
  referenciaTesoreria: string | null;
}

/** De dónde se abrió el detalle, para el enlace de regreso (?desde=). */
export type OrigenDetalle = 'aprobaciones' | 'tesoreria';

export interface Indicador {
  cantidad: number;
  monto: number;
}

export interface Movimiento {
  documentoId: number;
  folio: string;
  descripcion: string;
  accion: AccionHistorial;
  estadoNuevo: EstadoDocumento;
  usuario: string;
  comentario: string | null;
  fecha: string;
}

export interface Dashboard {
  todos: boolean;
  enviadosDesde: string | null;
  indicadores: Record<'enviados' | 'pendientes' | 'autorizados' | 'rechazados' | 'enTesoreria' | 'procesados', Indicador>;
  esperanMiAprobacion: number | null;
  porProcesar: Indicador | null;
  movimientos: Movimiento[];
}

/**
 * Filtros de la lista de documentos: un estado o un grupo de estados.
 * "enviados" y "autorizados" coinciden con los indicadores del inicio.
 */
export const FILTROS_ESTADO: { clave: string; etiqueta: string; estados: EstadoDocumento[] }[] = [
  ...ESTADOS.map((e) => ({ clave: e.valor as string, etiqueta: e.etiqueta, estados: [e.valor] })),
  { clave: 'enviados', etiqueta: 'Enviados (todos menos borrador)', estados: ['EnAprobacion', 'Rechazado', 'EnTesoreria', 'Procesado'] },
  { clave: 'autorizados', etiqueta: 'Autorizados (Tesorería + procesados)', estados: ['EnTesoreria', 'Procesado'] },
];
