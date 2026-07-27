param(
    [ValidateNotNullOrEmpty()]
    [string]$PublishAddress = "192.168.0.100",

    [switch]$ExposeDatabase
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$env:L2DN_PUBLISH_ADDRESS = $PublishAddress
$ComposeArguments = @("compose", "-f", "docker-compose.yml")
if ($ExposeDatabase)
{
    $ComposeArguments += @("-f", "docker-compose.database.yml")
}

$ComposeArguments += @("up", "-d", "--build")

Write-Host "Construyendo e iniciando servicios (postgres, auth, game)..." -ForegroundColor Cyan
& docker @ComposeArguments
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Esperando unos segundos..." -ForegroundColor Yellow
Start-Sleep -Seconds 8

Write-Host "Puertos en el host:" -ForegroundColor Cyan
netstat -an | findstr "LISTENING" | findstr ":2106 :7777"

Write-Host ""
Write-Host "Cliente L2: ServerAddr=$PublishAddress en L2.ini" -ForegroundColor Green
Write-Host "Login: puerto 2106 | Mundo: puerto 7777" -ForegroundColor Green
if ($ExposeDatabase)
{
    Write-Host "PostgreSQL: puerto 5432 expuesto en el host" -ForegroundColor Yellow
}
