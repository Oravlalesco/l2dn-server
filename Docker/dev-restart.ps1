# Reinicia el gameserver en modo desarrollo (DataPack/Config montados desde el repo).
param(
    [switch]$Code,
    [switch]$Migrate
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$files = @("docker-compose.yml")
if ($Code) {
    if (-not (Test-Path "publish/gameserver/L2Dn.GameServer.dll")) {
        & "$PSScriptRoot/dev-publish.ps1"
    }
    $files += "docker-compose.dev-code.yml"
}
$files += "docker-compose.dev.yml"

$compose = $files | ForEach-Object { "-f"; $_ }

if ($Migrate) {
    docker compose @compose run --rm l2dn-gameserver /App/L2Dn.GameServer -UpdateDatabase
}

docker compose @compose restart l2dn-gameserver
Write-Host "Gameserver restarted (dev mode)."
