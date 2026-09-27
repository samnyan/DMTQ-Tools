param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,
    [string]$SummaryPath = $env:GITHUB_STEP_SUMMARY,
    [double]$MaximumSizeMiB = 0
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "Publish directory does not exist: $PublishDirectory"
}

$root = (Resolve-Path -LiteralPath $PublishDirectory).Path
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File)
$directories = @(Get-ChildItem -LiteralPath $root -Recurse -Directory)

function Get-FileBytes([scriptblock]$Predicate) {
    $matches = @($files | Where-Object $Predicate)
    $bytes = ($matches | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $bytes) { $bytes = 0 }
    return [pscustomobject]@{ Count = $matches.Count; Bytes = [long]$bytes }
}

$pdb = Get-FileBytes { $_.Extension -ieq '.pdb' }
$dll = Get-FileBytes { $_.Extension -ieq '.dll' }
$windowsAppSdk = Get-FileBytes {
    $_.Name -match '^(Microsoft\.WindowsAppRuntime|Microsoft\.ui\.|Microsoft\.UI\.|Microsoft\.WinUI|WinUI|DWriteCore|CoreMessaging|MRM|dwmcorei|dcompi)'
}
$windowsMl = Get-FileBytes {
    $_.Name -match '(?i)(DirectML|onnxruntime|Microsoft\.Windows\.AI|NpuDetect|workloads.*\.json)'
}
$fluentIcons = Get-FileBytes { $_.Name -match '^Microsoft\.FluentUI\.AspNetCore\.Components\.Icons\..*\.dll$' }
$wwwrootFiles = Get-FileBytes { $_.FullName.StartsWith((Join-Path $root 'wwwroot') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) }
$nativeTexture = Get-FileBytes { $_.Name -match '^(textureencoder|cuttlefish|PVRTexLib)\.dll$' }
$totalBytes = [long](($files | Measure-Object -Property Length -Sum).Sum)
$totalMiB = $totalBytes / 1MB

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('## Windows publish measurements')
$lines.Add('')
$lines.Add("- Directory: ``$root``")
$lines.Add("- Total: $($files.Count) files, $($directories.Count) directories, $([math]::Round($totalMiB, 2)) MiB")
if ($MaximumSizeMiB -gt 0) {
    $lines.Add("- Size limit: $MaximumSizeMiB MiB")
}
$lines.Add("- DLLs: $($dll.Count) files, $([math]::Round($dll.Bytes / 1MB, 2)) MiB")
$lines.Add("- PDBs: $($pdb.Count) files, $([math]::Round($pdb.Bytes / 1MB, 2)) MiB")
$lines.Add("- Windows App SDK named files: $($windowsAppSdk.Count) files, $([math]::Round($windowsAppSdk.Bytes / 1MB, 2)) MiB")
$lines.Add("- Windows ML / ONNX named files: $($windowsMl.Count) files, $([math]::Round($windowsMl.Bytes / 1MB, 2)) MiB")
$lines.Add("- Fluent UI icon assemblies: $($fluentIcons.Count) files, $([math]::Round($fluentIcons.Bytes / 1MB, 2)) MiB")
$lines.Add("- wwwroot: $($wwwrootFiles.Count) files, $([math]::Round($wwwrootFiles.Bytes / 1MB, 2)) MiB")
$lines.Add("- Texture encoder native DLLs: $($nativeTexture.Count) files, $([math]::Round($nativeTexture.Bytes / 1MB, 2)) MiB")
$lines.Add('')
$lines.Add('### Culture directories')
$cultureDirectories = @($directories | Where-Object {
    $_.Parent.FullName -eq $root -and $_.Name -match '^[a-zA-Z]{2,3}(-[a-zA-Z0-9]+)*$'
})
if ($cultureDirectories.Count -eq 0) {
    $lines.Add('- None')
} else {
    foreach ($cultureDirectory in ($cultureDirectories | Sort-Object Name)) {
        $cultureFiles = @(Get-ChildItem -LiteralPath $cultureDirectory.FullName -Recurse -File)
        $cultureBytes = [long](($cultureFiles | Measure-Object -Property Length -Sum).Sum)
        $lines.Add("- $($cultureDirectory.Name): $($cultureFiles.Count) files, $([math]::Round($cultureBytes / 1MB, 3)) MiB")
    }
}
$lines.Add('')
$lines.Add('### Largest files')
$lines.Add('')
$lines.Add('| MiB | File |')
$lines.Add('| ---: | --- |')
foreach ($file in ($files | Sort-Object Length -Descending | Select-Object -First 30)) {
    $relativePath = $file.FullName.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'
    $lines.Add("| $([math]::Round($file.Length / 1MB, 2)) | ``$relativePath`` |")
}

$report = $lines -join [Environment]::NewLine
Write-Output $report
if (-not [string]::IsNullOrWhiteSpace($SummaryPath)) {
    Add-Content -LiteralPath $SummaryPath -Value $report
}

if ($MaximumSizeMiB -gt 0 -and $totalMiB -gt $MaximumSizeMiB) {
    throw "Publish size $([math]::Round($totalMiB, 2)) MiB exceeds the $MaximumSizeMiB MiB limit."
}
