# Локальный запуск HireFlow. Настройки берутся из .env (см. .env.example).
#   make run      — база, миграции, затем API, бот и кабинет рекрутера вместе (Ctrl+C останавливает всё)
#   make test     — все тесты (нужен запущенный Docker)
#   make help     — список команд

# Загружает .env в окружение команды: dotnet run сам .env не читает.
ENV := set -a; . ./.env; set +a;

.PHONY: help run db migrate api bot web test stop reset-db recruiter

help:
	@echo "make run       база + миграции + API, бот и кабинет"
	@echo "make db        поднять PostgreSQL и дождаться готовности"
	@echo "make migrate   применить миграции EF Core"
	@echo "make api       только API (http://localhost:5184)"
	@echo "make bot       только бот"
	@echo "make web       только кабинет рекрутера (http://localhost:5200)"
	@echo "make test      все тесты"
	@echo "make recruiter LOGIN=anna  создать рекрутера или сменить ему пароль"
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

# Кабинет рекрутера: секретов нет, .env не нужен.
web:
	dotnet run --project src/HireFlow.Web --launch-profile http

run: migrate
	$(MAKE) -j3 api bot web

test:
	dotnet test

recruiter: migrate
	@test -n "$(LOGIN)" || (echo "Укажите логин: make recruiter LOGIN=anna" && exit 1)
	$(ENV) dotnet run --project src/HireFlow.Api -- recruiter $(LOGIN)

stop:
	docker compose stop postgres

reset-db: .env
	docker compose down -v
	$(MAKE) migrate
