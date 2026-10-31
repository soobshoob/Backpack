<#
.SYNOPSIS
  Собирает плагин Backpack и упаковывает его в .bppkg (zip: manifest.json, lib\*.dll без SDK, LICENSE.txt при наличии).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\pack-plugin.ps1 -Project lab\samples\PresentationTimer\PresentationTimer.csproj -Out artifacts\plugins
#>
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$Out = 'artifacts\plugins',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectPath = Resolve-Path $Project
$projectDir = Split-Path $projectPath
$build = Join-Path $projectDir "bin\pack"
if (Test-Path $build) { Remove-Item $build -Recurse -Force }
dotnet build $projectPath -c $Configuration -o $build -nologo -v q | Out-Host
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$manifest = Get-Content (Join-Path $projectDir 'manifest.json') -Raw | ConvertFrom-Json
$stage = Join-Path ([IO.Path]::GetTempPath()) ("bppkg-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force (Join-Path $stage 'lib') | Out-Null
Copy-Item (Join-Path $projectDir 'manifest.json') $stage
Get-ChildItem $build -Filter *.dll | Where-Object { $_.Name -ne 'Backpack.Sdk.dll' } | Copy-Item -Destination (Join-Path $stage 'lib')
Get-ChildItem $build -Filter *.deps.json | Where-Object { $_.Name -ne 'Backpack.Sdk.deps.json' } | Copy-Item -Destination (Join-Path $stage 'lib')
foreach ($r in @($manifest.resources)) { if ($r) { $dst = Join-Path $stage $r; New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null; Copy-Item (Join-Path $projectDir $r) $dst } }
$license = Join-Path $projectDir 'LICENSE.txt'
if (Test-Path $license) { Copy-Item $license $stage }

New-Item -ItemType Directory -Force $Out | Out-Null
$pkg = Join-Path (Resolve-Path $Out) ("{0}-{1}.bppkg" -f $manifest.id, $manifest.version)
if (Test-Path $pkg) { Remove-Item $pkg -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $pkg)
Remove-Item $stage -Recurse -Force
$hash = (Get-FileHash $pkg -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "package: $pkg"
Write-Host "sha256:  $hash"
