# Runbook HireFlow

Алерты задаются правилами Prometheus (`deploy/prometheus/rules`), приходят в Alertmanager
(http://localhost:9093) и, если настроен бот дежурного, в Telegram. В Grafana (http://localhost:3000):
дашборд «HireFlow» (строка «SLO»), Alerting → Alert rules и Alert groups. Описание — design.md, раздел 16.

## SLO (окно 30 дней)

| SLO | Цель | Алерты |
|---|---|---|
| Доступность API | 99,5% ответов без 5xx (без `/health*`) | [ApiErrorBudgetFastBurn](ApiErrorBudgetFastBurn.md), [ApiErrorBudgetSlowBurn](ApiErrorBudgetSlowBurn.md) |
| Задержка API | 95% запросов быстрее 1 с | [ApiLatencyHigh](ApiLatencyHigh.md) |
| Скрининг | 99% откликов проходят скрининг быстрее 60 с | [ScreeningSlow](ScreeningSlow.md) |
| Публичный API | 99% успешных `POST /v1/screenings` с телом до 2 МБ быстрее 1 с | [PublicApiSlow](PublicApiSlow.md) |
| Доставка уведомлений | нет уведомлений, зависших дольше 10 мин | [NotificationsStuck](NotificationsStuck.md), [NotificationsFailed](NotificationsFailed.md) |

## Остальные алерты

| Алерт | Severity | Что значит |
|---|---|---|
| [ApiDown](ApiDown.md) | critical | нет метрик API 3 минуты |
| [BotDown](BotDown.md) | critical | нет метрик бота 5 минут |
| [BotPollingErrors](BotPollingErrors.md) | warning | бот не получает сообщения из Telegram |
| [BotMessagesDropped](BotMessagesDropped.md) | warning | бот отбрасывает сообщения кандидатов |
| [TelemetryCollectorDown](TelemetryCollectorDown.md) | critical | OTel Collector недоступен, телеметрии нет |

Подавления: `TelemetryCollectorDown` гасит `ApiDown`, `BotDown` и SLO-алерты API, включая `PublicApiSlow`
(данных нет); `ApiDown` гасит SLO-алерты API и `PublicApiSlow`.

## Бюджет ошибок

Бюджет доступности — 0,5% запросов за 30 дней. Burn rate — во сколько раз быстрее допустимого он тратится
(1 — бюджет закончится ровно за 30 дней). Алерты считаются в двух окнах, чтобы срабатывать быстро и
гаснуть сразу после исправления:
- `ApiErrorBudgetFastBurn`: burn rate больше 14.4 за 1 ч и за 5 мин (2% бюджета за час), не меньше 20 запросов за час;
- `ApiErrorBudgetSlowBurn`: больше 6 за 6 ч и за 30 мин (5% бюджета за 6 часов), не меньше 50 запросов за 6 ч.

Минимальный трафик защищает от ложных тревог: ночью 1 ошибка из 2 запросов — это 50%, но не авария.
Остаток бюджета — панель «Остаток бюджета ошибок за 30 дней». Пока бюджет есть, можно выкладывать
изменения; когда он исчерпан, сначала исправляем причины ошибок, потом новые функции.
Prometheus хранит данные 7 дней, поэтому панели «за 30 дней» фактически показывают срок хранения.

Плановые работы и известные ложные срабатывания (например, смена границ корзин гистограммы скрининга —
`ScreeningSlow` до часа) — заранее silence в Alertmanager (http://localhost:9093 → New Silence).

## Шаблон runbook

Что значит и влияние → как проверить → частые причины → что делать → когда эскалировать.
