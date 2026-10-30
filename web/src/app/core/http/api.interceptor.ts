import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

const METODOS_SEGUROS = ['GET', 'HEAD', 'OPTIONS'];

/**
 * - Antepone la URL de la API a las rutas /api.
 * - Envía las credenciales de Windows (withCredentials) también en desarrollo, donde la API está en otro puerto.
 * - Copia la cookie XSRF-TOKEN al encabezado X-XSRF-TOKEN en escrituras. El interceptor XSRF de Angular
 *   no lo hace para URLs absolutas, por eso se reemplaza con este.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) return next(req);

  const token = METODOS_SEGUROS.includes(req.method) ? null : leerCookie('XSRF-TOKEN');
  return next(
    req.clone({
      url: environment.apiUrl + req.url,
      withCredentials: true,
      setHeaders: token ? { 'X-XSRF-TOKEN': token } : {},
    }),
  );
};

function leerCookie(nombre: string): string | null {
  const par = document.cookie.split('; ').find((c) => c.startsWith(`${nombre}=`));
  return par ? decodeURIComponent(par.slice(nombre.length + 1)) : null;
}
