"""
LandAgencyMvc Telegram Code Assistant Bot.
Allows querying the LandAgencyMvc codebase from a mobile Telegram client
powered by Google Gemini 2.0 Flash.
"""

from __future__ import annotations

import html
import logging
import subprocess
import sys
import time
from typing import Any, Dict, List
from telegram import Update
from telegram.constants import ChatAction, ParseMode
from telegram.request import HTTPXRequest
from telegram.ext import (
    Application,
    ApplicationBuilder,
    CommandHandler,
    ContextTypes,
    MessageHandler,
    filters,
)

from config import load_settings
from codebase_indexer import CodebaseIndexer
from gemini_client import GeminiClient
from telegram_formatter import format_markdown_to_telegram_html, split_message_for_telegram

logging.basicConfig(
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    level=logging.INFO,
    handlers=[
        logging.StreamHandler(sys.stdout),
    ],
)
logger = logging.getLogger("LandAgencyBot")

settings = load_settings()
indexer = CodebaseIndexer(settings.codebase_path)
gemini_client = GeminiClient(api_key=settings.gemini_api_key, model=settings.gemini_model)

START_TIME = time.time()
user_conversations: Dict[int, List[Dict[str, Any]]] = {}


def is_user_authorized(user_id: int) -> bool:
    """Checks whether user is authorized if whitelist is enabled."""
    if not settings.is_whitelist_enabled:
        return True
    return user_id in settings.allowed_user_ids


async def reject_unauthorized(update: Update) -> None:
    """Informs unauthorized user of their user ID and restriction."""
    if not update.effective_user or not update.effective_message:
        return
    user_id = update.effective_user.id
    logger.warning("Unauthorized access attempt from user_id=%s (@%s)", user_id, update.effective_user.username)
    msg = (
        "⛔ <b>Доступ обмежено.</b>\n\n"
        f"Ваш Telegram User ID: <code>{user_id}</code>.\n\n"
        "Цей бот призначений для локальної розробки LandAgencyMvc.\n"
        "Щоб надати доступ своєму акаунту, додайте цей ID у файл:\n"
        "<code>tools/telegram_assistant/.env</code>\n"
        "у параметр <code>ALLOWED_TELEGRAM_USER_IDS</code> та перезапустіть бота."
    )
    await update.effective_message.reply_text(msg, parse_mode=ParseMode.HTML)


async def start_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /start command."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    indexer.refresh()
    file_count = indexer.get_file_count()
    user_id = update.effective_user.id

    msg = (
        "🚀 <b>LandAgencyMvc — Персональний Telegram Code Assistant</b>\n\n"
        "Вітаю! Я підключений до вашої кодової бази на Mac і готовий відповідати на будь-які запитання про проєкт прямо з телефону.\n\n"
        f"📂 <b>Проєкт:</b> <code>{settings.codebase_path.name}</code>\n"
        f"📄 <b>Проіндексовано файлів:</b> {file_count}\n"
        f"🤖 <b>AI Модель:</b> <code>{settings.gemini_model}</code>\n"
        f"🆔 <b>Ваш User ID:</b> <code>{user_id}</code>\n\n"
        "<b>Що можна робити:</b>\n"
        "• Простий текст: пишіть будь-які запитання (наприклад: <i>«Як мені змінити кнопку в Catalog.cshtml?»</i> або <i>«Поясни логіку ClientController»</i>)\n"
        "• /files — дерево основних файлів та контролерів проєкту\n"
        "• /find &lt;текст&gt; — швидкий пошук по коду (grep)\n"
        "• /cat &lt;файл&gt; — переглянути вміст файлу\n"
        "• /status — поточний статус Git та репозиторію\n"
        "• /clear — очистити історію діалогу\n"
        "• /help — детальна довідка"
    )
    await update.effective_message.reply_text(msg, parse_mode=ParseMode.HTML)


async def help_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /help command."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    msg = (
        "📖 <b>Довідка користувача LandAgencyMvc Assistant</b>\n\n"
        "Ви можете ставити запитання щодо коду, архітектури чи рефакторингу в довільній формі:\n\n"
        "💡 <b>Приклади запитів:</b>\n"
        "1. <i>«Як мені змінити кнопку в Catalog.cshtml?»</i>\n"
        "2. <i>«Поясни, де в ClientController обробляються ділянки»</i>\n"
        "3. <i>«Як працює валідація КОАТУУ в CadastralValidationService?»</i>\n"
        "4. <i>«Напиши новий метод у RealtorController для вивантаження звіту»</i>\n"
        "5. <i>«Покажи структуру моделі LandPlot»</i>\n\n"
        "⚙️ <b>Корисні команди:</b>\n"
        "• <code>/files</code> — показати структуру та список компонентів\n"
        "• <code>/find &lt;слово&gt;</code> — знайти рядок або змінну у файлах (наприклад: <code>/find CanRequestPurchase</code>)\n"
        "• <code>/cat &lt;файл&gt;</code> — показати файл (наприклад: <code>/cat Catalog.cshtml</code> або <code>/cat LandPlot.cs</code>)\n"
        "• <code>/status</code> — стан гілки Git, останній коміт та аптайм бота\n"
        "• <code>/clear</code> — скинути контекст розмови для чистого старту"
    )
    await update.effective_message.reply_text(msg, parse_mode=ParseMode.HTML)


