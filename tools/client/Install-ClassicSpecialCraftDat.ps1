[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientSystemPath,
    [string]$PatchPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($PatchPath)) {
    $PatchPath = Join-Path $PSScriptRoot 'output\PurchaseLimitCraft_Classic-eu.dat'
}

$clientSystem = (Resolve-Path -LiteralPath $ClientSystemPath).Path
$patch = (Resolve-Path -LiteralPath $PatchPath).Path
$destination = Join-Path $clientSystem 'eu\PurchaseLimitCraft_Classic-eu.dat'
if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
    throw "No existe el DAT original: $destination"
}

$bytes = [IO.File]::ReadAllBytes($patch)
if ($bytes.Length -lt 28 -or [Text.Encoding]::Unicode.GetString($bytes, 0, 28) -ne 'Lineage2Ver413') {
    throw "El parche no es un DAT Lineage2Ver413 valido: $patch"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$destination.backup-$timestamp"
Copy-Item -LiteralPath $destination -Destination $backup
Copy-Item -LiteralPath $patch -Destination $destination

$patchHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $patch).Hash
$installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
if ($patchHash -ne $installedHash) {
    throw "La copia no coincide. El original esta respaldado en: $backup"
}

Write-Host "DAT instalado: $destination" -ForegroundColor Green
Write-Host "Respaldo recuperable: $backup"
Write-Host "SHA-256: $installedHash"
