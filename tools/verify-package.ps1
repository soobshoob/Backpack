<#
.SYNOPSIS
  Проверка готового пакета Backpack без влияния на настройки пользователя.

.DESCRIPTION
  - распаковка zip в папку с пробелами во временном каталоге;
  - проверка контрольной суммы, состава (нет .pdb, references, снимков экрана) и self-contained (hostfxr, coreclr рядом);
  - запуск без .NET в PATH и с пустым DOTNET_ROOT, от обычного пользователя (процесс не повышается: asInvoker);
  - лабораторная самопроверка: --lab --auto-exit-ms с временным файлом настроек и --summary-out (только агрегаты);
  - обычный запуск: процесс жив, главное окно «Backpack» открыто; затем процесс закрывается (Stop-Process) — это
    завершение проверки, а не пользовательский сценарий выхода;
  - после проверки собственных процессов Backpack не остаётся. Ввод не инжектируется.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Zip,
    [string]$Sums = (Join-Path (Split-Path $Zip) 'SHA256SUMS.txt'),
    # Полная самопроверка интерфейса из распакованного пакета (окна открываются на экране, несколько минут): плагины из Plugins\.
    [switch]$SelfTestUi,
    # Phase 4I: GitHub-hosted Windows runners run elevated; the check is then reported as WARN (not PASS) instead of failing.
    [switch]$AllowElevated
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$results = [System.Collections.Generic.List[string]]::new()
function Pass($what) { $results.Add("PASS  $what"); Write-Host "PASS  $what" }
function Fail($what) { $results.Add("FAIL  $what"); Write-Host "FAIL  $what" -ForegroundColor Red }

# В SHA256SUMS.txt — строки всех выпусков: берётся строка именно этого zip.
$zipName = Split-Path $Zip -Leaf
$line = Get-Content $Sums | Where-Object { $_ -match "^\s*[0-9a-fA-F]{64}\s+\*?$([regex]::Escape($zipName))\s*$" } | Select-Object -First 1
$expected = if ($line) { ($line.Trim() -split '\s+')[0].ToLowerInvariant() } else { "(нет строки для $zipName)" }
$actual = (Get-FileHash $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expected -eq $actual) { Pass "SHA-256 совпадает ($actual)" } else { Fail "SHA-256: ожидалось $expected, получено $actual" }

$root = Join-Path ([IO.Path]::GetTempPath()) ("Backpack package test " + [guid]::NewGuid().ToString('N').Substring(0, 6))
$app = Join-Path $root 'Program Files Like\Backpack App'
$data = Join-Path $root 'User Data'
New-Item -ItemType Directory -Force $app, $data | Out-Null
Expand-Archive $Zip -DestinationPath $app
Pass "распаковано в путь с пробелами: $app"

$exe = Join-Path $app 'Backpack.exe'
foreach ($f in 'Backpack.exe', 'Backpack.Sdk.dll', 'PLUGINS-TRUST.md', 'Plugins\bundled.json', 'Plugins\com.example.drawing-1.0.1.bppkg', 'Plugins\com.example.layout-indicator-1.0.1.bppkg', 'Samples\Drawing\DrawingPlugin.cs', 'Samples\LayoutIndicator\LayoutIndicatorPlugin.cs', 'Plugins\com.example.cursor-zoom-1.0.1.bppkg', 'Plugins\com.example.lens-1.0.1.bppkg',
        'Plugins\com.example.focus-1.0.1.bppkg', 'Plugins\com.example.presentation-timer-2.0.1.bppkg', 'Samples\README.md', 'Samples\Focus\Focus.csproj',
        'Samples\Focus\manifest.json', 'Samples\CursorZoom\CursorZoomPlugin.cs', 'Samples\Lens\LensPlugin.cs', 'Samples\PresentationTimer\TimerPlugin.cs', 'hostfxr.dll', 'coreclr.dll', 'PresentationFramework.dll', 'README.txt', 'RELEASE-NOTES.md', 'Licenses\Inter-LICENSE.txt', 'Licenses\Spectral-LICENSE.txt', 'Licenses\Lucide-LICENSE.txt', 'Licenses\dotnet-LICENSE.txt', 'LICENSE.txt', 'NOTICE.txt') {
    if (Test-Path (Join-Path $app $f)) { Pass "есть $f" } else { Fail "нет $f" }
}

