<#
.SYNOPSIS
  Проверка записей реестра плагинов, ссылающихся на прямой пакет .bppkg (Phase 4I). Код плагина не выполняется.

.DESCRIPTION
  Для каждой записи distribution.kind = "package":
  - скачивание по HTTPS с ограничением размера (по умолчанию 25 МиБ, обрыв при превышении, без перенаправлений на другие схемы);
  - SHA-256 файла совпадает с distribution.sha256 (если указан) и sizeBytes (если указан);
  - разбор ZIP только чтением из памяти, без распаковки на диск: число записей, суммарный распакованный размер, коэффициент
    сжатия каждой записи (защита от zip-бомб), имена без выхода за каталог (.., абсолютные пути, диски, обратные слэши в начале);
  - manifest.json в корне: формат backpack-plugin, ID и версия совпадают с записью реестра, есть поле license;
  - LICENSE.txt в пакете (предупреждение, если нет).
  Записи "release" (страница выпуска) не скачиваются: их проверяет владелец вручную.
  Прохождение проверки не означает, что расширение безопасно: решение о merge принимает владелец после просмотра.

.EXAMPLE
  pwsh tools/registry/Test-RegistryPackages.ps1 -Index registry/index.json
  pwsh tools/registry/Test-RegistryPackages.ps1 -Index pr/registry/index.json -Offline   # только структура записей
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Index,
    [string]$Schema = (Join-Path $PSScriptRoot '..\..\registry\schema\registry-v1.schema.json'),
    [long]$MaxPackageBytes = 25MB,
    [long]$MaxUnpackedBytes = 100MB,
    [int]$MaxEntries = 2000,
    [double]$MaxRatio = 100,
    [switch]$Offline,
    # Только для тестов: проверить локальные файлы вместо скачивания (имя файла = последний сегмент URL).
    [string]$LocalPackages = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$failures = [System.Collections.Generic.List[string]]::new()
function Fail($what) { $failures.Add($what); Write-Host "FAIL  $what" -ForegroundColor Red }
function Pass($what) { Write-Host "PASS  $what" }
function Warn($what) { Write-Host "WARN  $what" -ForegroundColor Yellow }

# 1. Структура: JSON Schema v1 сайта (Test-Json, PowerShell 7.4+, draft 2020-12).
$json = Get-Content $Index -Raw -Encoding utf8
if (Test-Json -Json $json -SchemaFile $Schema -ErrorAction SilentlyContinue) { Pass 'index.json соответствует registry-v1.schema.json' }
else { Fail 'index.json не соответствует схеме'; Test-Json -Json $json -SchemaFile $Schema -ErrorAction Continue | Out-Null }

$registry = $json | ConvertFrom-Json -Depth 32
$packages = @($registry.plugins | Where-Object { $_.distribution.kind -eq 'package' })
Write-Host "записей: $(@($registry.plugins).Count), прямых пакетов: $($packages.Count)"
if ($Offline) { if ($failures.Count) { exit 1 } else { exit 0 } }

Add-Type -AssemblyName System.IO.Compression
$http = [System.Net.Http.HttpClient]::new([System.Net.Http.HttpClientHandler]@{ AllowAutoRedirect = $true; MaxAutomaticRedirections = 5 })
$http.Timeout = [TimeSpan]::FromSeconds(60)
$http.DefaultRequestHeaders.UserAgent.ParseAdd('Backpack-registry-check/1.0')

function Get-Package([string]$url) {
    if ($LocalPackages) { return [IO.File]::ReadAllBytes((Join-Path $LocalPackages ([Uri]$url).Segments[-1])) }
    $response = $http.GetAsync($url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "HTTP $([int]$response.StatusCode)" }
    if ($response.RequestMessage.RequestUri.Scheme -ne 'https') { throw 'перенаправление не на HTTPS' }
    if ($response.Content.Headers.ContentLength -gt $MaxPackageBytes) { throw "больше $MaxPackageBytes байт" }
    $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    $buffer = [IO.MemoryStream]::new()
    $chunk = [byte[]]::new(81920)
    while (($n = $stream.Read($chunk, 0, $chunk.Length)) -gt 0) {
        $buffer.Write($chunk, 0, $n)
        if ($buffer.Length -gt $MaxPackageBytes) { throw "больше $MaxPackageBytes байт" }
    }
    return $buffer.ToArray()
}

function Test-SafeName([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name.StartsWith('/') -or $name.StartsWith('\') -or $name.Contains(':')) { return $false }
    return -not ($name -split '[\\/]' | Where-Object { $_ -in '..', '.' })
}

foreach ($p in $packages) {
    $at = "$($p.id) $($p.version)"
    try {
        $bytes = Get-Package $p.distribution.url
        $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        if ($p.distribution.PSObject.Properties.Name -contains 'sha256') {
            if ($sha -eq $p.distribution.sha256.ToLowerInvariant()) { Pass "$at — SHA-256 совпадает" } else { Fail "$at — SHA-256 $sha не совпадает с записью" }
        } else { Warn "$at — в записи нет sha256 (рекомендуется для прямого пакета); фактический $sha" }
        if ($p.distribution.PSObject.Properties.Name -contains 'sizeBytes' -and $p.distribution.sizeBytes -ne $bytes.Length) { Fail "$at — sizeBytes $($p.distribution.sizeBytes) ≠ $($bytes.Length)" }

        $zip = [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($bytes), [IO.Compression.ZipArchiveMode]::Read)
        try {
            $entries = @($zip.Entries)
            if ($entries.Count -gt $MaxEntries) { throw "записей $($entries.Count) > $MaxEntries" }
            $total = ($entries | Measure-Object Length -Sum).Sum
            if ($total -gt $MaxUnpackedBytes) { throw "распакованный размер $total > $MaxUnpackedBytes" }
            $bomb = @($entries | Where-Object { $_.Length -gt 1MB -and $_.CompressedLength -gt 0 -and ($_.Length / $_.CompressedLength) -gt $MaxRatio })
            if ($bomb) { throw "подозрительное сжатие: $($bomb.FullName -join ', ')" }
            $unsafe = @($entries | Where-Object { -not (Test-SafeName $_.FullName) })
            if ($unsafe) { throw "небезопасные пути: $($unsafe.FullName -join ', ')" }
            $manifestEntry = $zip.GetEntry('manifest.json')
            if (-not $manifestEntry) { throw 'нет manifest.json в корне' }
            if ($manifestEntry.Length -gt 1MB) { throw 'manifest.json больше 1 МиБ' }
            $reader = [IO.StreamReader]::new($manifestEntry.Open(), [Text.UTF8Encoding]::new($false, $true))
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -Depth 32 } finally { $reader.Dispose() }
            if ($manifest.format -ne 'backpack-plugin') { throw 'manifest.json: format не backpack-plugin' }
            if ($manifest.id -ne $p.id) { throw "manifest id $($manifest.id) ≠ $($p.id)" }
            if ($manifest.version -ne $p.version) { throw "manifest version $($manifest.version) ≠ $($p.version)" }
            if (-not $manifest.license) { throw 'manifest.json: нет поля license' }
            if (-not $zip.GetEntry('LICENSE.txt')) { Warn "$at — в пакете нет LICENSE.txt" }
            Pass "$at — пакет $($bytes.Length) байт, $($entries.Count) файлов, пути безопасны, manifest совпадает"
        } finally { $zip.Dispose() }
    } catch {
        Fail "$at — $($_.Exception.Message)"
    }
}

Write-Host 'Проверка схемы, архива и хеша не означает, что расширение безопасно. Решение о merge — за владельцем после просмотра.'
if ($failures.Count) { exit 1 }
