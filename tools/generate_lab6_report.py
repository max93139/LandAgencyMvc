#!/usr/bin/env python3
"""Збирає звіт ЛР № 6 у docx із файлу Markdown тим самим оформленням, що й звіт ЛР № 5.

Перед збиранням перевіряється, що кожен рядок лістингу справді є у вихідному коді проєкту.

Запуск із кореня репозиторію (потрібен python-docx):
    .venv/bin/python tools/generate_lab6_report.py
"""
import importlib.util
import inspect
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

spec = importlib.util.spec_from_file_location('lab5_report', ROOT / 'tools' / 'generate_lab5_report.py')
lab5 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lab5)

lab5.SOURCE = ROOT / 'Звіт_ЛР6_Паралелізм_Агентство_земельних_ділянок_Сивопляс_ІПЗ-23.md'
lab5.OUTPUT = lab5.SOURCE.with_suffix('.docx')

title_code = inspect.getsource(lab5.add_title_page)
title_code = (title_code
              .replace('з лабораторної роботи № 5', 'з лабораторної роботи № 6')
              .replace('CRUD, ACID, повнота функціоналу', 'Паралельна обробка даних'))
namespace = dict(vars(lab5))
exec(title_code, namespace)
lab5.add_title_page = namespace['add_title_page']

if __name__ == '__main__':
    lab5.build()