# Phase 4I: ядро — официальный текст MPL 2.0 (mozilla.org/media/MPL/2.0/index.txt) без изменений; SDK и плагины от создателя —
# отдельный официальный текст PolyForm Strict 1.0.0 (polyformproject/polyform-licenses, тег 1.0.0) до решения владельца.
$mplHash = '3f3d9e0024b1921b067d6f7f88deb4a60cbe7a78e76c64e3f1d7fc3b779b9d04'
$strictHash = '9eb48619fbc193ab7bb327b090cfcc703000265b83e670f81f231d0b1c43c56e'
function HashOf($p) { if (Test-Path $p) { (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant() } else { '' } }
$licenseHash = HashOf (Join-Path $app 'LICENSE.txt')
if ($licenseHash -eq $mplHash) { Pass 'LICENSE.txt — официальный текст MPL 2.0 (ядро)' } else { Fail "LICENSE.txt отличается от официального текста MPL 2.0 ($licenseHash)" }
foreach ($f in 'Licenses\Backpack.Sdk-LICENSE.txt', 'Licenses\PolyForm-Strict-1.0.0.txt', 'Samples\LICENSE.txt') {
    if ((HashOf (Join-Path $app $f)) -eq $strictHash) { Pass "$f — PolyForm Strict 1.0.0 (SDK и плагины от создателя отдельно от MPL)" } else { Fail "$f отсутствует или отличается от PolyForm Strict 1.0.0" }
}
$notice = if (Test-Path (Join-Path $app 'NOTICE.txt')) { Get-Content (Join-Path $app 'NOTICE.txt') -Raw } else { '' }
if ($notice -match 'Mozilla Public License 2\.0' -and $notice -match 'PolyForm Strict') { Pass 'NOTICE.txt называет MPL для ядра и отдельные условия SDK и плагинов' } else { Fail 'NOTICE.txt не описывает области лицензий' }
# Каждый пакет плагина от создателя содержит LICENSE.txt (в preview.4/5 пакеты ссылались на него, но не содержали).
Add-Type -AssemblyName System.IO.Compression.FileSystem
$noLicense = @(Get-ChildItem (Join-Path $app 'Plugins') -Filter *.bppkg | Where-Object {
    $z = [IO.Compression.ZipFile]::OpenRead($_.FullName)
    try { -not ($z.Entries | Where-Object FullName -eq 'LICENSE.txt') } finally { $z.Dispose() }
})
if ($noLicense.Count -eq 0) { Pass 'каждый .bppkg содержит LICENSE.txt' } else { Fail "без LICENSE.txt: $($noLicense.Name -join ', ')" }

$bad = Get-ChildItem $app -Recurse | Where-Object { $_.Extension -eq '.pdb' -or $_.Name -like 'Screenshot*' -or $_.Name -in 'references', 'research' -or $_.Name -like 'keyoverlay-lab-*.json' }
if ($bad) { Fail "лишние файлы: $($bad.Name -join ', ')" } else { Pass 'нет .pdb, снимков экрана, references/research и экспортов диагностики' }

# Phase 4H: индекс поставки — шесть пакетов, SHA-256 каждого совпадает с файлом (основа отметки «От создателя»).
$indexPath = Join-Path $app 'Plugins\bundled.json'
if (Test-Path $indexPath) {
    $index = Get-Content $indexPath -Raw | ConvertFrom-Json
    $okIndex = @($index.packages).Count -eq 6 -and -not (@($index.packages) | Where-Object { (Get-FileHash (Join-Path $app "Plugins\$($_.file)") -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256 })
    if ($okIndex) { Pass 'bundled.json: шесть пакетов, SHA-256 совпадают' } else { Fail 'bundled.json: не шесть пакетов или хеш не совпадает' }
}

$runtimeConfig = Get-Content (Join-Path $app 'Backpack.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtimeConfig.runtimeOptions.PSObject.Properties.Name -contains 'includedFrameworks') { Pass 'self-contained: runtime в пакете' } else { Fail 'пакет зависит от установленного runtime' }

$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($elevated -and $AllowElevated) { $results.Add("WARN  проверка запущена с правами администратора (CI); проверка запуска без прав не выполнена"); Write-Host "WARN  elevated (CI)" -ForegroundColor Yellow }
elseif ($elevated) { Fail 'проверка запущена с правами администратора — запустите от обычного пользователя' } else { Pass 'проверка идёт без прав администратора' }

# Окружение без .NET: PATH без dotnet, пустой DOTNET_ROOT.
$env:DOTNET_ROOT = Join-Path $root 'no-dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:PATH = ($env:PATH.Split(';') | Where-Object { $_ -and $_ -notmatch '\\dotnet' }) -join ';'
New-Item -ItemType Directory -Force $env:DOTNET_ROOT | Out-Null

$settings = Join-Path $data 'settings.json'
$summary = Join-Path $data 'summary.json'
$p = Start-Process $exe -ArgumentList '--lab', '--auto-exit-ms=5000', "--settings=`"$settings`"", "--summary-out=`"$summary`"" -PassThru -Wait
if ($p.ExitCode -eq 0) { Pass 'лабораторный запуск: код 0' } else { Fail "лабораторный запуск: код $($p.ExitCode)" }
if (Test-Path $summary) {
    $s = Get-Content $summary -Raw | ConvertFrom-Json
    $notes = $s.ShutdownNotes -join ' | '
    if ($notes -match 'overlay styles ok' -and $notes -notmatch 'unhandled|left after close|MISSING') { Pass 'самопроверка окон: стили оверлея подтверждены, утечек подписок нет' } else { Fail "самопроверка: $notes" }
    if ($s.Sources | Where-Object { $_.Status -eq 'Stopped' }) { Pass 'источник ввода запущен и остановлен' } else { Fail 'источник ввода не остановлен' }
} else { Fail 'нет файла --summary-out' }

# Режим хоста плагина: без пакета плагина завершается сразу с кодом 10 (окна не открываются, захват не запускается).
$ph = Start-Process $exe -ArgumentList '--plugin-host', "`"$(Join-Path $data 'no-plugin')`"" -PassThru -Wait
if ($ph.ExitCode -eq 10) { Pass 'режим хоста плагина: отсутствующий пакет отклонён (код 10)' } else { Fail "режим хоста плагина: код $($ph.ExitCode)" }

$settings2 = Join-Path $data 'normal\settings.json'
$p2 = Start-Process $exe -ArgumentList "--settings=`"$settings2`"", '--no-plugins' -PassThru
$deadline = (Get-Date).AddSeconds(15)
$title = ''
while ((Get-Date) -lt $deadline -and -not $title) {
    Start-Sleep -Milliseconds 500
    $p2.Refresh()
    $title = $p2.MainWindowTitle
}
if (-not $p2.HasExited -and $title -eq 'Backpack') { Pass 'обычный запуск: главное окно «Backpack» открыто' } else { Fail "обычный запуск: окно «$title», процесс завершён: $($p2.HasExited)" }
if (-not $p2.HasExited) { Stop-Process -Id $p2.Id -Force; $p2.WaitForExit(5000) | Out-Null }
if ($SelfTestUi) {
    $uiSummary = Join-Path $data 'selftest-ui.json'
    $env:BACKPACK_SELFTEST_PLUGIN = Join-Path $app 'Plugins\com.example.presentation-timer-2.0.1.bppkg'
    Remove-Item Env:BACKPACK_BUNDLED_PLUGINS -ErrorAction SilentlyContinue
    $ui = Start-Process $exe -ArgumentList '--lab', '--selftest-ui', "--settings=`"$(Join-Path $data 'ui\settings.json')`"", "--summary-out=`"$uiSummary`"" -PassThru
    if (-not $ui.WaitForExit(600000)) { Stop-Process -Id $ui.Id -Force; Fail 'selftest-ui: не завершилась за 10 минут' }
    elseif (Test-Path $uiSummary) {
        $notes = @((Get-Content $uiSummary -Raw | ConvertFrom-Json).ShutdownNotes) + @((Get-Content $uiSummary -Raw | ConvertFrom-Json).Findings) | Where-Object { "$_" -like 'selftest-ui:*' }
        foreach ($n in $notes | Where-Object { "$_" -match 'tool plugin|plugin com|: ok, steps|FAILED' }) { Write-Host "      $n" }
        if (@($notes | Where-Object { "$_" -match 'tool plugin com\.example\.' }).Count -eq 3) { Pass 'selftest-ui: три плагина-инструмента из Plugins\ прошли путь импорт → включение → команды → отключение → удаление' } else { Fail 'selftest-ui: не все плагины-инструменты проверены' }
        if (@($notes | Where-Object { "$_" -match 'plugin com\.example\.(drawing|layout-indicator) .* ok' }).Count -eq 2) { Pass 'selftest-ui: «Рисование» и «Индикатор раскладки» прошли путь каталог → доверие → включение → команды → Esc → отключение → возврат' } else { Fail 'selftest-ui: плагины рисования и индикатора не проверены' }
    if ($notes -match 'live hover pointer moves [1-9]') { Pass 'selftest-ui: живое наведение (указатель двигался, пока впереди окно самопроверки)' } else { Fail 'selftest-ui: живое наведение не выполнено (окно самопроверки не было впереди?)' }
    if ($notes -match ': ok, steps') { Pass 'selftest-ui: все шаги прошли' } else { Fail 'selftest-ui: есть непройденные шаги (см. выше)' }
    } else { Fail 'selftest-ui: нет файла отчёта' }
}

if (Get-Process Backpack -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$app*" }) { Fail 'остались процессы Backpack' } else { Pass 'процессов Backpack из пакета не осталось' }

Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { $_ -like "FAIL*" }).Count
Write-Host "Итог: $($results.Count - $failed) PASS, $failed FAIL"
exit $failed
