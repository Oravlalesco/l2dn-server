[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientSystemPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$clientSystem = (Resolve-Path -LiteralPath $ClientSystemPath).Path
$dsetup = Join-Path $clientSystem 'DSETUP.dll'
$requiredNames = @(
    'eu\PurchaseLimitCraft_Classic-eu.dat',
    'eu\PurchaseLimitCraft_ClassicAden-eu.dat',
    'eu\NpcString_Classic-eu.dat',
    'eu\L2GameDataName.dat',
    'eu\ItemName_Classic-eu.dat',
    'eu\ItemName_ClassicAden-eu.dat',
    'eu\EtcItemgrp_Classic.dat',
    'eu\EtcItemgrp_ClassicAden.dat',
    'eu\Armorgrp_Classic.dat',
    'eu\Armorgrp_ClassicAden.dat',
    'eu\Weapongrp_Classic.dat',
    'eu\Weapongrp_ClassicAden.dat',
    'eu\item_baseinfo_Classic.dat',
    'eu\item_baseinfo_ClassicAden.dat',
    'eu\AdditionalItemGrp_Classic.dat',
    'eu\AdditionalItemGrp_ClassicAden.dat',
    'eu\ItemStatData_Classic.dat',
    'eu\ItemStatData_ClassicAden.dat',
    'DSETUP.dll'
)
$requiredFiles = $requiredNames | ForEach-Object { Join-Path $clientSystem $_ }
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Falta el archivo requerido: $requiredFile"
    }
}

$l2EncDecModulus = '75b4d6de5c016544068a1acf125869f43d2e09fc55b8b1e289556daf9b8757635593446288b3653da1ce91c87bb1a5c18f16323495c55d7d72c0890a83f69bfd1fd9434eb1c02f3e4679edfa43309319070129c267c85604d87bb65bae205de3707af1d2108881abb567c3b3d069ae67c3a4c6a3aa93d26413d4c66094ae2039'
$dsetupText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dsetup))
if (-not $dsetupText.Contains($l2EncDecModulus)) {
    throw 'DSETUP.dll no contiene la clave RSA l2encdec. Este cliente no aceptaria el DAT generado.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker no esta disponible en PATH.'
}

$outputDirectory = Join-Path $PSScriptRoot 'output'
$nugetDirectory = Join-Path $PSScriptRoot 'downloads\nuget'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $nugetDirectory -Force | Out-Null

& docker run --rm `
    --mount "type=bind,source=$repoRoot,target=/src" `
    --mount "type=bind,source=$clientSystem,target=/client,readonly" `
    --mount "type=bind,source=$nugetDirectory,target=/root/.nuget/packages" `
    --workdir /src `
    mcr.microsoft.com/dotnet/sdk:9.0 `
    dotnet run -p:NuGetAudit=false `
    --project L2Dn/Tools/L2Dn.ClientDat/L2Dn.ClientDat.csproj `
    -- build-special-craft-bundle `
    /client `
    /src/L2Dn/L2Dn.GameServer/DataPack/LimitShopCraft.xml `
    /src/tools/client/output

if ($LASTEXITCODE -ne 0) {
    throw "La generacion del DAT fallo con codigo $LASTEXITCODE."
}

$outputFiles = @(
    'L2GameDataName.dat',
    'PurchaseLimitCraft_Classic-eu.dat',
    'ItemName_Classic-eu.dat',
    'EtcItemgrp_Classic.dat',
    'Armorgrp_Classic.dat',
    'Weapongrp_Classic.dat',
    'item_baseinfo_Classic.dat',
    'AdditionalItemGrp_Classic.dat',
    'ItemStatData_Classic.dat'
)
Write-Host 'Paquete DAT generado y verificado:' -ForegroundColor Green
foreach ($fileName in $outputFiles) {
    $outputFile = Join-Path $outputDirectory $fileName
    $outputHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputFile).Hash
    Write-Host "  $fileName  $outputHash"
}
Write-Host 'El cliente instalado no fue modificado.'
