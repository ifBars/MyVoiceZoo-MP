[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$GameDirectory = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'MVZ.MP.csproj'
$dll = Join-Path $repo 'bin\Release\net6.0\MVZ.MP.dll'
$readme = Join-Path $repo 'docs\INSTALL.md'

if (-not $SkipBuild) {
    $buildArgs = @('build', $project, '-c', 'Release')
    if ($GameDirectory) { $buildArgs += "-p:GameDir=$([IO.Path]::GetFullPath($GameDirectory))" }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }
}
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "Mod DLL missing: $dll" }
if (-not (Test-Path -LiteralPath $readme -PathType Leaf)) { throw "README missing: $readme" }

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
$package = Join-Path $output "MVZ-MP-$stamp-$suffix.zip"
$archive = [IO.Compression.ZipFile]::Open($package, [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $dll, 'MVZ.MP.dll') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $readme, 'README.md') | Out-Null
} finally { $archive.Dispose() }
$entries = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $names = @($entries.Entries | ForEach-Object FullName | Sort-Object)
    if (($names -join ',') -ne 'MVZ.MP.dll,README.md') { throw "Unexpected package contents: $($names -join ', ')" }
} finally { $entries.Dispose() }
Write-Output "Package: $package"
Write-Output "DLL SHA256: $((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash)"
