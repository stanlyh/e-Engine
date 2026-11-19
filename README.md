# Aprobación de Documentos (CFDI)

El usuario carga una factura (XML CFDI + PDF) y la envía a aprobación. Según el monto, la aprueba una cadena de gerentes; ya autorizada pasa a Tesorería, que la marca como procesada. Si se rechaza en cualquier paso, regresa al usuario para corregirla.

**Stack:** .NET 10 (Minimal APIs, EF Core 10) · Angular 22 + Tailwind CSS v4 · SQL Server · Windows Authentication (intranet)

## Estructura

```
src/
  Aprobaciones.Domain/          Entidades y máquina de estados (Documento, ReglaAprobacion)
  Aprobaciones.Application/     Abstracciones (DbContext, usuario actual, directorio AD), roles
  Aprobaciones.Infrastructure/  EF Core, migraciones, datos iniciales, consulta a AD
  Aprobaciones.Api/             Endpoints, Windows Auth, grupos AD → roles, antiforgery
tests/
  Aprobaciones.Domain.Tests/    Pruebas de transiciones y reglas por monto
web/                            Angular 22 (el build sale a src/Aprobaciones.Api/wwwroot)
```

## Flujo

```
Borrador ─enviar→ EnAprobacion (paso 1 → paso 2 → …) ─último paso→ EnTesoreria ─procesar→ Procesado
                      │ rechazo en cualquier paso
                      ▼
                  Rechazado ─corregir y reenviar→ EnAprobacion (nueva ronda, desde el paso 1)
```

## Reglas de aprobación (datos iniciales, editables)

| Rango (MXN) | Aprobadores, en orden |
|---|---|
| 0 – 50,000 | Gerente de Área |
| 50,000.01 – 250,000 | Gerente de Área → Gerente Financiero |
| > 250,000 | Gerente de Área → Gerente Financiero → Director General |

Cada paso apunta a un grupo de AD (`APR_GerenteArea`, `APR_GerenteFinanciero`, `APR_DirectorGeneral`). En producción usa el formato `DOMINIO\Grupo`.

## Grupos de AD → roles

Se configuran en `appsettings.json`, sección `Seguridad`:

| Rol | Origen |
|---|---|
| Usuario | `GrupoUsuarios` (vacío = cualquier usuario del dominio) |
| Aprobador | Pertenecer a algún grupo de un paso de aprobación |
| Tesoreria | `GrupoTesoreria` |
| Admin | `GrupoAdmin` |

En **Development**, `Seguridad:GruposSimulados` asigna grupos al usuario actual sin consultar AD.

## Archivos y CFDI (sección `Documentos` de appsettings)

| Clave | Uso |
|---|---|
| `RutaArchivos` | Carpeta absoluta de los XML/PDF: `{año}/{mes}/{documentoId}/{guid}.ext` (en desarrollo: `D:\repos\e-Engine\docs`) |
| `TamanoMaximoMb` | Límite por archivo (10 MB) |
| `RfcEmpresa` | Si se configura, el RFC receptor del CFDI debe coincidir. En desarrollo se usa `EKU9003173C9`, el RFC de pruebas del SAT |
| `TiposComprobante` | Tipos aceptados (por defecto `I`, ingreso) |

Al subir el XML se valida que sea CFDI 4.0 timbrado (sin DTD, para evitar XXE) y que el UUID no esté en otro documento. El PDF se valida por su firma `%PDF-`.

## API

| Método | Ruta | Rol |
|---|---|---|
| GET | `/api/me` | Todos (entrega la cookie XSRF-TOKEN) |
| GET | `/api/reglas-aprobacion` | Todos |
| GET | `/api/documentos?estado=&buscar=&pagina=&tamano=` | Mis documentos |
| POST | `/api/documentos` `{ descripcion }` | Usuario |
| GET | `/api/documentos/{id}` | Con acceso: detalle, archivos, cadena, historial y permisos |
| PUT | `/api/documentos/{id}` `{ descripcion }` | Creador (Borrador/Rechazado) |
| POST | `/api/documentos/{id}/archivos/xml` · `/pdf` (multipart `archivo`) | Creador |
| GET | `/api/documentos/{id}/archivos/{archivoId}[?descargar=true]` | Con acceso |
| POST | `/api/documentos/{id}/enviar` | Creador |
| POST | `/api/documentos/{id}/aprobar` `{ comentario? }` | Aprobador del paso actual |
| POST | `/api/documentos/{id}/rechazar` `{ motivo }` | Aprobador del paso actual |
| POST | `/api/documentos/{id}/procesar` `{ referencia, comentario? }` | Tesorería |
| GET | `/api/aprobaciones/pendientes?buscar=&pagina=` | Aprobador: paso actual de sus grupos, sin los propios ni los que ya aprobó |
| GET | `/api/aprobaciones/resueltos?buscar=&pagina=` | Aprobador: sus decisiones |
| GET | `/api/tesoreria/bandeja?procesados=&buscar=&pagina=` | Tesorería |

