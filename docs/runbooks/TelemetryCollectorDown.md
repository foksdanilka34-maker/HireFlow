# TelemetryCollectorDown (critical)

**Что значит.** Prometheus 2 минуты не может опросить собственные метрики OTel Collector
(`up{job="otel-collector"} == 0`). Через коллектор идут все метрики, трейсы и логи API и бота.
**Влияние:** на кандидатов и рекрутеров напрямую не влияет, но мы слепы: дашборды пустые, алерты по метрикам
API и бота не срабатывают или срабатывают ложно (`ApiDown`, `BotDown` и SLO-алерты подавлены этим алертом).

## Как проверить
- `docker compose --profile app ps otel-collector`, `docker compose logs --tail 100 otel-collector`.
- Prometheus → Status → Targets (http://localhost:9090/targets): цель `otel-collector:8888`.
- Работают ли сами сервисы: `curl -s http://localhost:8080/api/health/ready`, написать боту.

## Частые причины
- Коллектор упал или остановлен.
- Ошибка в `deploy/otel-collector.yml` после правки (коллектор не стартует, причина в логах).
- Не хватает памяти (срабатывает `memory_limiter`, контейнер перезапускается).

## Что делать
1. Запустить: `docker compose --profile app up -d otel-collector`.
2. Ошибка конфигурации — исправить `deploy/otel-collector.yml` по логам и перезапустить.
3. После восстановления проверить, что погасли подавленные алерты, а не просто скрыты.

## Когда эскалировать
Коллектор не поднимается за 15 минут — тому, кто отвечает за стенд.
