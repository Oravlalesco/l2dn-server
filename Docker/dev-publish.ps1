# Publica el GameServer con el SDK dentro de Docker (no hace falta .NET en el PC).
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RootDocker = ($Root -replace '\\', '/')
$Out = Join-Path $PSScriptRoot "publish/gameserver"
$OutDocker = ($Out -replace '\\', '/')
$DockerConfig = Join-Path $PSScriptRoot "config.gameserver.docker.json"

Write-Host "Publishing GameServer via Docker SDK to $Out ..."
docker run --rm `
    -v "${RootDocker}:/src" `
    -w /src/L2Dn `
    mcr.microsoft.com/dotnet/sdk:9.0-alpine `
    sh -c "dotnet publish L2Dn.GameServer/L2Dn.GameServer.csproj -c Debug --no-self-contained -o /src/Docker/publish/gameserver"

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -Force $DockerConfig (Join-Path $Out "config.json")
Write-Host "Done. Restart with:"
Write-Host "  .\dev-restart.ps1 -Code"
