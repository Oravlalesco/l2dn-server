[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientSystemPath,
    [string]$PatchDirectory
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($PatchDirectory)) {
    $PatchDirectory = Join-Path $PSScriptRoot 'output'
}

$clientSystem = (Resolve-Path -LiteralPath $ClientSystemPath).Path
$patchRoot = (Resolve-Path -LiteralPath $PatchDirectory).Path
$fileNames = @(
    'PurchaseLimitCraft_Classic-eu.dat',
    'ItemName_Classic-eu.dat',
    'EtcItemgrp_Classic.dat',
    'Armorgrp_Classic.dat',
    'Weapongrp_Classic.dat'
)

$operations = foreach ($fileName in $fileNames) {
    $patch = Join-Path $patchRoot $fileName
    $destination = Join-Path $clientSystem "eu\$fileName"
    if (-not (Test-Path -LiteralPath $patch -PathType Leaf)) {
        throw "Falta el DAT generado: $patch"
    }
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "No existe el DAT original: $destination"
    }

    $bytes = [IO.File]::ReadAllBytes($patch)
    if ($bytes.Length -lt 28 -or [Text.Encoding]::Unicode.GetString($bytes, 0, 28) -ne 'Lineage2Ver413') {
        throw "El parche no es un DAT Lineage2Ver413 valido: $patch"
    }

    [pscustomobject]@{
        Name = $fileName
        Patch = $patch
        Destination = $destination
    }
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backups = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($operation in $operations) {
        $backup = "$($operation.Destination).backup-$timestamp"
        Copy-Item -LiteralPath $operation.Destination -Destination $backup
        $backups.Add([pscustomobject]@{ Source = $backup; Destination = $operation.Destination })
    }

    foreach ($operation in $operations) {
        Copy-Item -LiteralPath $operation.Patch -Destination $operation.Destination
        $patchHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $operation.Patch).Hash
        $installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $operation.Destination).Hash
        if ($patchHash -ne $installedHash) {
            throw "La copia no coincide para $($operation.Name)."
        }
    }
}
catch {
    foreach ($backup in $backups) {
        Copy-Item -LiteralPath $backup.Source -Destination $backup.Destination
    }
    throw "La instalacion fallo y se restauraron los DAT originales. $($_.Exception.Message)"
}

Write-Host 'Paquete Special Craft instalado:' -ForegroundColor Green
foreach ($operation in $operations) {
    $installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $operation.Destination).Hash
    Write-Host "  $($operation.Name)  $installedHash"
}
Write-Host "Respaldos recuperables: *.backup-$timestamp"
