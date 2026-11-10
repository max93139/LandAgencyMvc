"""
Codebase Indexer and Context Provider for LandAgencyMvc.
Scans and indexes C#, Razor, JSON, and CSS files to provide high-fidelity context to Gemini.
"""

from __future__ import annotations

import os
import re
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

IGNORED_DIRS = {
    ".git",
    "bin",
    "obj",
    ".idea",
    "tools",
    ".venv",
    "__pycache__",
    "uploads",
    "node_modules",
}

ALLOWED_EXTENSIONS = {
    ".cs",
    ".cshtml",
    ".json",
    ".css",
    ".csproj",
    ".sln",
    ".md",
}


class CodebaseIndexer:
    def __init__(self, root_dir: Path):
        self.root_dir = root_dir.resolve()
        self.files_by_rel_path: Dict[str, Path] = {}
        self.files_by_name: Dict[str, List[Path]] = {}
        self.refresh()

    def refresh(self) -> None:
        """Rescans the codebase and updates the index."""
        self.files_by_rel_path.clear()
        self.files_by_name.clear()

        if not self.root_dir.exists():
            return

        for current_root, dirs, files in os.walk(self.root_dir):
            dirs[:] = [d for d in dirs if d not in IGNORED_DIRS and not d.startswith(".")]

            for file_name in files:
                ext = os.path.splitext(file_name)[1].lower()
                if ext not in ALLOWED_EXTENSIONS:
                    continue

                full_path = Path(current_root) / file_name
                try:
                    rel_path = full_path.relative_to(self.root_dir).as_posix()
                except ValueError:
                    rel_path = full_path.as_posix()

                self.files_by_rel_path[rel_path] = full_path

                name_lower = file_name.lower()
                self.files_by_name.setdefault(name_lower, []).append(full_path)

                stem_lower = Path(file_name).stem.lower()
                if stem_lower != name_lower:
                    self.files_by_name.setdefault(stem_lower, []).append(full_path)

    def get_file_count(self) -> int:
        return len(self.files_by_rel_path)

    def get_project_tree(self) -> str:
        """Returns a high-level tree of LandAgencyMvc components."""
        categories: Dict[str, List[str]] = {
            "Controllers": [],
            "Models & Data": [],
            "Views": [],
            "Services": [],
            "Configuration & Core": [],
        }

        for rel in sorted(self.files_by_rel_path.keys()):
            if "bin/" in rel or "obj/" in rel:
                continue

            if "Controllers/" in rel:
                categories["Controllers"].append(rel)
            elif "Models/" in rel or "Data/" in rel:
                categories["Models & Data"].append(rel)
            elif "Views/" in rel:
                categories["Views"].append(rel)
            elif "Services/" in rel:
                categories["Services"].append(rel)
            elif rel.endswith((".json", ".csproj", ".sln", ".cs", ".md")):
                categories["Configuration & Core"].append(rel)

        output_lines = [f"Codebase Root: {self.root_dir.name} ({len(self.files_by_rel_path)} indexed files)\n"]
        for cat, items in categories.items():
            if items:
                output_lines.append(f"<b>{cat} ({len(items)}):</b>")
                for item in items[:12]:
                    output_lines.append(f"  • <code>{item}</code>")
                if len(items) > 12:
                    output_lines.append(f"  • ... та ще {len(items) - 12} файлів")
                output_lines.append("")

        return "\n".join(output_lines).strip()

    def find_matching_files(self, query: str) -> List[Path]:
        """Identifies files explicitly or implicitly referenced in query."""
        matched: Set[Path] = set()
        words = re.findall(r"[A-Za-z0-9_.-]+", query)

        for word in words:
            word_lower = word.lower()
            if word_lower in self.files_by_name:
                matched.update(self.files_by_name[word_lower])

            for rel_path, full_path in self.files_by_rel_path.items():
                if word_lower in rel_path.lower():
                    matched.add(full_path)

        return sorted(list(matched), key=lambda p: str(p))

    def search_code(self, keyword: str, max_results: int = 15) -> List[Tuple[str, int, str]]:
        """Searches for keyword across indexed files."""
        results: List[Tuple[str, int, str]] = []
        if not keyword or len(keyword) < 2:
            return results

        pattern = re.compile(re.escape(keyword), re.IGNORECASE)

        for rel_path, full_path in self.files_by_rel_path.items():
            if not full_path.is_file():
                continue
            try:
                content = full_path.read_text(encoding="utf-8", errors="ignore")
                for line_idx, line in enumerate(content.splitlines(), start=1):
                    if pattern.search(line):
                        results.append((rel_path, line_idx, line.strip()))
                        if len(results) >= max_results:
                            return results
            except Exception:
                continue

        return results

    def read_file(self, target: str, max_lines: int = 600) -> Optional[str]:
        """Reads content of a file by relative path or file name."""
        target_path: Optional[Path] = None
        target_clean = target.strip().lstrip("/")

        if target_clean in self.files_by_rel_path:
            target_path = self.files_by_rel_path[target_clean]
        else:
            name_lower = Path(target_clean).name.lower()
            candidates = self.files_by_name.get(name_lower, [])
            if candidates:
                target_path = candidates[0]
            else:
                for rel, p in self.files_by_rel_path.items():
                    if target_clean.lower() in rel.lower():
                        target_path = p
                        break

        if not target_path or not target_path.is_file():
            return None

        try:
            lines = target_path.read_text(encoding="utf-8", errors="ignore").splitlines()
            if len(lines) > max_lines:
                truncated = lines[:max_lines]
                truncated.append(f"\n... [Урізано, всього {len(lines)} рядків] ...")
                return "\n".join(truncated)
            return "\n".join(lines)
        except Exception:
            return None

    def build_gemini_context(self, user_query: str) -> str:
        """
        Assembles codebase context to accompany the user's prompt.
        Includes matched files and a summary of the project architecture.
        """
        matched_files = self.find_matching_files(user_query)
        context_parts: List[str] = []

        context_parts.append(
            "PROJECT CONTEXT:\n"
            "- Project: LandAgencyMvc (Land Plots Real Estate Agency Automation)\n"
            "- Architecture: ASP.NET Core MVC (.NET 10.0), Entity Framework Core SQLite, Clean Architecture\n"
            "- Auth: Cookie Authentication (LandAgency.Auth) with Roles: Client, Realtor, Lawyer\n"
            "- Features: Strict MVC ViewModels, Cadastral validation (KOATUU), DzkRegistrySimulator, AutoMatchingService, 0 inline styles\n"
        )

        if matched_files:
            context_parts.append(f"RELEVANT FILES FOR QUERY ({len(matched_files)} files detected):")
            for f in matched_files[:5]:
                try:
                    rel = f.relative_to(self.root_dir).as_posix()
                except ValueError:
                    rel = f.name
                content = self.read_file(rel, max_lines=450)
                if content:
                    context_parts.append(f"\n--- FILE: {rel} ---\n{content}\n--- END FILE: {rel} ---")
        else:
            core_files = [
                "LandAgencyMvc/Controllers/ClientController.cs",
                "LandAgencyMvc/Views/Client/Catalog.cshtml",
                "LandAgencyMvc/Models/LandPlot.cs",
                "LandAgencyMvc/Program.cs",
            ]
            context_parts.append("SAMPLE ARCHITECTURE FILES:")
            for cf in core_files:
                if cf in self.files_by_rel_path:
                    excerpt = self.read_file(cf, max_lines=120)
                    if excerpt:
                        context_parts.append(f"\n--- FILE EXCERPT: {cf} ---\n{excerpt}\n--- END EXCERPT ---")

        return "\n\n".join(context_parts)
