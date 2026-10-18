#!/usr/bin/env bash
# Снимок рабочего дерева для checker.
#   save — запомнить хэши всех неигнорируемых файлов (снимок лежит в .git/, рабочее дерево не меняется);
#   diff — вывести файлы, которые изменились, появились или удалены после снимка.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"
snapshot="$(git rev-parse --git-dir)/pipeline-snapshot"

# Хэши текущих файлов, отсортированные по пути.
current() {
    # Удалённые, но ещё отслеживаемые файлы git перечисляет, а shasum на них падает — пропускаем их.
    git ls-files -co --exclude-standard -z | { xargs -0 shasum -a 256 2>/dev/null || true; } | sort -k2
}

case "${1:-}" in
    save)
        current > "$snapshot"
        echo "Snapshot saved: $(wc -l < "$snapshot" | tr -d ' ') files"
        ;;
    diff)
        [[ -f "$snapshot" ]] || { echo "No snapshot. Run: $0 save" >&2; exit 1; }
        diff "$snapshot" <(current) | awk '/^[<>] /{ sub(/^[<>] [0-9a-f]+  /, ""); print }' | sort -u || true
        ;;
    *)
        echo "Usage: $0 save|diff" >&2
        exit 2
        ;;
esac
