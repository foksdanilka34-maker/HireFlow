# Учения: пауза PostgreSQL под нагрузкой

**Цель.** Проверить, что при недоступной базе алерты срабатывают вовремя и ведут к причине, API не падает
и сам восстанавливается после возврата базы, а данные не теряются. По итогам — постмортем
(`docs/postmortems/<дата>-postgres-pause.md` по [шаблону](../postmortems/README.md)).

**Сбой.** `docker compose pause postgres`: контейнер замораживается, соединения висят без ответа (как при
зависшем сервере или потере сети). Данные и volume не трогаются.

## Гипотеза

- `ApiErrorBudgetFastBurn` срабатывает через ~3–5 мин после паузы, гаснет через ~4–5 мин после её снятия.
- `ApiLatencyHigh` (окно 15 мин, `for` 5 мин) — через ~6–7 мин после паузы, в конце паузы или сразу после;
  гаснет только через ~15 мин после снятия паузы, позже конца `RECOVERY`.
- `ApiErrorBudgetSlowBurn` (окна 6 ч и 30 мин, `for` 15 мин) — через ~16–17 мин после начала паузы, при значениях
  по умолчанию уже после конца скрипта; гаснет через ~15–30 мин.
- `ApiDown` и `TelemetryCollectorDown` не срабатывают: API жив и шлёт метрики.
- `/health/ready` отвечает 503, контейнер `api` становится unhealthy.
- Бот отвечает кандидатам «Что-то пошло не так».
- После снятия паузы API восстанавливается за ~1 мин без перезапуска, данные целы; алерты гаснут по срокам выше,
  часть — уже после конца скрипта.

## Запуск

Стенд поднят (`make up`, `api` healthy), рядом открыты Grafana и Alertmanager. Учения ставят на паузу
PostgreSQL стенда: на время учений бот и кабинет не работают.

```
make drill
RATE=5 BASELINE=2m OUTAGE=8m RECOVERY=6m make drill
```

| Переменная | По умолчанию | Что задаёт |
|---|---|---|
| `RATE` | 10 | итераций k6 в секунду |
| `BASELINE` | 3m | нагрузка до сбоя (включая минуту разгона k6) |
| `OUTAGE` | 6m | сколько база на паузе |
| `RECOVERY` | 6m | наблюдение после снятия паузы |

Скрипт `deploy/drill.sh`: проверяет, что `api` healthy и postgres не на паузе, запускает k6 в фоне на всё
время учений, ждёт `BASELINE`, ставит паузу на `OUTAGE`, снимает её и ждёт `RECOVERY` и конца k6.
k6 идёт ещё ~1 мин после `RECOVERY` (минута разгона). Пороги k6 при учениях ожидаемо не выполняются — код выхода
k6 только записывается в лог. Если k6 завершился до паузы, пауза не ставится.

Шаги с метками времени UTC — в консоли и `load/results/drill-<время>.log`, вывод k6 —
`load/results/drill-<время>-k6.log` (каталог в `.gitignore`, ключей и ПД там нет).

## Что смотреть

- Grafana (http://localhost:3000), дашборд «HireFlow»: строка «SLO» (burn rate, доля быстрых запросов),
  «HTTP API: доля 5xx», «HTTP API: запросы/с по маршруту».
- Alertmanager (http://localhost:9093): когда пришли и когда погасли алерты.
- Tempo (Grafana → Explore → Tempo): долгие спаны Npgsql и запросы, упавшие по таймауту.
- Loki (Grafana → Explore → Loki): `{service_name="hireflow-api"} | detected_level="error"`.
- `docker compose --profile app ps`: статус `api` (unhealthy во время паузы, healthy после).

## Как прервать

Ctrl+C или закрытие терминала: скрипт сам снимает паузу с postgres и останавливает k6. Если скрипт убит иначе
(например, `kill -9`) —
`docker compose unpause postgres` вручную (проверить: `docker compose ps postgres`, статус не `paused`).

## Очистка

Подбор вакансий пишет строки лимита загрузок для тестовых Telegram ID. После учений и нагрузочных тестов:

```
docker compose exec postgres psql -U hireflow -d hireflow -c "DELETE FROM resume_uploads WHERE telegram_id >= 990000000000"
```

## Таймлайн для постмортема

Метки шагов — из лога учений. Когда горели алерты — ряд `ALERTS` в Prometheus (http://localhost:9090,
вкладка Graph, интервал учений): `ALERTS{alertstate="firing"}` (горит) и `ALERTS{alertstate="pending"}`
(условие выполнено, ждёт `for`). Интервал — от начала учений примерно до 40 мин после начала паузы: `SlowBurn` и
`ApiLatencyHigh` гаснут уже после конца скрипта. То же через API, время — в UTC:

```
curl -s http://localhost:9090/api/v1/query_range \
  --data-urlencode 'query=ALERTS{alertstate="firing"}' \
  --data-urlencode 'start=2026-10-22T10:00:00Z' --data-urlencode 'end=2026-10-22T10:45:00Z' \
  --data-urlencode 'step=15s'
```

Первая и последняя точка каждого `alertname` — начало и конец алерта. Время обнаружения — от паузы до
первой точки `firing`. Влияние — запросы и 5xx за интервал учений:
`sum(increase(http_server_request_duration_seconds_count{service_name="hireflow-api", http_route!~"/health.*"}[<интервал>]))`
и то же с `http_response_status_code=~"5.."`; сводка k6 — из лога k6.
