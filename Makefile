# Локальный запуск HireFlow. Настройки берутся из .env (см. .env.example).
#   make run      — база, миграции, затем API и бот вместе (Ctrl+C останавливает оба)
#   make test     — все тесты (нужен запущенный Docker)
#   make help     — список команд

# Загружает .env в окружение команды: dotnet run сам .env не читает.
ENV := set -a; . ./.env; set +a;

.PHONY: help run db migrate api bot test stop reset-db

help:
	@echo "make run       база + миграции + API и бот"
	@echo "make db        поднять PostgreSQL и дождаться готовности"
	@echo "make migrate   применить миграции EF Core"
	@echo "make api       только API (http://localhost:5184)"
	@echo "make bot       только бот"
	@echo "make test      все тесты"
	@echo "make stop      остановить PostgreSQL"
	@echo "make reset-db  удалить базу и создать заново (данные пропадут)"

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

run: migrate
	$(MAKE) -j2 api bot

test:
	dotnet test

stop:
	docker compose stop postgres

reset-db: .env
	docker compose down -v
	$(MAKE) migrate
