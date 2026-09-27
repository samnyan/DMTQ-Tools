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

Write-Output "Removed $($symbols.Count) PDB files from $root"
