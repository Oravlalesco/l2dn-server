# Levanta el stack en modo desarrollo (DataPack/Config montados). Solo Docker en el PC.
param(
    [switch]$Build,
    [switch]$Code
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

if ($Build) {
    docker compose -f docker-compose.yml build
}

docker compose @compose up -d --force-recreate
Write-Host "Stack up (dev). GameServer: DataPack + Config desde el repo."