| DELETE | `/api/documentos/{id}` | Creador, solo borradores que nunca se enviaron (borra también sus archivos) |
| GET | `/api/admin/reglas` | Admin: reglas con su `version` |
| PUT | `/api/admin/reglas` `{ version, reglas[] }` | Admin: reemplaza la tabla completa; 400 si hay huecos o traslapes, 409 si otra persona la cambió |
| GET | `/api/admin/grupos/miembros?grupo=` | Admin: verifica un grupo de AD y quién recibirá los avisos |
| GET | `/api/admin/correos/resumen` · `/api/admin/correos?estado=&pagina=` | Admin: cola de correos |
| POST | `/api/admin/correos/{id}/reintentar` · `/api/admin/correos/prueba` | Admin |

Las escrituras exigen el encabezado `X-XSRF-TOKEN`.

## Avisos por correo

Cada cambio de estado deja el correo en la tabla `Notificaciones`, en la misma transacción. Un proceso en segundo plano los envía cada `Correo:IntervaloSegundos` y reintenta con espera creciente (1, 5, 15 y 60 min); después queda **Fallida** y se puede reintentar desde Administración → Correos.

| Evento | Destinatarios |
|---|---|
| Enviado, reenviado o paso aprobado | Miembros del grupo de AD del paso siguiente, sin quien cargó el documento ni quienes ya aprobaron en esa ronda |
| Autorizado | Quien lo cargó y el grupo `Seguridad:GrupoTesoreria` |
| Rechazado (con motivo) · Procesado (con referencia) | Quien lo cargó |

Si nadie del destino tiene correo en AD, queda **Sin destinatarios**.

| Clave | Uso |
|---|---|
| `Correo:Modo` | `Smtp`, `Carpeta` (escribe `.eml`, para desarrollo) o `Desactivado` (los avisos se acumulan como pendientes) |
| `Correo:Host` · `Puerto` · `Ssl` | Servidor SMTP |
| `Correo:UsarCredencialesWindows` | Autenticarse con la cuenta del Application Pool (relay de Exchange); si no, `Usuario` y `Contrasena` |
| `Correo:Remitente` · `NombreRemitente` | Obligatorio para enviar |
| `Correo:RedirigirA` | Ambientes de prueba: todo va a esta dirección, con los destinatarios reales al inicio del cuerpo |
| `Notificaciones:UrlBase` | Dirección del sitio para los enlaces de los correos (vacía = sin enlace) |

En **Development**, la sección `DirectorioSimulado` (usuarios con correo y grupos) reemplaza a AD; así se prueban los avisos fuera del dominio. Con `Correo:Modo = Carpeta` los correos quedan en `correos/` (ignorada por git).

## Desarrollo

1. Cadena de conexión en `src/Aprobaciones.Api/appsettings.Development.json` (`ConnectionStrings:Aprobaciones`).
2. Crear la base:
   ```
   dotnet ef database update -p src/Aprobaciones.Infrastructure -s src/Aprobaciones.Api
   ```
3. API: `dotnet run --project src/Aprobaciones.Api --launch-profile http` (http://localhost:5214)
4. Angular: `cd web && npm start` (http://localhost:4200). Llama a la API con credenciales de Windows vía CORS.
5. Pruebas: `dotnet test` (dominio, reglas, avisos, lector CFDI y almacén en disco)
6. Con `sqlcmd` usa `-I`: los índices filtrados requieren `QUOTED_IDENTIFIER ON`.

## Producción (IIS)

1. `cd web && npm run build` (genera `src/Aprobaciones.Api/wwwroot`)
2. `dotnet publish src/Aprobaciones.Api -c Release`
3. En el sitio IIS: **Windows Authentication habilitado** y **Anonymous deshabilitado**.
4. La cuenta del Application Pool necesita acceso a SQL Server y permiso de escritura en `Documentos:RutaArchivos`.
5. Correos: configurar la sección `Correo` y `Notificaciones:UrlBase`. El envío corre dentro del sitio, así que el Application Pool debe quedar activo: **Start Mode = AlwaysRunning**, **Idle Time-out = 0** y en el sitio **Preload Enabled = True**. Si IIS lo detiene, los avisos esperan en la cola hasta la siguiente petición.
6. Desde Administración → Correos, "Enviarme un correo de prueba" confirma la configuración; "Verificar" en cada paso de las reglas confirma que el grupo existe en AD y quiénes tienen correo.

## Avance

- [x] Fase 1: solución, BD, migración inicial con reglas de ejemplo, Windows Auth, grupos → roles, antiforgery
- [x] Fase 2: dominio y máquina de estados con pruebas
- [x] Fase 3: API de documentos y archivos (XML/PDF en disco, lectura de CFDI, UUID único, acciones del flujo)
- [x] Fase 4: shell de Angular (layout, sesión, guards, interceptor, Tailwind) y componentes compartidos (badge de estado, zona de carga, seguimiento, historial)
- [x] Fase 5: Mis documentos (filtros y paginación), nuevo/corregir con XML y PDF (carga con progreso), detalle con seguimiento, historial, vista previa del PDF y acciones aprobar/rechazar/procesar
- [x] Fase 6: bandejas "Por aprobar" (pendientes de mis grupos + resueltos por mí) y "Tesorería" (por procesar + procesados)
- [x] Fase 7: indicadores del inicio (6 tarjetas por alcance y periodo, trabajo pendiente por rol, últimos movimientos) enlazados a la lista filtrada; "Todos los documentos" para Admin
- [x] Fase 8: avisos por correo (cola con reintentos), administración de reglas (validación de huecos y traslapes, verificación de grupos en AD) y eliminación de borradores
- [ ] Fase 9: pruebas de integración y despliegue
