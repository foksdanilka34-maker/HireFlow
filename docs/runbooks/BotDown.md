# BotDown (critical)

**Что значит.** Около 5 минут в Prometheus нет рантайм-метрик бота
(`dotnet_process_cpu_time_seconds_total{service_name="hireflow-bot"}`). Бот отправляет их каждые 15 с даже
без сообщений, поэтому бот не работает или не отправляет телеметрию.
**Влияние:** кандидаты не получают ответов в Telegram, уведомления (outbox) не рассылаются.

## Как проверить
- `docker compose --profile app ps bot`, `docker compose logs --tail 100 bot`.
- Не горит ли `TelemetryCollectorDown`: тогда бот может работать, но метрик нет.
- Написать боту в Telegram — отвечает ли.

## Частые причины
- Бот не запущен (стенд поднят без него) или упал.
- Неверный или отозванный `Bot__TelegramToken` в `.env`.
- Бот ждёт API: стартует только после `healthy` у `api` (см. [ApiDown](ApiDown.md)).

## Что делать
1. Запустить: `docker compose --profile app up -d bot`.
2. Ошибка токена в логах — получить новый у @BotFather, вписать в `.env`, повторить.
3. Не запускать второй экземпляр бота с тем же токеном (`make run` вместе с `make up`): см.
   [BotPollingErrors](BotPollingErrors.md).

## Когда эскалировать
Бот не поднимается за 15 минут — разработчику бота.
