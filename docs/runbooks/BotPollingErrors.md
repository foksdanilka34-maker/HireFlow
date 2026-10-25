# BotPollingErrors (warning)

**Что значит.** За 10 минут больше 5 сбоев `GetUpdates` (метрика `hireflow_bot_polling_errors_total`): бот не
может получать сообщения из Telegram.
**Влияние:** кандидаты пишут боту, а он не отвечает.

## Как проверить
- Grafana, дашборд «HireFlow»: «Ошибки бота, в минуту».
- Тип ошибки в логах (Loki): `{service_name="hireflow-bot"} |= "Polling failed"` или
  `docker compose logs --tail 100 bot`.
- Сколько ботов запущено: `docker compose --profile app ps bot` и нет ли локального `make run`/`make bot`.

## Частые причины
- Запущен второй экземпляр бота с тем же токеном (`make up` и `make run` одновременно) — Telegram отвечает
  конфликтом (409).
- Telegram недоступен или нет сети на хосте.
- Токен отозван (401).

## Что делать
1. Второй экземпляр — остановить лишний (локальный `make run` или `docker compose --profile app stop bot`).
2. Токен отозван — новый токен у @BotFather в `.env`, `docker compose --profile app up -d bot`.
3. Telegram или сеть недоступны — проверить сеть хоста; бот сам повторяет опрос.

## Когда эскалировать
Ошибки больше часа при работающей сети и одном экземпляре — разработчику бота.
