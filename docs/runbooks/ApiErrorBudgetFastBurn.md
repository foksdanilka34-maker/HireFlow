# ApiErrorBudgetFastBurn (critical)

**Что значит.** Доля ответов API с 5xx (без `/health*`) больше 7,2% (burn rate 14.4) и за последний час, и за
последние 5 минут, при не меньше 20 запросах за час. С такой скоростью 2% месячного бюджета ошибок уходят за час.
**Влияние:** у кандидатов бот отвечает «что-то пошло не так» на отклик и подбор вакансий, у рекрутеров не
открывается или не сохраняется кабинет.

## Как проверить
- Grafana, дашборд «HireFlow»: «Burn rate API», «HTTP API: доля 5xx», «HTTP API: запросы/с по маршруту».
- Какие маршруты отвечают 5xx (Prometheus, http://localhost:9090):
  `sum by (http_route, http_response_status_code) (rate(http_server_request_duration_seconds_count{service_name="hireflow-api", http_response_status_code=~"5.."}[5m]))`
- Логи ошибок (Grafana → Explore → Loki): `{service_name="hireflow-api"} | detected_level="error"`; из лога —
  переход в трейс (Tempo) по `trace_id`.
- Состояние: `docker compose --profile app ps`, `docker compose logs --tail 100 api`,
  `curl -s http://localhost:8080/api/health/ready`.

## Частые причины
- PostgreSQL недоступен или перегружен (`/health/ready` отвечает 503, в логах ошибки Npgsql).
- Ошибка в новой версии после выкладки.
- Кончилось место на диске (volume `resumes`, база).

## Что делать
1. База: `docker compose ps postgres`, `docker compose logs --tail 100 postgres`; если остановлена —
   `docker compose up -d postgres`.
2. Если ошибки начались после выкладки — откатить на предыдущий образ API.
3. Проверить место: `docker system df`.
4. После исправления burn rate за 5 мин падает сразу, алерт гаснет за несколько минут.

## Когда эскалировать
Причина не найдена за 30 минут или нужна правка кода — разработчику API.
