param([string]$EnvFile = '.env')
$ErrorActionPreference = 'Stop'
$config = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding UTF8) {
    if ($line -match '^\s*(AiReview__Model|AI_LLAMA_EXE|AI_MODEL_PATH)=(.*)$') {
        $config[$Matches[1]] = $Matches[2].Trim().Trim('"', "'")
    }
}
$modelName = if ($config['AiReview__Model']) { $config['AiReview__Model'] } else { 'gemma-4-e4b' }
function Test-AiServer {
    try {
        $models = Invoke-RestMethod -Uri 'http://127.0.0.1:18081/v1/models' -TimeoutSec 2
        return @($models.data | Where-Object { $_.id -eq $modelName }).Count -gt 0
    } catch { return $false }
}
if (Test-AiServer) { Write-Host 'Локальный сервер ИИ уже запущен.'; exit 0 }
if (Get-NetTCPConnection -LocalPort 18081 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Порт 18081 уже занят другим сервером; проверьте его перед запуском.'
}
$exe = $config['AI_LLAMA_EXE']
$model = $config['AI_MODEL_PATH']
if (-not $exe -or -not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Укажите AI_LLAMA_EXE в .env.' }
if (-not $model -or -not (Test-Path -LiteralPath $model -PathType Leaf)) { throw 'Укажите AI_MODEL_PATH в .env.' }
$stdout = Join-Path $env:TEMP 'hireflow-llama-out.log'
$stderr = Join-Path $env:TEMP 'hireflow-llama-err.log'
$argsList = @('-m', ('"' + $model + '"'), '--host', '127.0.0.1', '--port', '18081', '--ctx-size', '16000', '--parallel', '1', '-ngl', '99', '--alias', $modelName)
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -ArgumentList $argsList -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -WindowStyle Hidden
$process.Id | Set-Content -LiteralPath (Join-Path $env:TEMP 'hireflow-llama.pid') -Encoding ascii
for ($i = 0; $i -lt 90; $i++) {
    if (Test-AiServer) { Write-Host 'Локальный сервер ИИ готов.'; exit 0 }
    if ($process.HasExited) { throw "llama-server завершился с кодом $($process.ExitCode); журнал: $stderr" }
    Start-Sleep -Seconds 1
}
throw "llama-server не запустился за 90 секунд; журнал: $stderr"
