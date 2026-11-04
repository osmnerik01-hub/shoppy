param([switch]$System)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$originalPath = $env:PATH
$originalDotnetRoot = $env:DOTNET_ROOT
$localSdk = Join-Path $env:LOCALAPPDATA 'Project1\dotnet'
$launcher = $null
$previousControl = $env:LOCAL_CONTROL_DIRECTORY
Push-Location $root
try {
    # Use the same local SDK installation as start-local.ps1, when available.
    if (Test-Path (Join-Path $localSdk 'dotnet.exe')) {
        $env:DOTNET_ROOT = $localSdk
        $env:PATH = "$localSdk;$env:PATH"
    }
    if (!$env:CATALOG_TEST_POSTGRES) { throw 'CATALOG_TEST_POSTGRES must point to a local PostgreSQL account with CREATEDB permission.' }
    dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'global.json ile uyumlu .NET 10 SDK kurulmalıdır.' }
    dotnet build Project1.sln
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    # Integration fixtures manage their own broker settings; do not inherit the app URI.
    $previousRabbitMqUri = $env:RabbitMQ__Uri
    try {
        Remove-Item Env:RabbitMQ__Uri -ErrorAction SilentlyContinue
        dotnet test Project1.sln --no-build --logger trx --results-directory artifacts/dotnet
        if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız.' }
    } finally {
        $env:RabbitMQ__Uri = $previousRabbitMqUri
    }
    Push-Location (Join-Path $root 'src/Web')
    try {
        foreach ($arguments in @(@('ci'), @('run','build'), @('exec','playwright','install','chromium'), @('test'), @('run','test:e2e'))) {
            & npm.cmd @arguments
            if ($LASTEXITCODE) { throw "Web verification failed: npm $arguments" }
        }
    } finally { Pop-Location }
    if ($System) {
        if (!(Get-Command rabbitmqctl.bat -ErrorAction SilentlyContinue)) { throw 'RabbitMQ sbin must be on PATH; run as the RabbitMQ node owner with its Erlang cookie.' }
        $env:LOCAL_CONTROL_DIRECTORY = Join-Path $root ('artifacts/local/control/' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force $env:LOCAL_CONTROL_DIRECTORY | Out-Null
        $launcher = Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File', "`"$root/scripts/start-local.ps1`"", '-ControlDirectory', "`"$env:LOCAL_CONTROL_DIRECTORY`"") -RedirectStandardOutput (Join-Path $env:LOCAL_CONTROL_DIRECTORY 'launcher.out.log') -RedirectStandardError (Join-Path $env:LOCAL_CONTROL_DIRECTORY 'launcher.err.log') -PassThru
        $deadline = [DateTime]::UtcNow.AddMinutes(10)
        while (!(Test-Path (Join-Path $env:LOCAL_CONTROL_DIRECTORY 'ready'))) {
            if ($launcher.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw "Local system startup failed. Logs: $env:LOCAL_CONTROL_DIRECTORY" }
            Start-Sleep -Seconds 1
        }
        Push-Location (Join-Path $root 'src/Web')
        try {
            & npx.cmd playwright test --config playwright.system.config.cjs
            if ($LASTEXITCODE) { throw 'Local system tests failed.' }
        } finally { Pop-Location }
    }

}
finally {
    if ($launcher -and !$launcher.HasExited -and (Test-Path (Join-Path $env:LOCAL_CONTROL_DIRECTORY 'ready'))) {
        try { & "$PSScriptRoot/control-local.ps1" -Action start -Services rabbitmq }
        finally {
            & "$PSScriptRoot/control-local.ps1" -Action shutdown
            if (!$launcher.WaitForExit(15000)) { Write-Warning 'Launcher did not stop; inspect local logs.' }
        }
    }
    $env:LOCAL_CONTROL_DIRECTORY = $previousControl
    $env:PATH = $originalPath
    $env:DOTNET_ROOT = $originalDotnetRoot
    Pop-Location
}
