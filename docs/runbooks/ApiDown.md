# ApiDown (critical)

**Что значит.** Около 3 минут в Prometheus нет ни одной точки `http_server_request_duration_seconds_count`
от `hireflow-api`. Healthcheck Docker обращается к `/health/ready` каждые 15 с, поэтому у живого API эти
ряды есть всегда: API не работает или не отправляет телеметрию.
**Влияние:** бот не принимает отклики и не подбирает вакансии, кабинет рекрутера не работает, уведомления
не уходят.

## Как проверить
- `docker compose --profile app ps api` — запущен ли, `healthy` ли.
- `docker compose logs --tail 100 api` — ошибка старта, падение.
- `curl -s http://localhost:8080/api/health/ready` — отвечает ли API через Caddy.
- Не горит ли `TelemetryCollectorDown`: если коллектор лежит, API может работать, но метрик нет.

## Частые причины
- Контейнер API упал или перезапускается (ошибка конфигурации в `.env`, нет базы).
- Миграции не применились: `docker compose logs migrate` (API стартует только после успешного `migrate`).
- PostgreSQL недоступен: `docker compose ps postgres`.

## Что делать
1. Запустить: `docker compose --profile app up -d api`.
2. Ошибка миграций или конфигурации — исправить по логам и повторить.
3. API отвечает, а метрик нет — проверить `docker compose logs --tail 100 otel-collector`.

## Когда эскалировать
API не поднимается за 15 минут — разработчику API.
