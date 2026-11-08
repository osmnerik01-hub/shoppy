param(
    [ValidateSet('stop','start','restart','shutdown')][string]$Action,
    [string]$Services,
    [string]$ControlDirectory = $env:LOCAL_CONTROL_DIRECTORY
)
$ErrorActionPreference = 'Stop'
$directory = $ControlDirectory
if (!$directory -or !(Test-Path (Join-Path $directory 'ready'))) { throw 'Controlled local launcher is not running.' }
# Capture original process handles before requesting shutdown (also supports older launchers).
$owned = @()
if ($Action -eq 'shutdown') {
    $launcherId = [int](Get-Content (Join-Path $directory 'ready') -Raw)
    if (!(Get-Process -Id $launcherId -ErrorAction SilentlyContinue)) { throw 'Kontrol oturumunun süreci çalışmıyor; eski ready işaretini çalışan uygulamalarla eşleştirmeden yeniden başlatmayın. Farklı portlar ve yeni bir ControlDirectory kullanın.' }
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $owned = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $launcherId" |
        Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine.Contains("$projectRoot\src\") } |
        ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })
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

