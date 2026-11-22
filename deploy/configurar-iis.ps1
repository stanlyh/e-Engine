#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Crea o actualiza el sitio IIS de Aprobación de documentos (Windows Authentication, pool siempre activo).

.DESCRIPTION
    Requisitos en el servidor:
      - IIS con "Autenticación de Windows" (Agregar roles > Servidor web > Seguridad).
      - Hosting Bundle de .NET 10 (instala el ASP.NET Core Module V2).
    El script es idempotente: se puede volver a ejecutar para corregir la configuración.
    No configura HTTPS: asigna el certificado al binding desde el Administrador de IIS.

.EXAMPLE
    .\configurar-iis.ps1 -RutaSitio C:\inetpub\Aprobaciones -RutaArchivos E:\AprobacionesArchivos -NombreHost aprobaciones.empresa.local
#>
[CmdletBinding()]
param(
    [string]$Sitio = 'Aprobaciones',
    [string]$Pool = 'Aprobaciones',
    [Parameter(Mandatory)] [string]$RutaSitio,
    [Parameter(Mandatory)] [string]$RutaArchivos,
    [string]$NombreHost = '',
    [int]$Puerto = 80,
    # Cuenta de dominio para el pool (p. ej. EMPRESA\svc-aprobaciones). Vacío = ApplicationPoolIdentity.
    [string]$CuentaPool = ''
)

$ErrorActionPreference = 'Stop'

# --- Requisitos -------------------------------------------------------------
if (-not (Get-Service W3SVC -ErrorAction SilentlyContinue)) { throw 'IIS no está instalado en este servidor.' }
Import-Module WebAdministration
if (-not (Get-WebGlobalModule -Name WindowsAuthenticationModule -ErrorAction SilentlyContinue)) {
    throw 'Falta la característica "Autenticación de Windows" de IIS.'
}
if (-not (Get-WebGlobalModule -Name AspNetCoreModuleV2 -ErrorAction SilentlyContinue)) {
    throw 'Falta el ASP.NET Core Module V2: instala el Hosting Bundle de .NET 10 y ejecuta "iisreset".'
}

New-Item -ItemType Directory -Force $RutaSitio, $RutaArchivos | Out-Null

# --- Application Pool: sin CLR administrado y siempre activo ---------------------
# El envío de correos corre dentro del sitio: si IIS apaga el pool por inactividad, los avisos esperan en la cola.
if (-not (Test-Path "IIS:\AppPools\$Pool")) { New-WebAppPool -Name $Pool | Out-Null }
$rutaPool = "IIS:\AppPools\$Pool"
Set-ItemProperty $rutaPool -Name managedRuntimeVersion -Value ''
Set-ItemProperty $rutaPool -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $rutaPool -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
if ($CuentaPool) {
    $credencial = Get-Credential -UserName $CuentaPool -Message "Contraseña de $CuentaPool para el Application Pool"
    Set-ItemProperty $rutaPool -Name processModel -Value @{
        identityType = 'SpecificUser'; userName = $credencial.UserName; password = $credencial.GetNetworkCredential().Password
    }
    $identidad = $CuentaPool
}
else {
    Set-ItemProperty $rutaPool -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    $identidad = "IIS AppPool\$Pool"
}

# --- Sitio ---------------------------------------------------------------
if (-not (Test-Path "IIS:\Sites\$Sitio")) {
    New-Website -Name $Sitio -PhysicalPath $RutaSitio -ApplicationPool $Pool -Port $Puerto -HostHeader $NombreHost | Out-Null
}
else {
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name physicalPath -Value $RutaSitio
    Set-ItemProperty "IIS:\Sites\$Sitio" -Name applicationPool -Value $Pool
}
# Precarga: el sitio arranca con el pool, sin esperar la primera visita.
Set-ItemProperty "IIS:\Sites\$Sitio" -Name applicationDefaults.preloadEnabled -Value $true

# --- Autenticación: solo Windows -----------------------------------------------
# Se fija en applicationHost.config (estas secciones están bloqueadas para web.config por defecto).
$auth = '/system.webServer/security/authentication'
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $Sitio -Filter "$auth/anonymousAuthentication" -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $Sitio -Filter "$auth/windowsAuthentication" -Name enabled -Value $true

# --- Permisos de carpetas --------------------------------------------------------
& icacls $RutaSitio /grant "${identidad}:(OI)(CI)RX" /T /Q | Out-Null
& icacls $RutaArchivos /grant "${identidad}:(OI)(CI)M" /T /Q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "No se pudieron asignar permisos a $identidad." }

Write-Host "`nSitio '$Sitio' configurado. Identidad del pool: $identidad" -ForegroundColor Green
Write-Host @"
Siguientes pasos:
  1. SQL Server: crear el login de $identidad (si SQL está en otro servidor y se usa ApplicationPoolIdentity,
     el login es la cuenta de equipo DOMINIO\SERVIDOR`$) y ejecutar deploy\permisos-sql.sql.
  2. Aplicar migraciones.sql del paquete en la base (lo puede hacer el DBA).
  3. Copiar el contenido de 'sitio\' a $RutaSitio y crear appsettings.json a partir de appsettings.ejemplo.json.
  4. Si se usa HTTPS, asignar el certificado al binding.
  5. Abrir https://<sitio>/api/salud: debe responder "ok".
"@
