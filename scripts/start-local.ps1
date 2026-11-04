[CmdletBinding()]
param(
    [ValidateRange(1,65535)][int]$GatewayPort = 5100,
    [ValidateRange(1,65535)][int]$CatalogPort = 5101,
    [ValidateRange(1,65535)][int]$InventoryPort = 5102,
    [ValidateRange(1,65535)][int]$OrdersPort = 5103,
    [ValidateRange(5,600)][int]$StartupTimeoutSeconds = 90,
    [switch]$StopAfterReady,
    [string]$ControlDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$processes = @()
$services = @{}
$stopped = @{}
$savedEnvironment = @{}
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
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        if ($child.HasExited) { throw "$Name kapandı (kod $($child.ExitCode)). Loglar: $outLog ve $errLog" }
        try {
            $response = Invoke-WebRequest "http://localhost:$Port/health/$Health" -UseBasicParsing -TimeoutSec 4
            if ($response.StatusCode -eq 200) { Write-Host "$Name hazır: http://localhost:$Port"; return }
        } catch { }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Name hazır olmadı. Veritabanı bağlantısı/yetkileri ve RabbitMQ kullanıcı/vhost izinlerini kontrol edin. Loglar: $outLog ve $errLog"
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
    if ([string]::IsNullOrWhiteSpace($env:RabbitMQ__Uri)) { throw 'RabbitMQ__Uri bağlantısını ayarlayın (amqp://kullanici:parola@localhost:5672/).' }
    $brokerUri = $null
    if (![Uri]::TryCreate($env:RabbitMQ__Uri, [UriKind]::Absolute, [ref]$brokerUri) -or $brokerUri.Scheme -notin @('amqp','amqps')) { throw 'RabbitMQ__Uri geçerli bir amqp/amqps adresi olmalıdır.' }
    # Validate service reachability before installing packages/building. Authentication
    # and schema access are checked by migrations and readiness below.
    $pg = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $pg.set_ConnectionString($env:ConnectionStrings__Catalog) } catch { throw 'ConnectionStrings__Catalog geçerli bir PostgreSQL bağlantı dizesi olmalıdır.' }
    $pgHost = 'localhost'
    foreach ($key in @('Host','Server')) { if ($pg.ContainsKey($key)) { $pgHost = [string]$pg[$key]; break } }
    $pgPort = 5432
    if ($pg.ContainsKey('Port')) { $pgPort = [int]$pg['Port'] }
    $brokerPort = $brokerUri.Port
    if ($brokerPort -lt 1) { if ($brokerUri.Scheme -eq 'amqps') { $brokerPort = 5671 } else { $brokerPort = 5672 } }
    foreach ($dependency in @(@('PostgreSQL', $pgHost, $pgPort), @('RabbitMQ', $brokerUri.Host, $brokerPort))) {
        $tcp = New-Object System.Net.Sockets.TcpClient
        try {
            $task = $tcp.ConnectAsync([string]$dependency[1], [int]$dependency[2])
            if (!$task.Wait(3000) -or !$tcp.Connected) { throw 'unreachable' }
        } catch { throw "$($dependency[0]) erişilemiyor. Windows servisini, bağlantı adresini ve portunu kontrol edin." }
        finally { $tcp.Dispose() }
    }
    $ports = @($GatewayPort, $CatalogPort, $InventoryPort, $OrdersPort)
    if (@($ports | Select-Object -Unique).Count -ne 4) { throw 'Uygulama portları birbirinden farklı olmalıdır.' }
    foreach ($port in $ports) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port kullanımda: $port" }
    }
    $dataDirectory = Join-Path $root 'artifacts/local/data'
    $logDirectory = Join-Path $root ('artifacts/local/logs/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $dataDirectory, $logDirectory | Out-Null
    if (!$env:ConnectionStrings__Inventory) { Set-LocalEnvironment 'ConnectionStrings__Inventory' "Data Source=$dataDirectory/inventory.db;Default Timeout=30" }
    if (!$env:ConnectionStrings__Orders) { Set-LocalEnvironment 'ConnectionStrings__Orders' "Data Source=$dataDirectory/orders.db;Default Timeout=30" }
    Set-LocalEnvironment 'Catalog__ApplyMigrations' 'true'
    if (!$env:Inventory__SeedDemo) { Set-LocalEnvironment 'Inventory__SeedDemo' 'true' }
    Set-LocalEnvironment 'Catalog__Url' "http://localhost:$CatalogPort"
    Set-LocalEnvironment 'ReverseProxy__Clusters__catalog__Destinations__primary__Address' "http://localhost:$CatalogPort/"
    Set-LocalEnvironment 'ReverseProxy__Clusters__orders__Destinations__primary__Address' "http://localhost:$OrdersPort/"
    Push-Location (Join-Path $root 'src/Web')
    try {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci başarısız.' }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Web derlemesi başarısız.' }
    } finally { Pop-Location }
    & $dotnet build Project1.sln --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    Start-LocalService 'Catalog.Api' (Join-Path $root 'src/Services/Catalog/Catalog.Api') $CatalogPort
    Start-LocalService 'Inventory.Api' (Join-Path $root 'src/Services/Inventory/Inventory.Api') $InventoryPort
    Start-LocalService 'Orders.Api' (Join-Path $root 'src/Services/Orders/Orders.Api') $OrdersPort
    Start-LocalService 'Gateway' (Join-Path $root 'src/Gateway') $GatewayPort
    Write-Host "Arayüz: http://localhost:$GatewayPort. Loglar: $logDirectory. Durdurmak için Ctrl+C."
    if ($ControlDirectory) {
        $ControlDirectory = [IO.Path]::GetFullPath($ControlDirectory)
        New-Item -ItemType Directory -Force $ControlDirectory | Out-Null
        Set-Content (Join-Path $ControlDirectory 'ready') $PID
    }
    if (!$StopAfterReady) {
        while ($true) {
            if ($ControlDirectory -and (Test-Path (Join-Path $ControlDirectory 'request.json'))) {
                $request = Get-Content (Join-Path $ControlDirectory 'request.json') -Raw | ConvertFrom-Json
                Remove-Item -LiteralPath (Join-Path $ControlDirectory 'request.json')
                $result = 'ok'
                try {
                    if ($request.action -eq 'shutdown') { break }
                    foreach ($name in $request.services) {
                        if ($name -eq 'rabbitmq') {
                            if ($request.action -in @('stop','restart')) { & rabbitmqctl.bat stop_app; if ($LASTEXITCODE) { throw 'RabbitMQ stop_app failed' } }
                            if ($request.action -in @('start','restart')) { & rabbitmqctl.bat start_app; if ($LASTEXITCODE) { throw 'RabbitMQ start_app failed' } }
                            continue
                        }
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
    if ($ControlDirectory) { Remove-Item -LiteralPath (Join-Path $ControlDirectory 'ready') -ErrorAction SilentlyContinue }
    foreach ($child in $processes) {
        if (!$child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue; $child.WaitForExit(5000) | Out-Null }
        $child.Dispose()
    }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Pop-Location
}

