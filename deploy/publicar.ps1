<#
.SYNOPSIS
    Arma el paquete de despliegue: pruebas, build de Angular, publicación de la API y script SQL de migraciones.

.DESCRIPTION
    Resultado en artifacts\<version>\:
      sitio\                  contenido para la carpeta del sitio IIS (sin appsettings: la configuración vive en el servidor)
      migraciones.sql         script idempotente; se puede ejecutar en cualquier versión anterior de la base
      appsettings.ejemplo.json plantilla para el primer despliegue
      Aprobaciones-<version>.zip

.EXAMPLE
    .\deploy\publicar.ps1
    .\deploy\publicar.ps1 -SinPruebas
#>
[CmdletBinding()]
param(
    [string]$Salida = (Join-Path $PSScriptRoot '..\artifacts'),
    [switch]$SinPruebas
)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Ejecutar([string]$Descripcion, [scriptblock]$Comando) {
    Write-Host "==> $Descripcion" -ForegroundColor Cyan
    # npm y dotnet escriben advertencias en stderr; en PowerShell 5.1 con 'Stop' eso aborta el script
    # aunque el comando termine bien. Un comando externo se juzga solo por su código de salida.
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Comando 2>&1 | ForEach-Object { "$_" } }
    finally { $ErrorActionPreference = $anterior }
    if ($LASTEXITCODE -ne 0) { throw "Falló: $Descripcion (código $LASTEXITCODE)" }
}

Push-Location $raiz
try {
    $commit = (git rev-parse --short HEAD).Trim()
    $sucio = [bool](git status --porcelain)
    $version = (Get-Date -Format 'yyyy.MM.dd') + "-$commit" + $(if ($sucio) { '-con-cambios' } else { '' })
    if ($sucio) { Write-Warning 'Hay cambios sin commit: el paquete no corresponde exactamente a un commit.' }

    New-Item -ItemType Directory -Force $Salida | Out-Null
    $destino = Join-Path (Resolve-Path $Salida).Path $version
    if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
    New-Item -ItemType Directory -Force $destino | Out-Null

    if (-not $SinPruebas) {
        Ejecutar 'Pruebas (dominio, infraestructura e integración con LocalDB)' { dotnet test Aprobaciones.slnx -c Release --nologo -v q }
    }

    Push-Location web
    try {
        Ejecutar 'Dependencias de Angular' { npm ci --no-audit --no-fund }
        Ejecutar 'Build de Angular (sale a src\Aprobaciones.Api\wwwroot)' { npm run build }
    }
    finally { Pop-Location }

    Ejecutar 'Publicación de la API' {
        dotnet publish src\Aprobaciones.Api -c Release -o (Join-Path $destino 'sitio') --nologo -v q "-p:SourceRevisionId=$commit"
    }

    # Las herramientas de EF construyen el host para leer el modelo: necesitan una cadena de conexión,
    # pero 'migrations script' no se conecta a la base.
    $env:ConnectionStrings__Aprobaciones = 'Server=no-se-usa;Database=AprobacionDocumentos;Trusted_Connection=True'
    $env:Documentos__RutaArchivos = 'C:\no-se-usa'
    try {
        Ejecutar 'Script SQL de migraciones (idempotente)' {
            dotnet ef migrations script --idempotent -c AprobacionesDbContext -p src\Aprobaciones.Infrastructure -s src\Aprobaciones.Api `
                --configuration Release -o (Join-Path $destino 'migraciones.sql')
        }
    }
    finally {
        Remove-Item Env:ConnectionStrings__Aprobaciones, Env:Documentos__RutaArchivos -ErrorAction SilentlyContinue
    }

    Copy-Item deploy\appsettings.ejemplo.json $destino
    $colados = Get-ChildItem (Join-Path $destino 'sitio') -Filter 'appsettings*.json'
    if ($colados) { throw "El paquete no debe llevar configuración: $($colados.Name -join ', ')" }

    $zip = Join-Path $destino "Aprobaciones-$version.zip"
    Compress-Archive -Path (Join-Path $destino 'sitio'), (Join-Path $destino 'migraciones.sql'), (Join-Path $destino 'appsettings.ejemplo.json') -DestinationPath $zip
    Write-Host "`nPaquete listo: $zip" -ForegroundColor Green
}
finally { Pop-Location }
