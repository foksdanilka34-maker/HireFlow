#!/usr/bin/env bash
# Учения «пауза PostgreSQL под нагрузкой» (docs/drills/postgres-pause.md).
# k6 нагружает стенд, база замораживается на OUTAGE и размораживается; пауза снимается при любом выходе, в том числе по Ctrl+C.
# Переменные: RATE (итераций/с, 10), BASELINE (3m), OUTAGE (6m), RECOVERY (6m); длительность — 90s или 3m.
set -euo pipefail
cd "$(dirname "$0")/.."

RATE=${RATE:-10}
BASELINE=${BASELINE:-3m}
OUTAGE=${OUTAGE:-6m}
RECOVERY=${RECOVERY:-6m}

STAMP=$(date -u +%Y%m%dT%H%M%SZ)
LOG=load/results/drill-$STAMP.log
K6_LOG=load/results/drill-$STAMP-k6.log
K6_CONTAINER=hireflow-k6-drill
K6_PID=

# Переводит длительность вида 90s или 3m в секунды.
seconds() {
  case $1 in
    *m) echo $(( ${1%m} * 60 )) ;;
    *s) echo "${1%s}" ;;
    *) echo "$1" ;;
  esac
}

# Пишет шаг учений с меткой времени UTC в консоль и в лог.
log() {
  echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $*" | tee -a "$LOG"
}

# Снимает паузу с базы и останавливает k6 при выходе из скрипта: Ctrl+C, закрытие терминала, ошибка.
cleanup() {
  docker compose unpause postgres >/dev/null 2>&1 && log "postgres снят с паузы" || true
  if [ -n "$K6_PID" ] && kill -0 "$K6_PID" 2>/dev/null; then
    docker stop "$K6_CONTAINER" >/dev/null 2>&1 || true
    log "k6 остановлен"
  fi
}

mkdir -p load/results

health=$(docker compose --profile app ps --format '{{.Health}}' api 2>/dev/null || true)
if [ "$health" != "healthy" ]; then
  echo "api не healthy (сейчас: '${health:-не запущен}'): сначала make up и дождитесь healthy" >&2
  exit 1
fi
if [ "$(docker compose ps --format '{{.State}}' postgres)" != "running" ]; then
  echo "postgres не в состоянии running (возможно, уже на паузе: docker compose unpause postgres)" >&2
  exit 1
fi

total=$(( $(seconds "$BASELINE") + $(seconds "$OUTAGE") + $(seconds "$RECOVERY") ))
log "старт: RATE=$RATE BASELINE=$BASELINE OUTAGE=$OUTAGE RECOVERY=$RECOVERY, вывод k6 — $K6_LOG"

# Ловушки ставятся только после проверок: скрипт не снимает паузу, поставленную не им.
trap cleanup EXIT
trap 'exit 130' INT TERM
trap 'exit 129' HUP

# k6: минута разгона, затем удержание на всё время учений. Пороги при учениях ожидаемо проваливаются.
docker compose --profile load run --rm -T --name "$K6_CONTAINER" k6 \
  run -e RATE="$RATE" -e DURATION="${total}s" /scripts/hireflow.js >"$K6_LOG" 2>&1 &
K6_PID=$!
log "k6 запущен, baseline $BASELINE (включая минуту разгона)"
sleep "$(seconds "$BASELINE")"

kill -0 "$K6_PID" 2>/dev/null || { log "k6 завершился до паузы, см. $K6_LOG"; exit 1; }
log "ВНИМАНИЕ: postgres замораживается на $OUTAGE. Прервать — Ctrl+C (пауза снимется сама)"
docker compose pause postgres >/dev/null
log "postgres на паузе"
sleep "$(seconds "$OUTAGE")"

docker compose unpause postgres >/dev/null
log "postgres снят с паузы, recovery $RECOVERY"
sleep "$(seconds "$RECOVERY")"

log "ждём окончания k6"
if wait "$K6_PID"; then k6_code=0; else k6_code=$?; fi
K6_PID=
log "k6 завершился с кодом $k6_code (при учениях пороги SLO ожидаемо не выполняются)"
log "готово: таймлайн — $LOG, сводка k6 — $K6_LOG"
