"""
Google Gemini API Client for LandAgencyMvc Assistant.
Communicates directly with Google Gemini REST API using httpx.
"""

from __future__ import annotations

import logging
from typing import Any, Dict, List, Optional
import httpx

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """Ти — персональний Senior C# / ASP.NET Core MVC асистент для проєкту LandAgencyMvc.
Проєкт розташований у папці: /Users/mac/Desktop/projects/LandAgencyMvc.

Твоє завдання — допомагати розробнику з мобільного телефону швидко та без зайвої води реалізовувати типові веб-завдання: додавання кнопок, нових сторінок, екшенів у контролерах, фільтрів, форм та дій.

ГОЛОВНІ ПРИНЦИПИ ВІДПОВІДЕЙ (МІНІМУМ КОДУ, МАКСИМУМ ПЕРЕВИКОРИСТАННЯ):
1. МАКСИМАЛЬНЕ ПЕРЕВИКОРИСТАННЯ (DRY):
   - Завжди перевикористовуй готові компоненти проєкту:
     * Для виводу земельних ділянок обов'язково використовуй існуючий компонент: <partial name="_PlotCard" model="plot" />.
     * Для оформлення кнопок використовуй існуючі класи site.css: .la-btn, .la-btn-green, .la-btn-dark, .la-btn-gray, .la-btn-sm тощо.
     * Для сіток використовуй класи: .la-grid-2, .la-catalog-grid, .la-row-between тощо.
   - Ніколи не пиши новий HTML для картки ділянки, якщо вона вже є в _PlotCard.cshtml!
   - Якщо дія зводиться до скидання фільтрів або перегляду всього каталогу — перевикористовуй існуючий метод Catalog() у ClientController без створення дублюючих методів!

2. МІНІМУМ КОДУ ТА ЗАБОРОНА ОВЕРІНЖИНІРИНГУ (NO USELESS IF/ELSE):
   - Пиши тільки найпростіший, сухий, необхідний код без зайвих перевірок і штучних умов.
   - КАТЕГОРИЧНО ЗАБОРОНЕНО городити штучні розгалуження @if ... else ..., де обидві гілки роблять те саме (наприклад: @if (Model.HasActiveFilters) Скинути else Оновити все). Це оверінжиніринг і шум у UI!
   - Правило для фільтрів: якщо фільтрів немає — кнопка «Скинути» просто приховується (@if (Model.HasActiveFilters) { <a ...>Скинути</a> }). Жодної непотрібної кнопки «Оновити все» поруч вигадувати не можна!
   - Правило для окремих кнопок: якщо користувач просить кнопку «Показати всі ділянки» під формою — давай пряме безумовне посилання без жодних if:
     <a asp-controller="Client" asp-action="Catalog" class="la-btn la-btn-green">Показати всі ділянки</a>.
   - Якщо користувач не просив JavaScript чи модальних вікон — роби ЗВИЧАЙНИЙ стандартний перехід через <a> або форму.
   - Якщо користувач каже «відкрити», «показати» — за замовчуванням роби перехід у цьому самому вікні (без target="_blank", без window.open).

3. ЛОГІКА РОЗМІЩЕННЯ ЕЛЕМЕНТІВ (УНИКАЙ ДУБЛЮВАННЯ):
   - УВАГА: ніколи не став кнопки глобальної навігації або перегляду всього каталогу (наприклад, «Показати всі ділянки») всередину компонента окремої картки _PlotCard.cshtml!
   - Кнопки загальних дій над каталогом завжди розміщуй на самій сторінці (Views/Client/Catalog.cshtml) поруч із фільтрами або формами підбору.

4. АРХІТЕКТУРА ТА СТЕК ПРОЄКТУ:
   - Стек: .NET 10.0, ASP.NET Core MVC, Entity Framework Core, SQLite (landagency.db).
   - Сувора типізація (ViewModels або моделі предметної області LandPlot), жодних ViewBag.
   - 0 інлайн-стилів style="..." (тільки класи site.css).
   - Жодних декоративних розділювачів/банерів у коментарях до коду (// ===, /* === */, # ===).

5. ФОРМАТ ВІДПОВІДІ ДЛЯ МОБІЛЬНОГО ТЕЛЕФОНУ:
   - Відповідай коротко, чітко, українською мовою.
   - Розбивай задачу на чіткі кроки:
     1. Кнопка / Посилання (вказуй точний файл і місце).
     2. Контролер (готовий лаконічний метод action або вказівка перевикористати існуючий).
     3. Представлення (нова або змінена View).
   - Давай тільки чистий робочий код без теоретичних лекцій та без надлишкових блоків.
"""


