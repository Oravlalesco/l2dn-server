# Publica el AuthServer con el SDK dentro de Docker (no hace falta .NET en el PC).
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RootDocker = ($Root -replace '\\', '/')

Write-Host "Publishing AuthServer via Docker SDK ..."
docker run --rm `
    -v "${RootDocker}:/src" `
    -w /src/L2Dn `
    mcr.microsoft.com/dotnet/sdk:9.0-alpine `
    sh -c "dotnet publish L2Dn.AuthServer/L2Dn.AuthServer.csproj -c Debug --no-self-contained -o /src/Docker/publish/authserver"

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$Out = Join-Path $PSScriptRoot "publish/authserver"
$config = Join-Path $Root "L2Dn/L2Dn.AuthServer/config.json"
Copy-Item -Force $config (Join-Path $Out "config.json")
# Misma sustitución que el Dockerfile de producción
(Get-Content (Join-Path $Out "config.json") -Raw) -replace '127\.0\.0\.1', '0.0.0.0' | Set-Content (Join-Path $Out "config.json") -NoNewline

Write-Host "Done. Recreate auth container with publish mount (ver docker-compose.dev-auth.yml) o:"
Write-Host "  docker compose build l2dn-authserver && docker compose up -d l2dn-authserver"
