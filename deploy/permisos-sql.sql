/*
  Permisos mínimos de la cuenta del sitio en la base de producción (ejecutar como administrador de SQL Server).
  La aplicación lee y escribe datos; no crea ni cambia tablas: las migraciones las aplica un administrador
  con migraciones.sql. Cambia @cuenta (en los dos lotes) por la identidad del Application Pool:
    - ApplicationPoolIdentity con SQL en el mismo servidor:  IIS APPPOOL\Aprobaciones
    - ApplicationPoolIdentity con SQL en otro servidor:      EMPRESA\SERVIDOR-WEB$ (cuenta de equipo)
    - Cuenta de servicio:                                    EMPRESA\svc-aprobaciones
  Se puede ejecutar varias veces.
*/
DECLARE @cuenta sysname = N'EMPRESA\svc-aprobaciones';
DECLARE @sql nvarchar(max);
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @cuenta)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@cuenta) + N' FROM WINDOWS';
    EXEC sp_executesql @sql;
END
GO

USE AprobacionDocumentos;
GO

DECLARE @cuenta sysname = N'EMPRESA\svc-aprobaciones';
DECLARE @sql nvarchar(max);
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @cuenta)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@cuenta) + N' FOR LOGIN ' + QUOTENAME(@cuenta);
    EXEC sp_executesql @sql;
END
-- Verificado con un usuario restringido: bastan estos dos roles. El folio (secuencia DocumentoNumeros) se toma
-- desde el valor por defecto de la columna y no requiere permiso propio; /api/salud lee __EFMigrationsHistory
-- con db_datareader.
SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@cuenta);
EXEC sp_executesql @sql;
SET @sql = N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@cuenta);
EXEC sp_executesql @sql;
GO
