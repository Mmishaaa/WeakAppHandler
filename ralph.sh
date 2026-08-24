#!/bin/bash
set -e

TASKS_FILE="tasks.json"

# Hard-scope this script to the intended repository. It pushes branches and
# merges PRs — running it from the wrong working directory (a different repo
# that happens to also have an 'origin' and a 'main' branch) must fail loudly
# instead of silently operating on someone else's repo.
EXPECTED_REMOTE="Mmishaaa/WeakAppHandler"

guard_repository() {
    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "ralph.sh must be run from inside a git working tree." >&2
        exit 1
    fi
    local remote_url
    remote_url="$(git remote get-url origin 2>/dev/null || true)"
    if [[ "$remote_url" != *"$EXPECTED_REMOTE"* ]]; then
        echo "ralph.sh is scoped to '$EXPECTED_REMOTE' but origin is '$remote_url'." >&2
        echo "Refusing to run — this script pushes branches and merges PRs." >&2
        echo "If this is intentional (fork/rename), update EXPECTED_REMOTE at the top of ralph.sh." >&2
        exit 1
    fi
}

guard_repository

# Make sure gh (GitHub CLI) is on PATH for this script AND any subprocess it
# spawns (claude -p, and whatever shell commands that nested agent runs) —
# a fresh winget install doesn't always propagate to already-open shells.
if ! command -v gh >/dev/null 2>&1 && [ -x "/c/Program Files/GitHub CLI/gh.exe" ]; then
    export PATH="$PATH:/c/Program Files/GitHub CLI"
fi

# Agent selection:
# - Set RALPH_AGENT=claude or RALPH_AGENT=codex to force.
# - Otherwise auto-detect (prefers Claude if available).
resolve_agent() {
    if [[ -n "${RALPH_AGENT:-}" ]]; then
        echo "$RALPH_AGENT"
        return 0
    fi
    if command -v claude >/dev/null 2>&1; then
        echo "claude"
        return 0
    fi
    if command -v codex >/dev/null 2>&1; then
        echo "codex"
        return 0
    fi
    return 1
}

notify() {
    local msg="$1"
    echo "$msg"
    if command -v powershell.exe >/dev/null 2>&1; then
        local escaped="${msg//\'/\'\'}"
        powershell.exe -NoProfile -Command \
            "Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('${escaped}')" \
            >/dev/null 2>&1 || true
    fi
}

run_agent() {
    local agent="$1"
    local prompt="$2"

    case "$agent" in
        claude)
            claude --permission-mode acceptEdits -p "$prompt"
            ;;
        codex)
            local output_file
            output_file="$(mktemp -t ralph_codex.XXXXXX)"
            # Use non-interactive Codex exec and capture only the last message.
            codex exec --full-auto --color never -C "$PWD" --output-last-message "$output_file" "$prompt" >/dev/null
            cat "$output_file"
            rm -f "$output_file"
            ;;
        *)
            echo "Unsupported agent: $agent" >&2
            return 1
            ;;
    esac
}

# Функция проверки наличия pending задач
has_pending_tasks() {
    pending_count=$(grep -c '"status": "pending"' "$TASKS_FILE" 2>/dev/null || echo "0")
    [ "$pending_count" -gt 0 ]
}

iteration=1
max_iterations="${RALPH_MAX_ITERATIONS:-0}"  # 0 = unlimited

within_iteration_limit() {
    [ "$max_iterations" -eq 0 ] || [ "$iteration" -le "$max_iterations" ]
}

while has_pending_tasks && within_iteration_limit; do
    echo "Итерация $iteration"
    echo "-----------------------------------"

    # Показываем текущий статус задач
    pending=$(grep -c '"status": "pending"' "$TASKS_FILE" 2>/dev/null || echo "0")
    done_count=$(grep -c '"status": "done"' "$TASKS_FILE" 2>/dev/null || echo "0")
    echo "Задач pending: $pending, done: $done_count"
    echo "-----------------------------------"

    agent=$(resolve_agent) || {
        echo "Не найден поддерживаемый агент. Установите 'claude' или 'codex', либо задайте RALPH_AGENT." >&2
        exit 1
    }

    # Deterministic safety net: always start the iteration from a clean, up-to-date
    # main, regardless of whether the previous iteration's agent cleaned up after
    # itself. If this fails (diverged history, network down), stop the loop rather
    # than let an agent work from a stale or wrong base.
    git checkout main
    git pull origin main --ff-only

    prompt=$(cat <<'EOF'
@tasks.json @progress.txt
1. Найди фичу с наивысшим приоритетом и работай ТОЛЬКО над ней.
Это должна быть фича, которую ТЫ считаешь наиболее приоритетной — не обязательно первая в списке.
2. Создай новую ветку от актуального main с именем вида task/TASK-XXX-краткий-слаг
   (например task/task-002-solution-skeleton) и работай только в ней. НЕ коммить в main напрямую.
3. Проверь качество кода перед завершением:
   - Backend (.NET): 'dotnet build' проходит без предупреждений (TreatWarningsAsErrors)
     и 'dotnet test' проходит на затронутых проектах (используй соответствующий .slnf,
     например 'dotnet test processor.slnf', если он уже существует).
   - Frontend (React/TS): 'npm run lint' и 'npm run build' проходят; 'npm test', если тесты есть.
   Если задача не затрагивает соответствующий стек (например, ещё нет .sln/.slnf или package.json),
   пропусти неприменимую проверку.
4. Обнови TASK с информацией о выполненной работе.
5. Добавь свой прогресс в файл progress.txt.
Используй это, чтобы оставить заметку для следующей итерации работы над кодом.
6. Закоммить изменения в свою ветку.
7. ТОЛЬКО если задача полностью выполнена и все проверки прошли (status -> "done"):
   a. Запушь ветку: git push -u origin <ветка>
   b. Открой pull request через 'gh pr create --base main --head <ветка> --title ... --body ...'
   c. Смерджи его: 'gh pr merge --squash --delete-branch'
   d. Вернись на main и подтяни изменения: 'git checkout main && git pull origin main'
      (это обязательно — иначе следующая итерация увидит устаревший tasks.json)
   Если задача НЕ доведена до конца в этой сессии — оставь коммиты в своей ветке
   НЕ смердженными, НЕ открывай PR, оставь status "pending" и опиши блокер в progress.txt.
РАБОТАЙ ТОЛЬКО НАД ОДНОЙ ФИЧЕЙ.
Если при реализации фичи ты заметишь, что TASK полностью выполнен, выведи <promise>COMPLETE</promise>.
EOF
)

    result=$(run_agent "$agent" "$prompt")

    echo "$result"

    if [[ "$result" == *"<promise>COMPLETE</promise>"* ]]; then
        echo "✓ TASK выполнен!"
        # Проверяем, остались ли ещё pending задачи
        remaining=$(grep -c '"status": "pending"' "$TASKS_FILE" 2>/dev/null || echo "0")
        if [ "$remaining" -eq 0 ]; then
            echo "🎉 Все задачи выполнены!"
            notify "Хозяин, я всё сделал!"
            exit 0
        fi
        echo "Осталось задач: $remaining. Продолжаю..."
        notify "Задача готова. Продолжаю работу."
    fi

    ((iteration++))
done

echo "Все задачи выполнены! Итераций: $((iteration-1))"
notify "Хозяин, я сделал!"
