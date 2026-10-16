# HireFlow

Система первичного отбора кандидатов. Описание системы — в [design.md](design.md).

## Структура

```
src/
  HireFlow.Domain/          сущности и бизнес-правила, ни от чего не зависит
  HireFlow.Application/     сценарии (сервисы), зависит от Domain
  HireFlow.Infrastructure/  EF Core + PostgreSQL, миграции; зависит от Application
  HireFlow.Api/             HTTP API, точка входа; зависит от Infrastructure
  HireFlow.Bot/             Telegram-бот (позже)
```

## Первый запуск

1. Пароль для локального Postgres:
   ```
   cp .env.example .env        # и вписать пароль в POSTGRES_PASSWORD
   ```
2. Поднять Postgres:
   ```
   docker compose up -d
   ```
3. Строка подключения для API (хранится вне репозитория, в user-secrets):
   ```
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=hireflow;Username=hireflow;Password=<пароль из .env>" --project src/HireFlow.Api
   ```
4. Восстановить локальные утилиты (dotnet-ef) и применить миграции:
   ```
   dotnet tool restore
   dotnet ef database update --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api -- --environment Development
   ```
5. Запустить API:
   ```
   dotnet run --project src/HireFlow.Api
   ```

## Миграции

Создать миграцию после изменения сущностей или конфигураций:
```
dotnet ef migrations add <Имя> --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api --output-dir Persistence/Migrations -- --environment Development
```

Флаг `-- --environment Development` нужен, чтобы приложение прочитало строку подключения из user-secrets: они подключаются только в окружении Development.

Посмотреть таблицу в базе:
```
docker compose exec postgres psql -U hireflow -d hireflow -c '\d vacancies'
```
