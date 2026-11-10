#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

PID_FILE="$SCRIPT_DIR/bot.pid"
LOG_FILE="$SCRIPT_DIR/bot.log"
VENV_DIR="$SCRIPT_DIR/.venv"
ENV_FILE="$SCRIPT_DIR/.env"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"

# 1. Ensure .env exists
if [ ! -f "$ENV_FILE" ]; then
    echo "⚠️  Файл .env не знайдено. Створюю його з шаблону .env.example..."
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    echo "📝 Будь ласка, відкрийте $ENV_FILE та вкажіть ваш TELEGRAM_BOT_TOKEN та GEMINI_API_KEY!"
fi

# 2. Ensure virtualenv exists
if [ ! -d "$VENV_DIR" ]; then
    echo "📦 Створення віртуального оточення Python (.venv)..."
    python3 -m venv "$VENV_DIR"
fi

PYTHON_BIN="$VENV_DIR/bin/python"
PIP_BIN="$VENV_DIR/bin/pip"

# 3. Check dependencies
if [ ! -f "$VENV_DIR/.installed" ]; then
    echo "📥 Встановлення залежностей з requirements.txt..."
    "$PIP_BIN" install -q -r "$SCRIPT_DIR/requirements.txt"
    touch "$VENV_DIR/.installed"
fi

COMMAND="${1:-foreground}"

case "$COMMAND" in
    start|background)
        if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
            echo "✅ Бот вже працює у фоні! (PID: $(cat "$PID_FILE"))"
            exit 0
        fi

        echo "🚀 Запуск Telegram-бота у фоновому режимі..."
        nohup "$PYTHON_BIN" "$SCRIPT_DIR/bot.py" > "$LOG_FILE" 2>&1 &
        echo $! > "$PID_FILE"
        sleep 1

        if kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
            echo "🎉 Бот успішно запущений у фоні! (PID: $(cat "$PID_FILE"))"
            echo "📄 Логи записуються у: $LOG_FILE"
            echo "💡 Щоб переглянути логи: ./run.sh logs"
            echo "🛑 Щоб зупинити бота:  ./run.sh stop"
        else
            echo "❌ Помилка старту бота. Перевірте логи:"
            cat "$LOG_FILE"
            exit 1
        fi
        ;;

    stop)
        if [ -f "$PID_FILE" ]; then
            PID="$(cat "$PID_FILE")"
            if kill -0 "$PID" 2>/dev/null; then
                echo "🛑 Зупинка бота (PID: $PID)..."
                kill "$PID"
                sleep 1
                rm -f "$PID_FILE"
                echo "✅ Бот успішно зупинений."
            else
                echo "⚠️ Процес з PID $PID не знайдено. Видаляю старий pid-файл."
                rm -f "$PID_FILE"
            fi
        else
            echo "ℹ️ Бот не запущений (pid-файл відсутній)."
        fi
        ;;

    status)
        if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
            echo "🟢 Бот активний і працює у фоні. (PID: $(cat "$PID_FILE"))"
        else
            echo "⚪ Бот зараз не запущений."
        fi
        ;;

    logs)
        if [ -f "$LOG_FILE" ]; then
            echo "📄 Останні логи бота (натисніть Ctrl+C для виходу):"
            tail -f -n 40 "$LOG_FILE"
        else
            echo "ℹ️ Файл логів ще не створено."
        fi
        ;;

    foreground|run)
        echo "🚀 Запуск бота в інтерактивному режимі (натисніть Ctrl+C для зупинки)..."
        exec "$PYTHON_BIN" "$SCRIPT_DIR/bot.py"
        ;;

    *)
        echo "Використання: ./run.sh [start|stop|status|logs|foreground]"
        echo "  start      - запуск у фоновому режимі (nohup)"
        echo "  stop       - зупинка фонового процесу"
        echo "  status     - перевірка статусу бота"
        echo "  logs       - перегляд живих логів (tail -f)"
        echo "  foreground - запуск у поточному вікні термінала"
        ;;
esac
