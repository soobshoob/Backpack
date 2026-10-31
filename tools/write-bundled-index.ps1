<#
.SYNOPSIS
  Пишет индекс поставки плагинов Plugins\bundled.json (Phase 4H): для каждого пакета .bppkg — имя файла, ID и версию из
  manifest.json и SHA-256 пакета. Backpack при запуске ставит в каталог только пакеты, совпавшие с индексом (код не
  запускается, плагины выключены), и отмечает их «От создателя». Это проверка происхождения файла, не подпись издателя.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\write-bundled-index.ps1 -Dir artifacts\stage\Plugins
#>
param([Parameter(Mandatory = $true)][string]$Dir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @()
foreach ($pkg in Get-ChildItem $Dir -Filter *.bppkg | Sort-Object Name) {
    $zip = [IO.Compression.ZipFile]::OpenRead($pkg.FullName)
    try {
        $entry = $zip.GetEntry('manifest.json')
        if (-not $entry) { throw "$($pkg.Name): нет manifest.json" }
        $reader = New-Object IO.StreamReader($entry.Open())
        $manifest = $reader.ReadToEnd() | ConvertFrom-Json
        $reader.Dispose()
    } finally { $zip.Dispose() }
    $packages += [ordered]@{
        file = $pkg.Name
        id = $manifest.id
        version = $manifest.version
        sha256 = (Get-FileHash $pkg.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$index = [ordered]@{ format = 'backpack-bundled'; formatVersion = 1; packages = $packages }
$json = $index | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $Dir 'bundled.json'), $json, (New-Object Text.UTF8Encoding $false))
"bundled.json: $($packages.Count) packages"