class GeminiClient:
    def __init__(self, api_key: str, model: str = "gemini-3-flash-preview"):
        self.api_key = api_key
        self.model = model
        self.endpoint = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"

    async def generate_response(
        self,
        user_message: str,
        codebase_context: str,
        conversation_history: Optional[List[Dict[str, Any]]] = None,
        image_bytes: Optional[bytes] = None,
        image_mime_type: str = "image/jpeg",
    ) -> str:
        """Sends request to Gemini API and returns the generated answer."""
        if not self.api_key or self.api_key == "AIzaSyYourGeminiApiKeyHere":
            return (
                "⚠️ <b>Ключ Gemini API не налаштовано!</b>\n\n"
                "Будь ласка, відкрийте файл <code>tools/telegram_assistant/.env</code> "
                "і вкажіть ваш <code>GEMINI_API_KEY</code>.\n"
                "Отримати безкоштовний ключ можна за посиланням:\n"
                "https://aistudio.google.com/app/apikey"
            )

        contents: List[Dict[str, Any]] = []

        if conversation_history:
            contents.extend(conversation_history)

        prompt_text = (
            f"КОНТЕКСТ КОДОВОЇ БАЗИ:\n{codebase_context}\n\n"
            f"ЗАПИТ РОЗРОБНИКА:\n{user_message}"
        )

        user_parts: List[Dict[str, Any]] = []
        if image_bytes:
            import base64
            encoded_image = base64.b64encode(image_bytes).decode("utf-8")
            user_parts.append({
                "inline_data": {
                    "mime_type": image_mime_type,
                    "data": encoded_image,
                }
            })

        user_parts.append({"text": prompt_text})

        contents.append({
            "role": "user",
            "parts": user_parts,
        })

        payload = {
            "system_instruction": {
                "parts": [{"text": SYSTEM_PROMPT}]
            },
            "contents": contents,
            "generationConfig": {
                "temperature": 0.2,
                "maxOutputTokens": 4096,
            },
        }

        url = f"{self.endpoint}?key={self.api_key}"

        try:
            async with httpx.AsyncClient(timeout=60.0) as client:
                response = await client.post(url, json=payload)

            if response.status_code == 200:
                data = response.json()
                try:
                    text = data["candidates"][0]["content"]["parts"][0]["text"]
                    return text.strip()
                except (KeyError, IndexError):
                    return "⚠️ Отримано порожню відповідь від моделі Gemini."

            if response.status_code == 429:
                return "⏳ <b>Перевищено ліміт запитів до Gemini API (Rate limit)</b>. Зачекайте кілька секунд і спробуйте знову."

            if response.status_code == 400:
                err_body = response.text
                logger.error("Gemini 400 error: %s", err_body)
                return f"⚠️ <b>Помилка конфігурації Gemini API (HTTP 400)</b>:\n<code>{err_body[:300]}</code>"

            return f"⚠️ <b>Помилка сервера Gemini API (HTTP {response.status_code})</b>."

        except httpx.TimeoutException:
            return "⏱️ <b>Таймаут запиту до Gemini API</b>. Мережа занадто повільна або модель обробляє великий обсяг коду. Спробуйте ще раз."
        except Exception as e:
            logger.exception("Unexpected error in Gemini client: %s", e)
            return f"⚠️ <b>Виникла непередбачена помилка</b>:\n<code>{html_escape(str(e))}</code>"


def html_escape(text: str) -> str:
    return (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )
