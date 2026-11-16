#!/usr/bin/env python3
"""Збирає звіт ЛР № 5 у docx із файлу Markdown за оформленням ДСТУ 3008:2015.

Джерелом тексту є Звіт_ЛР5_*.md, а допоміжні функції оформлення беруться зі скрипта ЛР № 3.
Перед збиранням перевіряється, що кожен рядок лістингу справді є у вихідному коді проєкту.
Зміст є справжнім полем Word: номери сторінок у ньому орієнтовні й уточнюються, коли Word пропонує оновити поля.

Запуск із кореня репозиторію (потрібен python-docx):
    .venv/bin/python tools/generate_lab5_report.py
"""
import importlib.util
import math
import re
import struct
import sys
from pathlib import Path

import docx
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_TAB_ALIGNMENT, WD_TAB_LEADER
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn
from docx.shared import Inches, Pt, RGBColor

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'Звіт_ЛР5_CRUD_ACID_Агентство_земельних_ділянок_Сивопляс_ІПЗ-23.md'
OUTPUT = SOURCE.with_suffix('.docx')
CODE_ROOT = ROOT / 'LandAgencyMvc'
BLACK = RGBColor(0, 0, 0)
TEXT_WIDTH_IN = 6.69
FIRST_CONTENT_PAGE = 3

spec = importlib.util.spec_from_file_location('lab3_helpers', ROOT / 'generate_docx_report.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


def strip_inline(text):
    return re.sub(r'`([^`]+)`', r'\1', re.sub(r'\*\*(.+?)\*\*', r'\1', text))


def parse_blocks(markdown):
    blocks, lines, i = [], markdown.split('\n'), 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
        elif line.startswith('## '):
            blocks.append(('h1', line[3:].strip())); i += 1
        elif line.startswith('### '):
            blocks.append(('h2', line[4:].strip())); i += 1
        elif line.startswith('```'):
            code = []
            i += 1
            while not lines[i].startswith('```'):
                code.append(lines[i]); i += 1
            i += 1
            title = blocks.pop()[1] if blocks and blocks[-1][0] == 'p' and blocks[-1][1].startswith('Лістинг') else 'Лістинг'
            blocks.append(('code', title, code))
        elif line.startswith('!['):
            m = re.match(r'!\[(.*?)\]\((.*?)\)', line)
            blocks.append(('fig', m.group(1), m.group(2))); i += 1
        elif line.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].startswith('|'):
                rows.append([c.strip() for c in lines[i].strip().strip('|').split('|')]); i += 1
            caption = blocks.pop()[1] if blocks and blocks[-1][0] == 'p' and blocks[-1][1].startswith('**Таблиця') else None
            blocks.append(('table', caption, rows[0], rows[2:]))
        elif line.startswith('- '):
            blocks.append(('li', line[2:].strip())); i += 1
        elif re.match(r'\d+\. ', line):
            blocks.append(('li', line.strip())); i += 1
        else:
            blocks.append(('p', line.strip())); i += 1
    return blocks


def verify_listings(blocks):
    problems = []
    for block in blocks:
        if block[0] != 'code':
            continue
        title, code = block[1], block[2]
        m = re.search(r'\(([^)]+\.cs)\)', title)
        if not m:
            problems.append(f'{title}: не вказано файл у дужках')
            continue
        path = CODE_ROOT / m.group(1)
        if not path.exists():
            problems.append(f'{title}: немає файлу {m.group(1)}')
            continue
        known = {line.strip() for line in path.read_text(encoding='utf-8').split('\n')}
        for line in code:
            if line.strip() and line.strip() not in known:
                problems.append(f'{title}: у {m.group(1)} немає рядка «{line.strip()}»')
    if problems:
        print('Лістинги не збігаються з кодом:')
        for p in problems:
            print('  -', p)
        raise SystemExit(1)
    print('лістинги збігаються з вихідним кодом')


