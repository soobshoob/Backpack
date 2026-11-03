<#
.SYNOPSIS
  Установщик Backpack (Inno Setup 6) из готового и проверенного portable-пакета.

.DESCRIPTION
  1. распаковывает Backpack-<версия>-win-x64.zip (результат tools/package-release.ps1) во временную папку;
  2. находит ISCC.exe (Inno Setup 6); с -InstallInnoSetup ставит его через Chocolatey, если его нет (на GitHub Actions);
  3. собирает Backpack-<версия>-win-x64-setup.exe по tools/installer/Backpack.iss и пишет рядом .sha256;
  4. с -Verify проверяет установщик по-настоящему: тихая установка в отдельную папку для текущего пользователя, состав
     (Backpack.exe, .NET runtime, Plugins\bundled.json и пакеты плагинов от создателя), тихое удаление, папка пуста.
     Программа при этом не запускается, настройки пользователя (%APPDATA%\Backpack) не затрагиваются.

.EXAMPLE
  pwsh tools/build-installer.ps1 -Zip artifacts/release/Backpack-0.4.0-preview.8-win-x64.zip -Verify
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Zip,
    [string]$Output = (Split-Path $Zip),
    [switch]$InstallInnoSetup,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot
$Zip = (Resolve-Path $Zip).Path
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null

if ((Split-Path $Zip -Leaf) -notmatch '^Backpack-(?<v>\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?)-win-x64\.zip$') { throw "Unexpected package name: $Zip" }
$version = $Matches.v
# VersionInfo требует четыре числа: 0.4.0-preview.8 → 0.4.0.8, 1.2.3 → 1.2.3.0.
$core = $version.Split('-')[0]
$build = if ($version -match '-[A-Za-z.]*?(\d+)$') { $Matches[1] } else { '0' }
$numeric = "$core.$build"

function Find-Iscc {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc -and $InstallInnoSetup) {
    Write-Host 'Inno Setup 6 not found: installing it with Chocolatey'
    choco install innosetup --no-progress -y | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'choco install innosetup failed' }
    $iscc = Find-Iscc
}
if (-not $iscc) { throw 'ISCC.exe (Inno Setup 6) not found. Install Inno Setup 6 or pass -InstallInnoSetup.' }
Write-Host "ISCC: $iscc"

$work = Join-Path ([IO.Path]::GetTempPath()) ('backpack-installer-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$source = Join-Path $work 'package'
try {
    Expand-Archive $Zip -DestinationPath $source
    foreach ($required in 'Backpack.exe', 'LICENSE.txt', 'Plugins\bundled.json') {
        if (-not (Test-Path (Join-Path $source $required))) { throw "The package has no $required" }
    }
    if (Test-Path (Join-Path $source 'Backpack.portable')) { throw 'The package must not contain Backpack.portable' }

    & $iscc /Qp "/DAppVersion=$version" "/DAppVersionNumeric=$numeric" "/DSourceDir=$source" "/DOutputDir=$Output" "/DRepoRoot=$repo" (Join-Path $repo 'tools\installer\Backpack.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }

    $setup = Join-Path $Output "Backpack-$version-win-x64-setup.exe"
    if (-not (Test-Path $setup)) { throw "Installer not produced: $setup" }
    $size = (Get-Item $setup).Length
    if ($size -le 1MB) { throw "Installer is suspiciously small: $size bytes" }
    $hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $setup -Leaf)" | Set-Content "$setup.sha256" -Encoding ascii -NoNewline
    Write-Host ("Installer: {0} ({1:N1} MB), SHA-256 {2}" -f $setup, ($size / 1MB), $hash)

    if ($Verify) {
        $target = Join-Path $work 'installed Backpack'
        $p = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', "/DIR=`"$target`"", "/LOG=`"$work\install.log`"" -Wait -PassThru
        if ($p.ExitCode -ne 0) { Get-Content "$work\install.log" -ErrorAction SilentlyContinue | Select-Object -Last 40 | Out-Host; throw "Silent install failed: exit code $($p.ExitCode)" }
        $expected = @('Backpack.exe', 'Backpack.Sdk.dll', 'hostfxr.dll', 'coreclr.dll', 'LICENSE.txt', 'Plugins\bundled.json') +
            @(Get-ChildItem (Join-Path $source 'Plugins') -Filter *.bppkg | ForEach-Object { "Plugins\$($_.Name)" })
        foreach ($f in $expected) { if (-not (Test-Path (Join-Path $target $f))) { throw "Installed copy has no $f" } }
        foreach ($plugin in 'focus', 'lens', 'snapshot-overlay') {
            if (-not (Get-ChildItem (Join-Path $target 'Plugins') -Filter "com.example.$plugin-*.bppkg")) { throw "Installed copy has no bundled $plugin plugin" }
        }
        $installed = @(Get-ChildItem $target -Recurse -File | Where-Object { $_.Name -notlike 'unins*' }).Count
        $packaged = @(Get-ChildItem $source -Recurse -File).Count
        if ($installed -ne $packaged) { throw "Installed $installed files, package has $packaged" }
        Write-Host "PASS  silent install: $installed files, bundled plugins present"

        $uninstaller = Join-Path $target 'unins000.exe'
        Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait | Out-Null
        # Деинсталлятор Inno Setup перезапускает себя из временной папки — дождаться удаления файлов.
        $deadline = (Get-Date).AddSeconds(90)
        while ((Test-Path (Join-Path $target 'Backpack.exe')) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
        if (Test-Path (Join-Path $target 'Backpack.exe')) { throw 'Silent uninstall did not remove Backpack.exe' }
        Write-Host 'PASS  silent uninstall'
    }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
