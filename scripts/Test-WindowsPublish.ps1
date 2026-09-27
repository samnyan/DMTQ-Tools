param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "Publish directory does not exist: $PublishDirectory"
}

$root = (Resolve-Path -LiteralPath $PublishDirectory).Path
$requiredFiles = @(
    'DMTQ-Tools.exe',
    'DMTQ-Tools.dll',
    'DMTQ-Tools.deps.json',
    'DMTQ-Tools.runtimeconfig.json',
    'DMTQ.Tools.Core.dll',
    'DMTQ.Tools.Components.dll',
    'Assets.Lib.dll',
    'AssetsTools.NET.dll',
    'AssetsTools.NET.Texture.dll',
    'AssetRipper.TextureDecoder.dll',
    'textureencoder.dll',
    'cuttlefish.dll',
    'PVRTexLib.dll',
    'wwwroot'
)

$missing = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
if ($missing.Count -gt 0) {
    throw "Required publish files are missing: $($missing -join ', ')"
}

$pdbFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.pdb')
if ($pdbFiles.Count -gt 0) {
    throw "Release publish contains $($pdbFiles.Count) PDB files: $($pdbFiles.Name -join ', ')"
}

$devFlowFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Name -match '(?i)DevFlow|chobitsu\.js'
})
if ($devFlowFiles.Count -gt 0) {
    throw "Release publish contains DevFlow files: $($devFlowFiles.Name -join ', ')"
}

$allowedCultures = @('en-US', 'zh-CN')
$cultureDirectories = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object {
    $_.Name -match '^[a-zA-Z]{2,3}(-[a-zA-Z0-9]+)*$'
})
$unexpectedCultures = @($cultureDirectories | Where-Object { $_.Name -notin $allowedCultures })
if ($unexpectedCultures.Count -gt 0) {
    throw "Unexpected culture directories: $($unexpectedCultures.Name -join ', ')"
}

$runtimeCandidates = @('hostfxr.dll', 'coreclr.dll', 'System.Private.CoreLib.dll')
$runtimeFiles = @($runtimeCandidates | Where-Object {
    Test-Path -LiteralPath (Join-Path $root $_)
})
if ($runtimeFiles.Count -gt 0) {
    throw "Framework-dependent publish unexpectedly contains .NET runtime files: $($runtimeFiles -join ', ')"
}

Write-Output "Windows publish validation passed: $root"
