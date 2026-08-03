# Reinicia el gameserver en modo desarrollo (DataPack/Config montados desde el repo).
param(
    [switch]$Code,
    [switch]$Migrate
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$files = @("docker-compose.yml")
if ($Code) {
    & "$PSScriptRoot/dev-publish.ps1"
    $files += "docker-compose.dev-code.yml"
}
$files += "docker-compose.dev.yml"

$compose = $files | ForEach-Object { "-f"; $_ }

if ($Migrate) {
    # Avoid racing live character/premium-item writes while applying schema/data migrations.
    docker compose @compose stop l2dn-gameserver
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    docker compose @compose run --rm l2dn-gameserver /App/L2Dn.GameServer -UpdateDatabase
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

docker compose @compose up -d --force-recreate l2dn-gameserver
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Gameserver restarted (dev mode)."