async def files_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /files command: outputs codebase tree."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    indexer.refresh()
    tree = indexer.get_project_tree()
    await update.effective_message.reply_text(tree, parse_mode=ParseMode.HTML)


async def cat_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /cat <filename> command: prints file contents."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    args = context.args
    if not args:
        await update.effective_message.reply_text(
            "ℹ️ Вкажіть назву або шлях файлу: <code>/cat Catalog.cshtml</code>",
            parse_mode=ParseMode.HTML,
        )
        return

    target = " ".join(args).strip()
    content = indexer.read_file(target, max_lines=250)
    if not content:
        await update.effective_message.reply_text(
            f"❌ Файл <code>{html.escape(target)}</code> не знайдено або не вдалося прочитати.",
            parse_mode=ParseMode.HTML,
        )
        return

    escaped = html.escape(content, quote=False)
    text = f"📄 <b>Файл:</b> <code>{html.escape(target)}</code>\n\n<pre><code>{escaped}</code></pre>"
    chunks = split_message_for_telegram(text, max_chunk_size=3900)
    for chunk in chunks:
        await update.effective_message.reply_text(chunk, parse_mode=ParseMode.HTML)


async def find_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /find <keyword> command."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    args = context.args
    if not args:
        await update.effective_message.reply_text(
            "ℹ️ Вкажіть ключове слово для пошуку: <code>/find VerifiedInCatalog</code>",
            parse_mode=ParseMode.HTML,
        )
        return

    keyword = " ".join(args).strip()
    matches = indexer.search_code(keyword, max_results=12)
    if not matches:
        await update.effective_message.reply_text(
            f"🔍 За запитом <code>{html.escape(keyword)}</code> нічого не знайдено.",
            parse_mode=ParseMode.HTML,
        )
        return

    lines = [f"🔍 <b>Результати пошуку для:</b> <code>{html.escape(keyword)}</code>\n"]
    for rel_path, line_no, content in matches:
        esc_line = html.escape(content[:90], quote=False)
        lines.append(f"• <code>{rel_path}:{line_no}</code>\n  <code>{esc_line}</code>")

    await update.effective_message.reply_text("\n".join(lines), parse_mode=ParseMode.HTML)


async def status_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /status command: returns Git info and uptime."""
    if not update.effective_user or not update.effective_message:
        return
    if not is_user_authorized(update.effective_user.id):
        await reject_unauthorized(update)
        return

    uptime_sec = int(time.time() - START_TIME)
    uptime_fmt = f"{uptime_sec // 3600}г {(uptime_sec % 3600) // 60}хв {uptime_sec % 60}с"

    git_branch = "невідомо"
    git_commit = "невідомо"
    git_diff_count = 0

    try:
        branch_res = subprocess.run(
            ["git", "branch", "--show-current"],
            cwd=settings.codebase_path,
            capture_output=True,
            text=True,
            timeout=5,
        )
        if branch_res.returncode == 0:
            git_branch = branch_res.stdout.strip()

        commit_res = subprocess.run(
            ["git", "log", "-1", "--pretty=format:%h (%s)"],
            cwd=settings.codebase_path,
            capture_output=True,
            text=True,
            timeout=5,
        )
        if commit_res.returncode == 0:
            git_commit = commit_res.stdout.strip()

        status_res = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=settings.codebase_path,
            capture_output=True,
            text=True,
            timeout=5,
        )
        if status_res.returncode == 0:
            git_diff_count = len([line for line in status_res.stdout.splitlines() if line.strip()])
    except Exception as e:
        logger.warning("Failed to collect git status: %s", e)

    msg = (
        "📊 <b>Статус системи LandAgencyMvc Assistant</b>\n\n"
        f"🌿 <b>Гілка Git:</b> <code>{html.escape(git_branch)}</code>\n"
        f"🔖 <b>Останній коміт:</b> <code>{html.escape(git_commit)}</code>\n"
        f"✏️ <b>Незбережених змін (diff):</b> {git_diff_count} файлів\n"
        f"📁 <b>Індекс файлів:</b> {indexer.get_file_count()} файлів\n"
        f"⏱️ <b>Аптайм бота:</b> {uptime_fmt}\n"
        f"🤖 <b>Модель:</b> <code>{settings.gemini_model}</code>"
    )
    await update.effective_message.reply_text(msg, parse_mode=ParseMode.HTML)


async def clear_command(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles /clear command: clears conversation history."""
    if not update.effective_user or not update.effective_message:
        return
    user_id = update.effective_user.id
    if user_id in user_conversations:
        user_conversations[user_id].clear()
    await update.effective_message.reply_text(
        "🧹 <b>Історію діалогу очищено!</b> Наступні запитання почнуться з чистого листа.",
        parse_mode=ParseMode.HTML,
    )