def estimate_pages(blocks):
    page_h, y, page = 728.0, 0.0, FIRST_CONTENT_PAGE
    headings = []

    def place(height, keep_with=0.0):
        nonlocal y, page
        if y + height + keep_with > page_h and y > 0:
            page += 1
            y = 0.0
        y += height

    for block in blocks:
        kind = block[0]
        if kind in ('h1', 'h2'):
            place(44 if kind == 'h1' else 36, keep_with=80)
            headings.append((kind, block[1], page))
        elif kind == 'p':
            place(math.ceil(len(strip_inline(block[1])) / 73) * 20 + 4)
        elif kind == 'li':
            place(math.ceil(len(strip_inline(block[1])) / 68) * 20 + 3)
        elif kind == 'code':
            place(27 + sum(max(1, math.ceil(len(l) / 88)) for l in block[2]) * 11.5 + 24)
        elif kind == 'fig':
            w, h = png_size(ROOT / block[2])
            width_pt = min(6.0 * 72, 7.2 * 72 * w / h)
            place(width_pt * h / w + 40)
        elif kind == 'table':
            ncols = len(block[2])
            chars = max(6, int(TEXT_WIDTH_IN * 72 / ncols / 4.9))
            height = 27 if block[1] else 0
            for row in [block[2]] + block[3]:
                height_row = max(math.ceil(len(strip_inline(c)) / chars) for c in row) * 12.1 + 12
                height += height_row
            remaining = page_h - y
            if height > remaining and remaining > 120:
                y = page_h - 0
                place(0)
            place(min(height, page_h) if height <= page_h else height - page_h * (math.ceil(height / page_h) - 1))
            if height > page_h:
                page += math.ceil(height / page_h) - 1
    return headings


def png_size(path):
    with open(path, 'rb') as f:
        f.read(16)
        return struct.unpack('>II', f.read(8))


def set_run(run, size, bold=False, italic=False, mono=False):
    run.font.name = 'Consolas' if mono else 'Times New Roman'
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    run.font.color.rgb = BLACK


def add_inline(paragraph, text, size, bold=False):
    for token in re.split(r'(\*\*.+?\*\*|`[^`]+`)', text):
        if not token:
            continue
        if token.startswith('**'):
            set_run(paragraph.add_run(token[2:-2]), size, bold=True)
        elif token.startswith('`'):
            set_run(paragraph.add_run(token[1:-1]), size - 1.5, mono=True)
        else:
            set_run(paragraph.add_run(token), size, bold=bold)


def style_headings(doc):
    for name, size, before, after in (('Heading 1', 16, 18, 8), ('Heading 2', 14, 14, 6)):
        style = doc.styles[name]
        style.font.name = 'Times New Roman'
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.italic = False
        style.font.color.rgb = BLACK
        rfonts = style.element.rPr.rFonts
        for attr in ('w:asciiTheme', 'w:hAnsiTheme', 'w:eastAsiaTheme', 'w:cstheme'):
            if rfonts.get(qn(attr)) is not None:
                del rfonts.attrib[qn(attr)]
        rfonts.set(qn('w:eastAsia'), 'Times New Roman')
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True
    for name, indent in (('toc 1', 0.0), ('toc 2', 0.3)):
        style = doc.styles.add_style(name, WD_STYLE_TYPE.PARAGRAPH)
        style.base_style = doc.styles['Normal']
        style.font.name = 'Times New Roman'
        style.font.size = Pt(12)
        style.font.bold = name == 'toc 1'
        style.font.color.rgb = BLACK
        style.paragraph_format.left_indent = Inches(indent)
        style.paragraph_format.space_after = Pt(2)
        style.paragraph_format.tab_stops.add_tab_stop(Inches(TEXT_WIDTH_IN), WD_TAB_ALIGNMENT.RIGHT, WD_TAB_LEADER.DOTS)


def add_title_page(doc):
    def centered(text, size, bold=False, before=0, after=0):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(before)
        p.paragraph_format.space_after = Pt(after)
        p.paragraph_format.line_spacing = 1.15
        set_run(p.add_run(text), size, bold=bold)
        return p

    centered('КИЇВСЬКИЙ НАЦІОНАЛЬНИЙ УНІВЕРСИТЕТ ІМЕНІ ТАРАСА ШЕВЧЕНКА', 13, bold=True, after=2)
    centered('Факультет інформаційних технологій\nКафедра програмних систем і технологій', 13, after=40)
    centered('ЗВІТ', 20, bold=True, before=30, after=8)
    centered("з лабораторної роботи № 5\nз дисципліни «Об'єктно-орієнтоване програмування»", 14, after=14)
    centered('на тему: «CRUD, ACID, повнота функціоналу»\n'
             'Варіант: «Система автоматизації діяльності агентства земельних ділянок (LandAgencyMvc)»', 13, before=6, after=60)
    meta = doc.add_paragraph()
    meta.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    meta.paragraph_format.space_before = Pt(40)
    meta.paragraph_format.space_after = Pt(70)
    set_run(meta.add_run('Виконав:\nстудент групи ІПЗ-23\nСивопляс Максим\n\n'
                         'Перевірила:\nдоцент кафедри ПСТ,\nканд. техн. наук, доц.\nЗубик Л. В.'), 13)
    centered('Київ — 2026', 13, before=20)
    doc.add_page_break()


