# NotificationsStuck (warning)

**Что значит.** 10 минут подряд в базе есть уведомления в статусе `pending`, у которых `send_after` прошёл
больше 5 минут назад (метрика `hireflow_notifications_overdue`, снимок из БД раз в 30 с).
Уведомления (outbox) рассылает бот: забирает их из API и отправляет кандидату в Telegram.
**Влияние:** кандидат не узнаёт об отказе или приглашении на созвон.

## Как проверить
- Grafana, дашборд «HireFlow»: «Уведомления: просроченные и упавшие».
- Жив ли бот: горит ли `BotDown`, `docker compose --profile app ps bot`, `docker compose logs --tail 100 bot`.
- Ошибки рассылки в логах бота (Loki): `{service_name="hireflow-bot"} |~ "(?i)notification"`.
- Зависшие уведомления в базе:
  `docker compose exec postgres psql -U hireflow -d hireflow -c "select id, type, attempts, send_after, last_error from notifications where status = 'pending' and send_after < now() - interval '5 minutes' order by send_after limit 20"`

## Частые причины
- Бот остановлен или упал (тогда горит и `BotDown`).
- Бот не может достучаться до API (`ApiDown`, неверный `Bot__ApiKey`/`BotApi__ApiKey` в `.env`).
- Telegram недоступен: уведомления уходят в повтор (2, 4, 8… минут), `last_error` заполнен.

## Что делать
1. Бот не запущен — `docker compose --profile app up -d bot`.
2. API недоступен — runbook [ApiDown](ApiDown.md).
3. Ошибки Telegram — дождаться повторов; после 5 неудачных попыток уведомление станет `failed`
   (алерт [NotificationsFailed](NotificationsFailed.md)).

## Когда эскалировать
Бот и API работают, а уведомления не уходят больше 30 минут — разработчику бота.
