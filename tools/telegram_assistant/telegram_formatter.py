"""
Telegram message formatting and chunking utilities.
Converts markdown from LLMs into safe, valid Telegram HTML tags,
properly escaping C# generics (e.g. List<T>) and HTML/Razor markup.
"""

from __future__ import annotations

import html
import re
from typing import List


def format_markdown_to_telegram_html(text: str) -> str:
    """
    Transforms markdown into Telegram-safe HTML.
    Safely escapes characters inside code blocks (e.g. <div class="..."> or List<T>).
    """
    if not text:
        return ""

    # 1. Extract and preserve code blocks (fenced with ```)
    code_blocks: List[str] = []

    def replace_code_block(match: re.Match) -> str:
        lang = match.group(1) or ""
        code_content = match.group(2)
        escaped_code = html.escape(code_content, quote=False)
        if lang:
            tag = f'<pre><code class="language-{html.escape(lang)}">{escaped_code}</code></pre>'
        else:
            tag = f"<pre><code>{escaped_code}</code></pre>"
        idx = len(code_blocks)
        code_blocks.append(tag)
        return f"%%TOKEN_CODE_BLOCK_{idx}%%"

    # Match fenced code blocks
    pattern_code_block = re.compile(r"```([a-zA-Z0-9_-]*)\n?(.*?)```", re.DOTALL)
    text = pattern_code_block.sub(replace_code_block, text)

    # 2. Extract and preserve inline code (fenced with `)
    inline_codes: List[str] = []

    def replace_inline_code(match: re.Match) -> str:
        code_text = match.group(1)
        escaped_code = html.escape(code_text, quote=False)
        tag = f"<code>{escaped_code}</code>"
        idx = len(inline_codes)
        inline_codes.append(tag)
        return f"%%TOKEN_INLINE_CODE_{idx}%%"

    pattern_inline = re.compile(r"`([^`\n]+?)`")
    text = pattern_inline.sub(replace_inline_code, text)

    # 3. Escape all remaining raw HTML tags/chars in the regular text
    text = html.escape(text, quote=False)

    # 4. Handle headers: # Header -> <b>Header</b>
    lines = []
    for line in text.splitlines():
        hdr_match = re.match(r"^\s*#{1,6}\s+(.*)$", line)
        if hdr_match:
            lines.append(f"<b>{hdr_match.group(1).strip()}</b>")
            continue
        # Bullet list item: * or -
        bullet_match = re.match(r"^\s*[\*\-]\s+(.*)$", line)
        if bullet_match:
            lines.append(f"• {bullet_match.group(1).strip()}")
            continue
        lines.append(line)
    text = "\n".join(lines)

    # 5. Handle bold: **text** or __text__
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text, flags=re.DOTALL)
    text = re.sub(r"__(.+?)__", r"<b>\1</b>", text, flags=re.DOTALL)

    # 6. Handle italic: *text* or _text_ (excluding double underscores/asterisks)
    text = re.sub(r"(?<!\w)\*([^\*\n]+?)\*(?!\w)", r"<i>\1</i>", text)
    text = re.sub(r"(?<!\w)_([^_\n]+?)_(?!\w)", r"<i>\1</i>", text)

    # 7. Restore inline code
    for idx, tag in enumerate(inline_codes):
        text = text.replace(f"%%TOKEN_INLINE_CODE_{idx}%%", tag)

    # 8. Restore code blocks
    for idx, tag in enumerate(code_blocks):
        text = text.replace(f"%%TOKEN_CODE_BLOCK_{idx}%%", tag)

    return text


def split_message_for_telegram(text: str, max_chunk_size: int = 4000) -> List[str]:
    """
    Splits long messages into Telegram-compliant chunks (< 4096 chars).
    Splits at line breaks or paragraph breaks whenever possible.
    """
    if len(text) <= max_chunk_size:
        return [text]

    chunks: List[str] = []
    remaining = text

    while len(remaining) > max_chunk_size:
        # Search for a clean split point within max_chunk_size
        candidate_chunk = remaining[:max_chunk_size]
        split_idx = candidate_chunk.rfind("\n\n")
        if split_idx == -1 or split_idx < max_chunk_size // 3:
            split_idx = candidate_chunk.rfind("\n")
        if split_idx == -1 or split_idx < max_chunk_size // 3:
            split_idx = candidate_chunk.rfind(" ")
        if split_idx == -1:
            split_idx = max_chunk_size

        chunk = remaining[:split_idx].strip()
        remaining = remaining[split_idx:].strip()

        # Check and close unclosed tags in this chunk, reopen in the next
        open_pre = chunk.count("<pre>") - chunk.count("</pre>")
        if open_pre > 0:
            chunk += "</code></pre>"
            remaining = "<pre><code>" + remaining

        open_b = chunk.count("<b>") - chunk.count("</b>")
        if open_b > 0:
            chunk += "</b>"
            remaining = "<b>" + remaining

        open_i = chunk.count("<i>") - chunk.count("</i>")
        if open_i > 0:
            chunk += "</i>"
            remaining = "<i>" + remaining

        chunks.append(chunk)

    if remaining:
        chunks.append(remaining)

    return chunks
