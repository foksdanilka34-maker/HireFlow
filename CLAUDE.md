## Команды
- Сборка: `dotnet build`
- Тесты: `dotnet test`
- Один проект или тест: `dotnet test tests/<Проект>.Tests --filter "FullyQualifiedName~<Имя>"`
- Команды запускаются из корня (решение `HireFlow.slnx`). Интеграционным тестам нужен запущенный Docker.
- Миграции EF Core (сначала `dotnet tool restore`; строка подключения не хранится в репозитории, поэтому для команд без БД
  достаточно фиктивной):
  `ConnectionStrings__Postgres="Host=localhost;Database=hireflow;Username=hireflow;Password=x" dotnet ef migrations add <Имя> --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api --output-dir Persistence/Migrations`
- Проверка, что модель и миграции синхронны: та же строка подключения + `dotnet ef migrations has-pending-model-changes --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api`

## Тесты
- Фреймворк: xUnit, тесты в tests/<Проект>.Tests
- Assertions — встроенный `Assert` из xUnit, сторонние библиотеки не подключаем.
- `tests/HireFlow.Domain.Tests` — unit-тесты домена: скоринг, поиск навыков, переходы статусов, уведомления.
- `tests/HireFlow.Application.Tests` — unit-тесты логики Application без БД (например, `ResumeReader` с фейковым экстрактором).
- `tests/HireFlow.Api.Tests` — интеграционные: `WebApplicationFactory<Program>` + Testcontainers PostgreSQL с миграциями.
  Здесь проверяются сервисы, которые работают через `IAppDbContext`, EF-конфигурации и HTTP-эндпоинты.
- Domain.Tests и Application.Tests пока не созданы. Их создаёт задача, в PLAN.md которой это записано.
- EF InMemory и SQLite не используем: схема завязана на PostgreSQL (`uuidv7()`, JSON-колонки, позже `FOR UPDATE SKIP LOCKED`).

## Архитектура
Полное описание в `design.md`; реализация идёт по этапам из раздела 17, функциональность следующих этапов заранее не добавляем.
- Слои: `Domain` (без зависимостей) ← `Application` (сервисы, DTO, интерфейсы `IAppDbContext`, `IFileStorage`…) ←
  `Infrastructure` (EF Core/Npgsql, файлы, извлечение текста) ← `Api` (minimal API). `HireFlow.Bot` и будущий `HireFlow.Web`
  ходят только в HTTP API и не ссылаются на Application, Infrastructure или БД.
- Бизнес-правила и переходы статусов находятся в доменных сущностях: приватные сеттеры, методы бросают `DomainException`.
  Каждая смена статуса проходит через сущность, пишет историю (`ApplicationStatusChange`) и сама создаёт `Notification`
  (outbox), а сохраняется одним `SaveChangesAsync`. Сервисы уведомления напрямую не создают.
- Ошибки: `NotFoundException` → 404, `BadRequestException` → 400, `DomainException` → 409 (`AppExceptionHandler`).
- Enum в API и БД пишутся в snake_case (`recruiter_review`): в БД через `SnakeCaseEnumConverter`, в JSON через глобальный конвертер.
- БД: snake_case naming convention, ключи `uuidv7()`, конфигурации в `Infrastructure/Persistence/Configurations`.
  После изменения модели нужна миграция.
- Регистрация в DI: `AddApplication()` / `AddInfrastructure()`; эндпоинты через `Map*Endpoints()` в `Program.cs`;
  настройки — options-класс с `SectionName` + `appsettings.json` и `appsettings.Development.json`.
- Время только UTC. Весь I/O асинхронный, `CancellationToken` передаётся до EF и файлов.
- Правила домена: ИИ только рекомендует и никогда не отказывает; автоотказ только по правилам скрининга. В подборе
  показываются только вакансии с вердиктом «прошёл», резюме при подборе не сохраняется. Текст резюме и ответов не пишется в логи.
- Над методами однострочный комментарий на русском, что делает метод, как в существующем коде.
