$ErrorActionPreference = 'Stop'
$pidPath = Join-Path $env:TEMP 'hireflow-llama.pid'
if (-not (Test-Path -LiteralPath $pidPath)) { Write-Host 'Нет сервера ИИ, запущенного скриптом HireFlow.'; exit 0 }
$serverId = 0
if (-not [int]::TryParse((Get-Content -LiteralPath $pidPath -Raw).Trim(), [ref]$serverId)) { throw 'Некорректный файл PID сервера ИИ.' }
$process = Get-CimInstance Win32_Process -Filter "ProcessId = $serverId"
if ($null -eq $process) { Remove-Item -LiteralPath $pidPath; Write-Host 'Сервер ИИ уже остановлен.'; exit 0 }
if ($process.Name -ine 'llama-server.exe' -or $process.CommandLine -notmatch '--port\s+18081' -or $process.CommandLine -notmatch '--host\s+127\.0\.0\.1') {
    throw 'PID занят другим процессом; остановка отменена.'
}
Stop-Process -Id $serverId
Remove-Item -LiteralPath $pidPath
Write-Host 'Локальный сервер ИИ остановлен.'
