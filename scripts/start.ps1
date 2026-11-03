$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)

function New-Secret {
    $bytes = New-Object byte[] 24
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ([BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}
function Test-Docker {
    try {
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
        return $LASTEXITCODE -eq 0
    } catch { return $false } finally { $ErrorActionPreference = $oldPreference }
}

try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Установите Docker Desktop и повторите запуск.' }
    if (-not (Test-Docker)) {
        $dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (-not (Test-Path -LiteralPath $dockerDesktop)) { throw 'Запустите Docker Desktop и повторите запуск.' }
        Write-Host 'Запускаем Docker Desktop...'
        Start-Process -FilePath $dockerDesktop -WindowStyle Hidden
        $deadline = (Get-Date).AddMinutes(3)
        do { Start-Sleep -Seconds 2 } until ((Test-Docker) -or (Get-Date) -ge $deadline)
        if (-not (Test-Docker)) { throw 'Docker Desktop не запустился за 3 минуты. Проверьте его и повторите запуск.' }
    }

    if (-not (Test-Path -LiteralPath '.env')) {
        Write-Host 'Первый запуск: настройка Telegram-бота и локальной модели.'
        $tokenSecure = Read-Host 'Токен бота от BotFather' -AsSecureString
        $tokenPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($tokenSecure)
        try { $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPtr).Trim() }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPtr) }
        if ($token -notmatch '^\d+:[A-Za-z0-9_-]+$') { throw 'Некорректный токен бота. Запустите команду снова.' }
        try { $botName = (Invoke-RestMethod -Uri "https://api.telegram.org/bot$token/getMe" -TimeoutSec 15).result.username }
        catch { throw 'Не удалось проверить токен бота через Telegram. Проверьте токен и соединение.' }
        if ($botName -notmatch '^[A-Za-z0-9_]{5,32}$') { throw 'Telegram не вернул имя бота. Проверьте токен.' }
        $llama = 'J:/Apps/llama.cpp/llama-server.exe'
        $model = 'J:/Apps/llama.cpp/models/gemma-4-E4B_q4_0-it.gguf'
        if (-not (Test-Path -LiteralPath $llama -PathType Leaf)) { $llama = (Read-Host 'Полный путь к llama-server.exe').Trim(' ', '"') }
        if (-not (Test-Path -LiteralPath $model -PathType Leaf)) { $model = (Read-Host 'Полный путь к файлу модели GGUF').Trim(' ', '"') }
        if (-not (Test-Path -LiteralPath $llama -PathType Leaf) -or -not (Test-Path -LiteralPath $model -PathType Leaf)) { throw 'Файл llama-server или модели не найден.' }
        if ($llama -match '[\r\n"$]' -or $model -match '[\r\n"$]') { throw 'Путь содержит неподдерживаемые символы.' }
        $password = New-Secret
        $botKey = New-Secret
        $grafana = New-Secret
        $config = Get-Content -LiteralPath '.env.example' -Raw -Encoding UTF8
        $config = $config.Replace('POSTGRES_PASSWORD=hireflow-local', "POSTGRES_PASSWORD=$password")
        $config = $config.Replace('Password=hireflow-local', "Password=$password")
        $config = $config.Replace('BotApi__ApiKey=hireflow-local-bot-key', "BotApi__ApiKey=$botKey")
        $config = $config.Replace('Bot__ApiKey=hireflow-local-bot-key', "Bot__ApiKey=$botKey")
        $config = $config.Replace('Bot__TelegramToken=123456789:replace-with-token-from-botfather', "Bot__TelegramToken=$token")
        $config = $config.Replace('GRAFANA_ADMIN_PASSWORD=change-me-local', "GRAFANA_ADMIN_PASSWORD=$grafana")
        $config = $config.Replace('AiReview__Enabled=false', 'AiReview__Enabled=true')
        $config = $config.Replace('AI_LLAMA_EXE=J:/Apps/llama.cpp/llama-server.exe', ('AI_LLAMA_EXE="' + $llama.Replace('\', '/') + '"'))
        $config = $config.Replace('AI_MODEL_PATH=J:/Apps/llama.cpp/models/gemma-4-E4B_q4_0-it.gguf', ('AI_MODEL_PATH="' + $model.Replace('\', '/') + '"'))
        $config += "`r`nBOT_USERNAME=$botName`r`n"
        [IO.File]::WriteAllText((Join-Path (Get-Location) '.env'), $config, (New-Object Text.UTF8Encoding($false)))
        $token = $null
        Write-Host 'Настройки сохранены в .env.'
    }

    $settings = Get-Content -LiteralPath '.env' -Encoding UTF8
    if (-not ($settings -match '^\s*Bot__TelegramToken=\d+:[A-Za-z0-9_-]+\s*$') -or ($settings -match 'replace-with-token-from-botfather')) {
        throw 'Укажите действительный Bot__TelegramToken в .env.'
    }
    if (-not ($settings -match '^\s*AiReview__Enabled=true\s*$')) { throw 'Чтобы запускать модель, установите AiReview__Enabled=true в .env.' }
    & "$PSScriptRoot\start-ai.ps1"
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось запустить локальную модель.' }
    Write-Host 'Запускаем HireFlow. При первом запуске загрузка и сборка могут занять несколько минут...'
    & docker compose --profile app up -d --build
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось запустить контейнеры HireFlow.' }

    $count = & docker compose exec -T postgres psql -U hireflow -d hireflow -tAc 'SELECT count(*) FROM recruiters'
    if ($LASTEXITCODE -ne 0 -or ($count.Trim() -notmatch '^\d+$')) { throw 'Не удалось проверить учётные записи рекрутеров.' }
    if ([int]$count.Trim() -eq 0) {
        Write-Host 'Создайте первую учётную запись рекрутера.'
        $login = Read-Host 'Логин'
        & docker compose exec api dotnet HireFlow.Api.dll recruiter $login
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать рекрутера; запустите команду снова.' }
    }
    Write-Host 'HireFlow готов: http://localhost:8080'
    Start-Process 'http://localhost:8080'
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}
