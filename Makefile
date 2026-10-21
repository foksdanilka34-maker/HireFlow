# Локальный запуск HireFlow. Настройки берутся из .env (см. .env.example).
#   make run      — база, миграции, затем API, бот и кабинет рекрутера вместе (Ctrl+C останавливает всё)
#   make test     — все тесты (нужен запущенный Docker)
#   make check    — те же проверки, что в CI
#   make up       — весь стенд в Docker, кабинет на http://localhost:8080
#   make help     — список команд

# Загружает .env в окружение команды: dotnet run сам .env не читает.
ENV := set -a; . ./.env; set +a;

.PHONY: help run db migrate api bot web test check stop reset-db recruiter up down logs docker-recruiter

help:
	@echo "make run       база + миграции + API, бот и кабинет"
	@echo "make db        поднять PostgreSQL и дождаться готовности"
	@echo "make migrate   применить миграции EF Core"
	@echo "make api       только API (http://localhost:5184)"
	@echo "make bot       только бот"
	@echo "make web       только кабинет рекрутера (http://localhost:5200)"
	@echo "make test      все тесты"
	@echo "make check     проверки как в CI: сборка без предупреждений, уязвимые пакеты, миграции, тесты"
	@echo "make recruiter LOGIN=anna  создать рекрутера или сменить ему пароль"
	@echo "make stop      остановить PostgreSQL"
	@echo "make reset-db  удалить базу и создать заново (данные пропадут)"
	@echo ""
	@echo "Стенд в Docker:"
	@echo "make up        собрать образы и поднять стенд (кабинет: http://localhost:8080)"
	@echo "make down      остановить стенд и PostgreSQL (база для make run тоже; данные в volume сохраняются)"
	@echo "make logs      логи стенда"
	@echo "make docker-recruiter LOGIN=anna  создать рекрутера в стенде"
	@echo "Не запускайте make up и make run одновременно: два бота с одним токеном мешают друг другу."

# Создаёт .env из примера при первом запуске и просит вписать токен бота.
.env:
	cp .env.example .env
	@echo "Создан .env — впишите в него Bot__TelegramToken от @BotFather и запустите команду снова." && exit 1

db: .env
	docker compose up -d --wait postgres

migrate: db
	dotnet tool restore
	$(ENV) dotnet ef database update --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api

api: .env
	$(ENV) dotnet run --project src/HireFlow.Api --launch-profile http

bot: .env
	@grep -q "replace-with-token" .env && echo "В .env не задан Bot__TelegramToken" && exit 1 || true
	$(ENV) dotnet run --project src/HireFlow.Bot

# Кабинет рекрутера: секретов нет, .env не нужен.
web:
	dotnet run --project src/HireFlow.Web --launch-profile http

run: migrate
	$(MAKE) -j3 api bot web

test:
	dotnet test

# Те же шаги, что job build-test в .github/workflows/ci.yml. dotnet list package всегда возвращает 0,
# поэтому уязвимости ищем по тексту английского вывода.
check:
	dotnet restore HireFlow.slnx
	dotnet build HireFlow.slnx -c Release --no-restore --no-incremental -warnaserror
	@out=$$(DOTNET_CLI_UI_LANGUAGE=en dotnet list HireFlow.slnx package --vulnerable --include-transitive) || exit 1; \
	echo "$$out"; \
	if echo "$$out" | grep -q "has the following vulnerable packages"; then echo "Найдены уязвимые пакеты"; exit 1; fi
	dotnet tool restore
	ConnectionStrings__Postgres="Host=localhost;Database=hireflow;Username=hireflow;Password=x" BotApi__ApiKey=x \
		dotnet ef migrations has-pending-model-changes --project src/HireFlow.Infrastructure --startup-project src/HireFlow.Api --no-build --configuration Release
	dotnet test HireFlow.slnx -c Release --no-build

recruiter: migrate
	@test -n "$(LOGIN)" || (echo "Укажите логин: make recruiter LOGIN=anna" && exit 1)
	$(ENV) dotnet run --project src/HireFlow.Api -- recruiter $(LOGIN)

stop:
	docker compose stop postgres

reset-db: .env
	docker compose down -v
	$(MAKE) migrate

# Весь стенд в Docker: миграции, API, бот и кабинет за Caddy.
up: .env
	@grep -q "replace-with-token" .env && echo "В .env не задан Bot__TelegramToken" && exit 1 || true
	docker compose --profile app up -d --build
	@echo "Кабинет: http://localhost:8080"

down:
	docker compose --profile app down

logs:
	docker compose --profile app logs -f

docker-recruiter: .env
	@test -n "$(LOGIN)" || (echo "Укажите логин: make docker-recruiter LOGIN=anna" && exit 1)
	docker compose --profile app run --rm -it api recruiter $(LOGIN)
