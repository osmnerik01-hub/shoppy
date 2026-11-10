param(
    [ValidateSet('stop','start','restart','shutdown')][string]$Action,
    [string]$Services,
    [string]$ControlDirectory = $env:LOCAL_CONTROL_DIRECTORY,
    [switch]$ClearStale,
    [int[]]$Ports = @(5100,5101,5102,5103)
)
$ErrorActionPreference = 'Stop'
$directory = $ControlDirectory
if (!$directory -or (!$ClearStale -and !(Test-Path (Join-Path $directory 'ready')))) { throw 'Controlled local launcher is not running.' }
# Capture original process handles before requesting shutdown (also supports older launchers).
$marker = Join-Path $directory 'ready'
$launcherId = 0
$validId = (Test-Path -LiteralPath $marker) -and [int]::TryParse(([string](Get-Content -LiteralPath $marker -Raw)).Trim(), [ref]$launcherId) -and $launcherId -gt 0
$launcher = if ($validId) { Get-Process -Id $launcherId -ErrorAction SilentlyContinue }
$projectRoot = (Split-Path $PSScriptRoot -Parent).TrimEnd('\')
$projectProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe' OR Name = 'Gateway.exe' OR Name = 'Catalog.Api.exe' OR Name = 'Inventory.Api.exe' OR Name = 'Orders.Api.exe'" |
    Where-Object { ([string]$_.CommandLine).Replace('/','\').IndexOf("$projectRoot\src\", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        ([string]$_.ExecutablePath).Replace('/','\').StartsWith("$projectRoot\src\", [StringComparison]::OrdinalIgnoreCase) })
$children = @($projectProcesses | Where-Object ParentProcessId -eq $launcherId)
$ownerFile = Join-Path $directory 'owner.json'
$verified = $false
if ($launcher) {
    if (Test-Path -LiteralPath $ownerFile) {
        $owner = Get-Content -LiteralPath $ownerFile -Raw | ConvertFrom-Json
        $verified = $owner.pid -eq $launcherId -and $owner.started -eq $launcher.StartTime.ToUniversalTime().Ticks.ToString()
    } elseif ($children.Count -and $launcher.ProcessName -in @('powershell','pwsh')) {
        $verified = $true
    }
}
if (!$verified) {
    if (!$ClearStale -or $Action -ne 'shutdown') { throw 'Kontrol oturumu doğrulanamadı. Başlatıcı, eski kaydı yeniden başlatma sırasında güvenle kontrol eder.' }
    if ($projectProcesses.Count) { throw "Projenin kontrol dışı süreçleri hâlâ çalışıyor: $($projectProcesses.ProcessId -join ', '). Hiçbir süreç durdurulmadı." }
    if (Get-NetTCPConnection -LocalPort $Ports -State Listen -ErrorAction SilentlyContinue) { throw 'Eski kontrol kaydı var ve uygulama portlarından biri dinleniyor. Süreç sahipliği doğrulanmadan yeniden başlatılmadı.' }
    foreach ($name in @('ready','owner.json','request.json','request.tmp','response.txt')) {
        Remove-Item -LiteralPath (Join-Path $directory $name) -ErrorAction SilentlyContinue
    }
    Write-Host 'Eski kontrol kaydı kaldırıldı; uygulama portları boş ve projeye ait çalışan servis yok.'
    return
}
$owned = @()
if ($Action -eq 'shutdown') {
    $owned = @($children | ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })
    foreach ($child in $owned) { $null = $child.Handle }
}
$response = Join-Path $directory 'response.txt'
Remove-Item -LiteralPath $response -ErrorAction SilentlyContinue
$request = @{ action=$Action; services=@(if ($Services) { $Services.Split(',') | Where-Object { $_ } }) } | ConvertTo-Json
$temp = Join-Path $directory 'request.tmp'
Set-Content $temp $request
Move-Item -LiteralPath $temp -Destination (Join-Path $directory 'request.json')
if ($Action -eq 'shutdown') {
    $deadline = [DateTime]::UtcNow.AddSeconds(210)
    do {
        $running = @($owned | Where-Object { !$_.HasExited })
        if (!$running.Count -and !(Test-Path (Join-Path $directory 'ready'))) {
            Write-Host "Shutdown verified: $($owned.Count) application processes exited."
            foreach ($child in $owned) { $child.Dispose() }
            return
        }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Shutdown timed out; rebuild must not proceed.' }
        Start-Sleep -Milliseconds 200
    } while ($true)
}
$deadline = [DateTime]::UtcNow.AddSeconds(210)
while (!(Test-Path $response)) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Local control request timed out.' }
    Start-Sleep -Milliseconds 200
}
$result = (Get-Content $response -Raw).Trim()
if ($result -ne 'ok') { throw $result }

