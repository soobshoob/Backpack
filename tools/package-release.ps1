<#
.SYNOPSIS
  Воспроизводимая упаковка Backpack для Windows x64 (portable, self-contained).

.DESCRIPTION
  1. (необязательно) тесты решения;
  2. dotnet publish Release win-x64 --self-contained: программа работает без установленного .NET SDK и отдельного runtime;
  3. папка пакета: программа, Licenses\ (Inter, Spectral, .NET), README.txt (запуск, обновление, удаление), RELEASE-NOTES.md,
     THIRD-PARTY-NOTICES.md, LICENSE-STATUS.md, LICENSE.txt (ядро — MPL 2.0), NOTICE.txt, Licenses\Backpack.Sdk-LICENSE.txt и
     Samples\LICENSE.txt (PolyForm Strict 1.0.0); без .pdb, экспортов диагностики, references\, research\ и временных файлов;
  4. zip и SHA256SUMS.txt (контрольные суммы zip и всех файлов пакета).

  Дата выпуска задаётся явно (-ReleaseDate), не берётся из часов сборки. Репозиторий обновлений задаёт владелец
  (-UpdateRepository владелец/репозиторий); без него программа честно показывает «источник обновлений не настроен».
  Пакет не подписывается: подписи нет, пока владелец не предоставит сертификат.

