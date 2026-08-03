[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$downloadRoot = Join-Path $PSScriptRoot 'downloads'

function Get-VerifiedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        [Parameter(Mandatory = $true)]
        [string]$Sha256
    )

    $parent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    if (-not (Test-Path -LiteralPath $Destination)) {
        $temporary = "$Destination.download"
        Invoke-WebRequest -Uri $Uri -OutFile $temporary
        Move-Item -LiteralPath $temporary -Destination $Destination
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Destination).Hash
    if ($actualHash -ne $Sha256) {
        throw "SHA-256 invalido para $Destination. Esperado: $Sha256. Obtenido: $actualHash"
    }

    Write-Host "OK $actualHash $Destination" -ForegroundColor Green
}

$openRoot = Join-Path $downloadRoot 'open-l2encdec\1.3.9'
$openBinaryZip = Join-Path $openRoot 'l2encdec_windows.zip'
$openSourceZip = Join-Path $openRoot 'open-l2encdec-1.3.9-source.zip'
Get-VerifiedFile `
    -Uri 'https://github.com/ritsuwastaken/open-l2encdec/releases/download/1.3.9/l2encdec_windows.zip' `
    -Destination $openBinaryZip `
    -Sha256 '3A7743C03A635DBBF7892C4F3D65D0D13D5FC04830721540AD9E430C7BD0495E'
Get-VerifiedFile `
    -Uri 'https://github.com/ritsuwastaken/open-l2encdec/archive/refs/tags/1.3.9.zip' `
    -Destination $openSourceZip `
    -Sha256 '4B2359EE64BA97BDAE04AE37F8F939C7ED9F61C03DBE1E710E8AFAA36E209513'

$openWindows = Join-Path $openRoot 'windows'
$openSource = Join-Path $openRoot 'source'
if (-not (Test-Path -LiteralPath (Join-Path $openWindows 'l2encdec_win.exe'))) {
    Expand-Archive -LiteralPath $openBinaryZip -DestinationPath $openWindows
}
if (-not (Test-Path -LiteralPath (Join-Path $openSource 'open-l2encdec-1.3.9'))) {
    Expand-Archive -LiteralPath $openSourceZip -DestinationPath $openSource
}

$mobiusRoot = Join-Path $downloadRoot 'L2ClientDat'
$mobiusSourceZip = Join-Path $mobiusRoot 'L2ClientDat-fa94655-source.zip'
Get-VerifiedFile `
    -Uri 'https://github.com/MobiusDevelopment/L2ClientDat/archive/fa94655ad19fdecfc9611c52dc7c67ffd416a1a8.zip' `
    -Destination $mobiusSourceZip `
    -Sha256 'A841E7342AE8D6F0ADB49389ACBD6304484477E4D7F4EF45772AD67BFC66D90C'

$mobiusSource = Join-Path $mobiusRoot 'source-fa94655'
if (-not (Test-Path -LiteralPath $mobiusSource)) {
    Expand-Archive -LiteralPath $mobiusSourceZip -DestinationPath $mobiusSource
}

Write-Host "Ejecutable: $(Join-Path $openWindows 'l2encdec_win.exe')"
Write-Host "Fuentes open-l2encdec: $openSource"
Write-Host "Fuentes L2ClientDat: $mobiusSource"
