[CmdletBinding()]
param(
    [ValidateRange(1,65535)][int]$GatewayPort = 5100,
    [ValidateRange(1,65535)][int]$CatalogPort = 5101,
    [ValidateRange(1,65535)][int]$InventoryPort = 5102,
    [ValidateRange(1,65535)][int]$OrdersPort = 5103,
    [ValidateRange(5,600)][int]$StartupTimeoutSeconds = 90,
    [switch]$StopAfterReady,
    [string]$ControlDirectory,
    [switch]$UseExistingData
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$processes = @()
$services = @{}
$stopped = @{}
$savedEnvironment = @{}
$controlRegistered = $false
$logReaders = @{}
$startupMutex = $null
$startupLocked = $false
function Publish-LocalLogs {
    foreach ($key in $logReaders.Keys) {
        $log = $logReaders[$key]
        $buffer = New-Object char[] 8192
        $count = $log.Reader.Read($buffer, 0, $buffer.Length)
        if ($count -eq 0) { continue }
        $lines = ($log.Pending + (-join $buffer[0..($count - 1)])) -split '\r?\n'
        $log.Pending = $lines[-1]
        if ($lines.Length -lt 2) { continue }
        foreach ($line in $lines[0..($lines.Length - 2)]) {
            if (!$line) { continue }
            if ($log.Error) { [Console]::Error.WriteLine("[$key] $line") }
            else { Write-Host "[$key] $line" }
        }
    }
}
function Set-LocalEnvironment($Name, $Value) {
    if (!$savedEnvironment.ContainsKey($Name)) { $savedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name, 'Process') }
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
}
function Start-LocalService($Name, $Directory, $Port, $Health = 'ready') {
    $dll = Join-Path $Directory "bin/Debug/net10.0/$Name.dll"
    $outLog = Join-Path $logDirectory "$Name.out.log"
    $errLog = Join-Path $logDirectory "$Name.err.log"
    $child = Start-Process -FilePath $dotnet -WorkingDirectory $Directory -ArgumentList @("`"$dll`"", '--urls', "http://localhost:$Port") -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru
    $script:processes += $child
    $script:services[$Name] = @{ Process = $child; Directory = $Directory; Port = $Port }
    Write-Host "[$Name] PID $($child.Id) · port $Port · $Directory"
    foreach ($entry in @(@('stdout',$outLog), @('stderr',$errLog))) {
        $key = "$Name $($entry[0])"
        if ($logReaders.ContainsKey($key)) { $logReaders[$key].Reader.Dispose() }
        $stream = [IO.File]::Open($entry[1], [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $logReaders[$key] = @{Reader = [IO.StreamReader]::new($stream); Error = $entry[0] -eq 'stderr'; Pending = ''}
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        Publish-LocalLogs
        if ($child.HasExited) {
            $reason = 'Servis baslatma hatasi'
            if ($Name -eq 'Catalog.Api') {
                $details = (Get-Content $outLog,$errLog -Raw -ErrorAction SilentlyContinue) -join "`n"
                $reason = 'TCP kontrolu gecti; veritabani/migration hatasi'
                if ($details -match '28P01|28000|password authentication failed|no pg_hba') { $reason = 'PostgreSQL kimlik dogrulama hatasi (TCP kontrolu gecti)' }
                elseif ($details -match '42501|permission denied') { $reason = 'PostgreSQL yetki/migration hatasi (TCP kontrolu gecti)' }
            }
            $target = if ($Name -eq 'Catalog.Api') { " PostgreSQL hedefi: $safeHost`:$pgPort." } else { '' }
            throw "$Name kapandi (kod $($child.ExitCode)). $reason.$target Loglar: $outLog ve $errLog"
        }
        try {
            $response = Invoke-WebRequest "http://localhost:$Port/health/$Health" -UseBasicParsing -TimeoutSec 4
            if ($response.StatusCode -eq 200) { Write-Host "$Name hazır: http://localhost:$Port"; return }
        } catch { }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    $target = if ($Name -eq 'Catalog.Api') { " PostgreSQL hedefi: $safeHost`:$pgPort (ilk TCP kontrolu gecti; readiness/veritabani/migration kontrolu tamamlanamadi)." } else { '' }
    throw "$Name hazir olmadi.$target Servis loglarini kontrol edin: $outLog ve $errLog"
}
Push-Location $root
try {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    $localSdk = Join-Path $env:LOCALAPPDATA 'Project1/dotnet/dotnet.exe'
    if (Test-Path $localSdk) { $dotnet = $localSdk }
    if (!$dotnet) { throw '.NET 10 SDK kurulmalıdır (global.json).' }
    Set-LocalEnvironment 'DOTNET_ROOT' (Split-Path $dotnet -Parent)
    Set-LocalEnvironment 'PATH' "$(Split-Path $dotnet -Parent);$env:PATH"
    & $dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'global.json ile uyumlu .NET 10 SDK kurulmalıdır.' }
    if (!(Get-Command node -ErrorAction SilentlyContinue) -or !(Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'Node.js ve npm kurulmalı ve PATH içinde bulunmalıdır (önerilen Node.js 24 LTS).' }
    if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Catalog)) { throw 'ConnectionStrings__Catalog PostgreSQL bağlantısını ayarlayın. docs/local-windows.md belgesine bakın.' }
    # Validate service reachability before installing packages/building. Authentication
    # and schema access are checked by migrations and readiness below.
    $pg = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $pg.set_ConnectionString($env:ConnectionStrings__Catalog) } catch { throw 'ConnectionStrings__Catalog geçerli bir PostgreSQL bağlantı dizesi olmalıdır.' }
    $pgHost = 'localhost'
    foreach ($key in @('Host','Server')) { if ($pg.ContainsKey($key)) { $pgHost = [string]$pg[$key]; break } }
    $pgPort = 5432
    if ($pg.ContainsKey('Port')) {
        if (![int]::TryParse([string]$pg['Port'], [ref]$pgPort) -or $pgPort -lt 1 -or $pgPort -gt 65535) {
            throw 'ConnectionStrings__Catalog Port degeri 1-65535 arasinda bir tamsayi olmalidir.'
        }
    }
    $safeHost = $pgHost -replace '[^a-zA-Z0-9.:_-]', '?'
    Write-Host "PostgreSQL TCP hedefi: $safeHost`:$pgPort"
    foreach ($dependency in @(,@('PostgreSQL', $pgHost, $pgPort))) {
        $tcp = New-Object System.Net.Sockets.TcpClient
        try {
            $task = $tcp.ConnectAsync([string]$dependency[1], [int]$dependency[2])
            if (!$task.Wait(3000) -or !$tcp.Connected) { throw 'unreachable' }
        } catch { throw "PostgreSQL TCP erisilemiyor ($safeHost`:$pgPort). Windows servisini veya yerel pg_ctl ornegini, adresi ve portu kontrol edin." }
        finally { $tcp.Dispose() }
    }
    $ports = @($GatewayPort, $CatalogPort, $InventoryPort, $OrdersPort)
    if (@($ports | Select-Object -Unique).Count -ne 4) { throw 'Uygulama portları birbirinden farklı olmalıdır.' }
    if ($ControlDirectory) {
        $ControlDirectory = [IO.Path]::GetFullPath($ControlDirectory)
        $hash = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($ControlDirectory.ToLowerInvariant()))).Replace('-','')
        $startupMutex = [Threading.Mutex]::new($false, "Local\Project1-$hash")
        try { $startupLocked = $startupMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $startupLocked = $true }
        if (!$startupLocked) { throw 'Bu kontrol dizininde başka bir başlatma devam ediyor.' }
        if (@('ready','owner.json','request.json') | Where-Object { Test-Path -LiteralPath (Join-Path $ControlDirectory $_) }) {
            & "$PSScriptRoot/control-local.ps1" -Action shutdown -ControlDirectory $ControlDirectory -ClearStale -Ports $ports
        }
    }
    foreach ($port in $ports) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port kullanımda: $port. İlgisiz süreçleri kapatmayın; boş bir port seçin veya mevcut kontrollü oturumun ControlDirectory değerini kullanın." }
    }
    if ($UseExistingData) {
        foreach ($entry in @(@('Inventory','inventory.db'), @('Orders','orders.db'))) {
            $variable = "ConnectionStrings__" + $entry[0]
            $connection = [Environment]::GetEnvironmentVariable($variable, 'Process')
            if (!$connection) { $connection = "Data Source=$(Join-Path $root ('artifacts/revision-data/' + $entry[1]))" }
            $sqlite = New-Object System.Data.Common.DbConnectionStringBuilder
            try { $sqlite.set_ConnectionString($connection) } catch { throw "$variable gecerli SQLite baglantisi olmalidir." }
            $path = [string]$sqlite['Data Source']
            if (!$path) { throw "$variable icin Data Source dosya yolu gereklidir." }
            if (![IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Mevcut veritabani dosyasi bulunamadi; dogru veri yolunu geri yukleyin: $path" }
            $sqlite['Data Source'] = $path
            $sqlite['Mode'] = 'ReadWrite'
            Set-LocalEnvironment $variable $sqlite.get_ConnectionString()
        }
        Set-LocalEnvironment 'Inventory__SeedDemo' 'false'
    }
    $dataDirectory = Join-Path $root 'artifacts/local/data' 
    $logDirectory = Join-Path $root ('artifacts/local/logs/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $dataDirectory, $logDirectory | Out-Null
    if (!$env:ConnectionStrings__Inventory) { Set-LocalEnvironment 'ConnectionStrings__Inventory' "Data Source=$dataDirectory/inventory.db;Default Timeout=30" }
    if (!$env:ConnectionStrings__Orders) { Set-LocalEnvironment 'ConnectionStrings__Orders' "Data Source=$dataDirectory/orders.db;Default Timeout=30" }
    Set-LocalEnvironment 'Catalog__ApplyMigrations' 'true'
    if (!$env:Inventory__SeedDemo) { Set-LocalEnvironment 'Inventory__SeedDemo' 'true' }
    Set-LocalEnvironment 'Inventory__Url' "http://127.0.0.1:$InventoryPort"
    Set-LocalEnvironment 'Orders__Url' "http://127.0.0.1:$OrdersPort"
    Set-LocalEnvironment 'Catalog__Url' "http://127.0.0.1:$CatalogPort"
    Set-LocalEnvironment 'ReverseProxy__Clusters__catalog__Destinations__primary__Address' "http://127.0.0.1:$CatalogPort/"
    Set-LocalEnvironment 'ReverseProxy__Clusters__orders__Destinations__primary__Address' "http://127.0.0.1:$OrdersPort/"
    Push-Location (Join-Path $root 'src/Web')
    try {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci başarısız.' }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Web derlemesi başarısız.' }
    } finally { Pop-Location }
    Set-LocalEnvironment 'MSBUILDDISABLENODEREUSE' '1'
    Set-LocalEnvironment 'DOTNET_CLI_USE_MSBUILD_SERVER' '0'
    & $dotnet build Project1.sln --nologo --disable-build-servers -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    Start-LocalService 'Catalog.Api' (Join-Path $root 'src/Services/Catalog/Catalog.Api') $CatalogPort
    Start-LocalService 'Inventory.Api' (Join-Path $root 'src/Services/Inventory/Inventory.Api') $InventoryPort
    Start-LocalService 'Orders.Api' (Join-Path $root 'src/Services/Orders/Orders.Api') $OrdersPort
    Start-LocalService 'Gateway' (Join-Path $root 'src/Gateway') $GatewayPort
    foreach ($route in @('/', '/api/products')) {
        try {
            $page = Invoke-WebRequest "http://localhost:$GatewayPort$route" -UseBasicParsing -TimeoutSec 10
            if ($page.StatusCode -ne 200) { throw "HTTP $($page.StatusCode)" }
            if ($route -eq '/' -and $page.Headers['Content-Type'] -notmatch 'text/html') { throw 'HTML arayüzü alınamadı' }
            if ($route -eq '/api/products') { $null = $page.Content | ConvertFrom-Json }
        } catch { throw "Gateway $route doğrulanamadı. Gateway/Catalog loglarını ve Web derlemesini kontrol edin: $($_.Exception.Message)" }
    }
    Write-Host "Doğrulanmış arayüz: http://localhost:$GatewayPort. Loglar: $logDirectory. Durdurmak için Ctrl+C."
    if ($ControlDirectory) {
        $ControlDirectory = [IO.Path]::GetFullPath($ControlDirectory)
        New-Item -ItemType Directory -Force $ControlDirectory | Out-Null
        @{pid=$PID; started=(Get-Process -Id $PID).StartTime.ToUniversalTime().Ticks.ToString()} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ControlDirectory 'owner.json')
        Set-Content (Join-Path $ControlDirectory 'ready') $PID
        $controlRegistered = $true
        $startupMutex.ReleaseMutex()
        $startupLocked = $false
    }
    if (!$StopAfterReady) {
        while ($true) {
            Publish-LocalLogs
            if ($ControlDirectory -and (Test-Path (Join-Path $ControlDirectory 'request.json'))) {
                $request = Get-Content (Join-Path $ControlDirectory 'request.json') -Raw | ConvertFrom-Json
                Remove-Item -LiteralPath (Join-Path $ControlDirectory 'request.json')
                $result = 'ok'
                try {
                    if ($request.action -eq 'shutdown') { break }
                    foreach ($name in $request.services) {
                        $dllName = @{ inventory='Inventory.Api'; orders='Orders.Api' }[$name]
                        if (!$dllName) { throw "Unknown service: $name" }
                        $service = $services[$dllName]
                        if ($request.action -in @('stop','restart')) {
                            $stopped[$service.Process.Id] = $true
                            if (!$service.Process.HasExited) { Stop-Process -Id $service.Process.Id; $service.Process.WaitForExit() }
                        }
                        if ($request.action -in @('start','restart')) { Start-LocalService $dllName $service.Directory $service.Port 'live' }
                    }
                } catch { $result = $_.Exception.Message }
                Set-Content (Join-Path $ControlDirectory 'response.txt') $result
            }
            foreach ($child in $processes) { if (!$stopped.ContainsKey($child.Id) -and $child.HasExited) { throw "Uygulama kapandı: $($child.Id). Loglar: $logDirectory" } }
            Start-Sleep -Seconds 1
        }
    }
} finally {
    foreach ($child in $processes) {
        if (!$child.HasExited) {
            Stop-Process -Id $child.Id -ErrorAction SilentlyContinue
            if (!$child.WaitForExit(30000)) { throw "Application did not exit: $($child.Id). Rebuild is unsafe." }
        }
        $child.Dispose()
    }
    foreach ($log in $logReaders.Values) { $log.Reader.Dispose() }
    if ($controlRegistered) { foreach ($name in @('ready','owner.json')) { Remove-Item -LiteralPath (Join-Path $ControlDirectory $name) -ErrorAction SilentlyContinue } }
    if ($startupLocked) { $startupMutex.ReleaseMutex() }
    if ($startupMutex) { $startupMutex.Dispose() }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Pop-Location
}


