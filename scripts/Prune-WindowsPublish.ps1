param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "Publish directory does not exist: $PublishDirectory"
}

$root = (Resolve-Path -LiteralPath $PublishDirectory).Path
$symbols = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.pdb')
foreach ($symbol in $symbols) {
    Remove-Item -LiteralPath $symbol.FullName -Force
}

$allowedCultures = @('en-US', 'zh-CN')
$cultureDirectories = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object {
    $_.Name -match '^[a-zA-Z]{2,3}(-[a-zA-Z0-9]+)*$' -and $_.Name -notin $allowedCultures
})
foreach ($cultureDirectory in $cultureDirectories) {
    Remove-Item -LiteralPath $cultureDirectory.FullName -Recurse -Force
}

Write-Output "Removed $($symbols.Count) PDB files and $($cultureDirectories.Count) unexpected culture directories from $root"
