"""
Configuration loader for LandAgencyMvc Telegram Assistant.
Loads settings from environment variables and .env file.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Set
from dotenv import load_dotenv

BASE_DIR = Path(__file__).resolve().parent
ENV_PATH = BASE_DIR / ".env"
load_dotenv(dotenv_path=ENV_PATH)


@dataclass(frozen=True)
class Settings:
    telegram_bot_token: str
    gemini_api_key: str
    gemini_model: str
    codebase_path: Path
    allowed_user_ids: Set[int]

    @property
    def is_whitelist_enabled(self) -> bool:
        return len(self.allowed_user_ids) > 0


def load_settings() -> Settings:
    token = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
    gemini_key = os.getenv("GEMINI_API_KEY", "").strip()
    model = os.getenv("GEMINI_MODEL", "gemini-2.0-flash").strip()

    raw_codebase_path = os.getenv("CODEBASE_PATH", "../../").strip()
    codebase_path = (BASE_DIR / raw_codebase_path).resolve()

    raw_allowed_ids = os.getenv("ALLOWED_TELEGRAM_USER_IDS", "").strip()
    allowed_user_ids: Set[int] = set()
    if raw_allowed_ids:
        for part in raw_allowed_ids.split(","):
            part = part.strip()
            if part.isdigit():
                allowed_user_ids.add(int(part))

    return Settings(
        telegram_bot_token=token,
        gemini_api_key=gemini_key,
        gemini_model=model,
        codebase_path=codebase_path,
        allowed_user_ids=allowed_user_ids,
    )
