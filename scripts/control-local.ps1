param(
    [ValidateSet('stop','start','restart','shutdown')][string]$Action,
    [string]$Services
)
$ErrorActionPreference = 'Stop'
$directory = $env:LOCAL_CONTROL_DIRECTORY
if (!$directory -or !(Test-Path (Join-Path $directory 'ready'))) { throw 'Controlled local launcher is not running.' }
$response = Join-Path $directory 'response.txt'
Remove-Item -LiteralPath $response -ErrorAction SilentlyContinue
$request = @{ action=$Action; services=@(if ($Services) { $Services.Split(',') | Where-Object { $_ } }) } | ConvertTo-Json
$temp = Join-Path $directory 'request.tmp'
Set-Content $temp $request
Move-Item -LiteralPath $temp -Destination (Join-Path $directory 'request.json')
if ($Action -eq 'shutdown') { return }
$deadline = [DateTime]::UtcNow.AddSeconds(210)
while (!(Test-Path $response)) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Local control request timed out.' }
    Start-Sleep -Milliseconds 200
}
$result = (Get-Content $response -Raw).Trim()
if ($result -ne 'ok') { throw $result }