async def handle_message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """Handles natural language questions from the user."""
    if not update.effective_user or not update.effective_message:
        return
    user_id = update.effective_user.id
    if not is_user_authorized(user_id):
        await reject_unauthorized(update)
        return

    raw_text = update.effective_message.text or update.effective_message.caption or ""
    user_text = raw_text.strip()

    image_bytes = None
    if update.effective_message.photo:
        try:
            photo = update.effective_message.photo[-1]
            photo_file = await context.bot.get_file(photo.file_id)
            image_bytes = bytes(await photo_file.download_as_bytearray())
        except Exception as e:
            logger.warning("Could not download attached photo: %s", e)

    if not user_text and not image_bytes:
        return

    if not user_text and image_bytes:
        user_text = "Проаналізуй цей скріншот інтерфейсу або коду та надай рекомендації."

    chat_id = update.effective_chat.id
    await context.bot.send_chat_action(chat_id=chat_id, action=ChatAction.TYPING)

    # 1. Build codebase context
    codebase_context = indexer.build_gemini_context(user_text)

    # 2. Retrieve history (keep up to 6 turns)
    history = user_conversations.setdefault(user_id, [])
    if len(history) > 6:
        history = history[-6:]
        user_conversations[user_id] = history

    # 3. Request completion from Gemini
    response_markdown = await gemini_client.generate_response(
        user_message=user_text,
        codebase_context=codebase_context,
        conversation_history=history,
        image_bytes=image_bytes,
    )

    # 4. Save history turn
    history.append({"role": "user", "parts": [{"text": user_text}]})
    history.append({"role": "model", "parts": [{"text": response_markdown}]})

    # 5. Format to Telegram safe HTML and chunk if needed
    formatted_html = format_markdown_to_telegram_html(response_markdown)
    chunks = split_message_for_telegram(formatted_html, max_chunk_size=3900)

    for chunk in chunks:
        try:
            await update.effective_message.reply_text(chunk, parse_mode=ParseMode.HTML)
        except Exception as e:
            logger.warning("Failed to send chunk with HTML parse mode (%s), falling back to plain text", e)
            await update.effective_message.reply_text(response_markdown[:3900])


def main() -> None:
    """Starts the Telegram assistant bot."""
    if not settings.telegram_bot_token or settings.telegram_bot_token == "123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ_example":
        logger.error(
            "TELEGRAM_BOT_TOKEN не налаштовано! Будь ласка, відкрийте tools/telegram_assistant/.env "
            "і вкажіть дійсний токен від @BotFather."
        )
        sys.exit(1)

    logger.info("Initializing Telegram Assistant for %s...", settings.codebase_path)
    logger.info("Codebase indexed: %d files.", indexer.get_file_count())
    if settings.is_whitelist_enabled:
        logger.info("Security whitelist enabled for user IDs: %s", settings.allowed_user_ids)
    else:
        logger.info("Security whitelist disabled (public to anyone with link).")

    t_request = HTTPXRequest(
        connect_timeout=20.0,
        read_timeout=30.0,
        write_timeout=20.0,
        pool_timeout=20.0,
    )
    application = (
        ApplicationBuilder()
        .token(settings.telegram_bot_token)
        .request(t_request)
        .build()
    )

    application.add_handler(CommandHandler("start", start_command))
    application.add_handler(CommandHandler("help", help_command))
    application.add_handler(CommandHandler("files", files_command))
    application.add_handler(CommandHandler("cat", cat_command))
    application.add_handler(CommandHandler("find", find_command))
    application.add_handler(CommandHandler("status", status_command))
    application.add_handler(CommandHandler("clear", clear_command))
    message_filter = (filters.TEXT | filters.PHOTO | filters.CAPTION) & (~filters.COMMAND)
    application.add_handler(MessageHandler(message_filter, handle_message))

    logger.info("Bot is running! Press Ctrl+C to stop.")
    application.run_polling()


if __name__ == "__main__":
    main()