def field_run(paragraph, kind=None, instruction=None):
    run = paragraph.add_run()
    if kind:
        el = OxmlElement('w:fldChar')
        el.set(qn('w:fldCharType'), kind)
    else:
        el = OxmlElement('w:instrText')
        el.set(qn('xml:space'), 'preserve')
        el.text = instruction
    run._r.append(el)


def add_toc(doc, headings):
    heading = doc.add_paragraph()
    heading.paragraph_format.space_after = Pt(8)
    set_run(heading.add_run('ЗМІСТ'), 16, bold=True)
    for index, (kind, title, page) in enumerate(headings):
        p = doc.add_paragraph(style='toc 1' if kind == 'h1' else 'toc 2')
        if index == 0:
            field_run(p, 'begin')
            field_run(p, instruction=' TOC \\o "1-2" \\h \\z \\u ')
            field_run(p, 'separate')
        p.add_run(f'{title}\t{page}')
        if index == len(headings) - 1:
            field_run(p, 'end')
    doc.add_page_break()


def add_paragraph(doc, text):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.first_line_indent = Inches(0.49)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.25
    add_inline(p, text, 14)


def add_bullet(doc, text):
    numbered = re.match(r'\d+\. ', text)
    p = doc.add_paragraph(style=None if numbered else 'List Bullet')
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.25
    if numbered:
        p.paragraph_format.first_line_indent = Inches(0.49)
    add_inline(p, text, 14)


def add_listing(doc, title, code):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(10)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.keep_with_next = True
    set_run(p.add_run(title), 12, bold=True)
    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = tbl.cell(0, 0)
    helpers.set_cell_background(cell, 'F9F9F9')
    helpers.set_cell_margins(cell, top=100, bottom=100, left=140, right=140)
    border = {'val': 'single', 'sz': 4, 'color': '444444'}
    helpers.set_cell_borders(cell, top=border, bottom=border, left=border, right=border)
    paragraph = cell.paragraphs[0]
    paragraph.paragraph_format.space_after = Pt(0)
    paragraph.paragraph_format.line_spacing = 1.05
    for i, line in enumerate(code):
        run = paragraph.add_run(line + ('\n' if i < len(code) - 1 else ''))
        set_run(run, 9.5, mono=True)
    doc.add_paragraph().paragraph_format.space_after = Pt(6)


def add_figure(doc, caption, rel_path):
    path = ROOT / rel_path
    w, h = png_size(path)
    width = min(6.0, 7.2 * w / h)
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(8)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.keep_with_next = True
    p.add_run().add_picture(str(path), width=Inches(width))
    cap = doc.add_paragraph()
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap.paragraph_format.space_after = Pt(12)
    set_run(cap.add_run(caption), 12, italic=True)


def add_table(doc, caption, headers, rows):
    weights = [max(8, min(40, max(len(strip_inline(r[j])) for r in [headers] + rows))) for j in range(len(headers))]
    total = sum(weights)
    widths = [Inches(TEXT_WIDTH_IN * w / total) for w in weights]
    helpers.add_table_data(doc, [strip_inline(h) for h in headers], [[strip_inline(c) for c in r] for r in rows],
                           col_widths=widths, title_str=strip_inline(caption) if caption else None)


def build():
    blocks = parse_blocks(SOURCE.read_text(encoding='utf-8'))
    verify_listings(blocks)
    headings = estimate_pages(blocks)

    doc = docx.Document()
    doc.settings.element.append(parse_xml(r'<w:updateFields %s w:val="true"/>' % nsdecls('w')))
    helpers.setup_page_numbering(doc)
    style_headings(doc)
    add_title_page(doc)
    add_toc(doc, headings)

    for block in blocks:
        kind = block[0]
        if kind in ('h1', 'h2'):
            doc.add_paragraph(block[1], style='Heading 1' if kind == 'h1' else 'Heading 2')
        elif kind == 'p':
            add_paragraph(doc, block[1])
        elif kind == 'li':
            add_bullet(doc, block[1])
        elif kind == 'code':
            add_listing(doc, block[1], block[2])
        elif kind == 'fig':
            add_figure(doc, block[1], block[2])
        elif kind == 'table':
            add_table(doc, block[1], block[2], block[3])
    doc.save(OUTPUT)
    print(f'збережено {OUTPUT.name}; орієнтовно {headings[-1][2]} сторінок')


if __name__ == '__main__':
    build()
