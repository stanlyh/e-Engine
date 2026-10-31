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
| `RfcEmpresa` | Si se configura, el RFC receptor del CFDI debe coincidir |
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

Las escrituras exigen el encabezado `X-XSRF-TOKEN`.

## Desarrollo

1. Cadena de conexión en `src/Aprobaciones.Api/appsettings.Development.json` (`ConnectionStrings:Aprobaciones`).
2. Crear la base:
   ```
   dotnet ef database update -p src/Aprobaciones.Infrastructure -s src/Aprobaciones.Api
   ```
3. API: `dotnet run --project src/Aprobaciones.Api --launch-profile http` (http://localhost:5214)
4. Angular: `cd web && npm start` (http://localhost:4200). Llama a la API con credenciales de Windows vía CORS.
5. Pruebas: `dotnet test` (dominio + lector CFDI + almacén en disco)
6. Con `sqlcmd` usa `-I`: los índices filtrados requieren `QUOTED_IDENTIFIER ON`.

## Producción (IIS)

1. `cd web && npm run build` (genera `src/Aprobaciones.Api/wwwroot`)
2. `dotnet publish src/Aprobaciones.Api -c Release`
3. En el sitio IIS: **Windows Authentication habilitado** y **Anonymous deshabilitado**.
4. La cuenta del Application Pool necesita acceso a SQL Server y permiso de escritura en `Documentos:RutaArchivos`.

## Avance

- [x] Fase 1: solución, BD, migración inicial con reglas de ejemplo, Windows Auth, grupos → roles, antiforgery
- [x] Fase 2: dominio y máquina de estados con pruebas
- [x] Fase 3: API de documentos y archivos (XML/PDF en disco, lectura de CFDI, UUID único, acciones del flujo)
- [~] Fase 4: shell de Angular (layout, sesión, guards, interceptor, Tailwind) listo; faltan componentes compartidos
- [ ] Fase 5: Mis documentos, nuevo/editar con 2 archivos, detalle con seguimiento
- [ ] Fase 6: bandejas de aprobación y Tesorería
- [ ] Fase 7: indicadores del dashboard
- [ ] Fase 8: notificaciones por correo y administración de reglas
- [ ] Fase 9: pruebas de integración y despliegue