.EXAMPLE
  pwsh tools/package-release.ps1 -ReleaseDate 2026-10-29
  pwsh tools/package-release.ps1 -ReleaseDate 2026-11-05 -UpdateRepository soobshoob/KeyOverlay -SkipTests
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d{4}-\d{2}-\d{2}$')][string]$ReleaseDate,
    [ValidatePattern('^$|^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$UpdateRepository = '',
    [ValidateSet('Stable', 'Preview')][string]$Channel = 'Preview',
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\release'),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'lab\src\KeyOverlay.Lab.App\KeyOverlay.Lab.App.csproj'
$props = [xml](Get-Content (Join-Path $repo 'lab\Directory.Build.props') -Raw)
$prefix = $props.Project.PropertyGroup.VersionPrefix
$suffix = $props.Project.PropertyGroup.VersionSuffix
$version = if ($suffix) { "$prefix-$suffix" } else { $prefix }
if ($Channel -eq 'Stable' -and $suffix) { throw "Канал Stable не допускает предварительную версию ($version)." }

$name = "Backpack-$version-win-x64"
$Output = [IO.Path]::GetFullPath($Output)
$stage = Join-Path $Output $name
$zip = Join-Path $Output "$name.zip"
Write-Host "Backpack $version ($Channel), дата выпуска $ReleaseDate, обновления: $(if ($UpdateRepository) { $UpdateRepository } else { 'не настроены' })"

if (-not $SkipTests) {
    dotnet test (Join-Path $repo 'lab\KeyOverlay.Lab.slnx') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Тесты не прошли — пакет не собирается.' }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true -o $stage `
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true `
    -p:SatelliteResourceLanguages=en `
    "-p:KeyOverlayReleaseDate=$ReleaseDate" "-p:KeyOverlayChannel=$Channel" "-p:KeyOverlayUpdateRepository=$UpdateRepository"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish завершился с ошибкой.' }

# Ничего лишнего: символы отладки, временные файлы, экспорт диагностики.
Get-ChildItem $stage -Recurse -Include *.pdb, *.tmp, *.keyoverlay-lab.json, keyoverlay-lab-*.json | Remove-Item -Force

# Лицензии сторонних компонентов.
$licenses = Join-Path $stage 'Licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null
$dotnetRoot = Split-Path (Get-Command dotnet).Source
Copy-Item (Join-Path $dotnetRoot 'LICENSE.txt') (Join-Path $licenses 'dotnet-LICENSE.txt')
Copy-Item (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') (Join-Path $licenses 'dotnet-ThirdPartyNotices.txt')
foreach ($font in 'Inter', 'Spectral') {
    if (-not (Test-Path (Join-Path $licenses "$font-LICENSE.txt"))) {
        Copy-Item (Join-Path $repo "lab\src\KeyOverlay.Visual\Fonts\$font-LICENSE.txt") (Join-Path $licenses "$font-LICENSE.txt")
    }
}

$release = Join-Path $repo 'docs\release'
Copy-Item (Join-Path $release "RELEASE-NOTES-$version.md") (Join-Path $stage 'RELEASE-NOTES.md')
Copy-Item (Join-Path $release 'THIRD-PARTY-NOTICES.md') (Join-Path $stage 'THIRD-PARTY-NOTICES.md')
Copy-Item (Join-Path $release 'LICENSE-STATUS.md') (Join-Path $stage 'LICENSE-STATUS.md')
# Лицензия ядра (Phase 4I: MPL 2.0, официальный текст без изменений) и уведомление об авторских правах.
Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $stage 'LICENSE.txt')
Copy-Item (Join-Path $repo 'NOTICE') (Join-Path $stage 'NOTICE.txt')
# SDK и плагины от создателя — отдельные условия (до решения владельца PolyForm Strict 1.0.0): корневая MPL их не покрывает.
Copy-Item (Join-Path $repo 'lab\sdk\LICENSE') (Join-Path $licenses 'Backpack.Sdk-LICENSE.txt')
Copy-Item (Join-Path $repo 'lab\samples\LICENSE') (Join-Path $licenses 'PolyForm-Strict-1.0.0.txt')
# Phase 4G/4H: шесть плагинов от создателя. Plugins\ — готовые пакеты .bppkg и индекс поставки bundled.json (SHA-256:
# при запуске Backpack ставит их в каталог выключенными, код не запускается; по индексу — отметка «От создателя»),
# Samples\ — исходники с инструкцией. В исходниках ссылка на проект SDK заменяется
# ссылкой на Backpack.Sdk.dll рядом с Backpack.exe, чтобы пример собирался прямо из папки пакета.
$plugins = Join-Path $stage 'Plugins'
$samples = Join-Path $stage 'Samples'
New-Item -ItemType Directory -Force $plugins, $samples | Out-Null
Copy-Item (Join-Path $repo 'lab\samples\LICENSE') (Join-Path $samples 'LICENSE.txt')
foreach ($sample in 'Drawing', 'LayoutIndicator', 'CursorZoom', 'Lens', 'Focus', 'PresentationTimer') {
    $src = Join-Path $repo "lab\samples\$sample"
    & (Join-Path $repo 'tools\pack-plugin.ps1') -Project (Join-Path $src "$sample.csproj") -Out $plugins | Out-Host
    $dst = Join-Path $samples $sample
    New-Item -ItemType Directory -Force $dst | Out-Null
    Get-ChildItem $src -File | Where-Object { $_.Extension -in '.cs', '.json', '.txt', '.png' } | Copy-Item -Destination $dst
    $csproj = Get-Content (Join-Path $src "$sample.csproj") -Raw
    $sdkRef = '<ProjectReference Include="..\..\sdk\Backpack.Sdk\Backpack.Sdk.csproj" Private="false" ExcludeAssets="runtime" />'
    if (-not $csproj.Contains($sdkRef)) { throw "$sample.csproj: ссылка на SDK не найдена" }
    $csproj.Replace($sdkRef, '<Reference Include="Backpack.Sdk"><HintPath>..\..\Backpack.Sdk.dll</HintPath><Private>false</Private></Reference>') |
        Set-Content (Join-Path $dst "$sample.csproj") -Encoding utf8
}
if (@(Get-ChildItem $plugins -Filter *.bppkg).Count -ne 6) { throw 'В Plugins должно быть шесть пакетов.' }
& (Join-Path $repo 'tools\write-bundled-index.ps1') -Dir $plugins | Out-Host
Copy-Item (Join-Path $repo 'docs\plugins\README.md') (Join-Path $samples 'README.md')
Copy-Item (Join-Path $repo 'docs\plugins\ADR-0001-plugin-trust.md') (Join-Path $stage 'PLUGINS-TRUST.md')

(Get-Content (Join-Path $release 'README-package.txt') -Raw).Replace('{VERSION}', $version).Replace('{DATE}', $ReleaseDate) |
    Set-Content (Join-Path $stage 'README.txt') -Encoding utf8

# Защита от случайного включения чужих материалов.
foreach ($forbidden in 'references', 'research', 'TestResults') {
    if (Get-ChildItem $stage -Recurse -Directory -Filter $forbidden) { throw "В пакет попала папка $forbidden." }
}

if (Get-ChildItem $stage -Recurse -Filter '*.png' | Where-Object { $_.Name -like 'Screenshot*' }) { throw 'В пакет попали снимки экрана.' }

# Контрольные суммы файлов пакета (относительные пути) и архива.
$sums = Get-ChildItem $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $rel
}
$sums | Set-Content (Join-Path $Output "$name.files.sha256") -Encoding ascii

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
# Контрольная сумма этой версии — отдельным файлом; в общем SHA256SUMS.txt обновляется только строка этой версии
# (строки прежних выпусков сохраняются).
"$zipHash  $name.zip" | Set-Content (Join-Path $Output "$name.sha256") -Encoding ascii
$sumsPath = Join-Path $Output 'SHA256SUMS.txt'
$lines = @(if (Test-Path $sumsPath) { Get-Content $sumsPath | Where-Object { $_ -and -not $_.EndsWith("  $name.zip") } })
($lines + "$zipHash  $name.zip") | Set-Content $sumsPath -Encoding ascii

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Готово: $zip ($size МБ), SHA-256 $zipHash"
Write-Host 'Пакет не подписан. Публикация не выполнялась.'
