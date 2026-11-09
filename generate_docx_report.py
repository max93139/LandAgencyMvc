#!/usr/bin/env python3
"""
Генератор офіційного академічного звіту з лабораторної роботи № 3 (ДСТУ 3008:2015)
для студента Сивопляса Максима (група ІПЗ-23), КНУ імені Тараса Шевченка.
Дисципліна: «Об'єктно-орієнтоване програмування»
Тема: «Створення проекту і файлу локальної бази даних. Основи LINQ»
Варіант: «Система автоматизації діяльності агентства земельних ділянок (LandAgencyMvc)»
Вимога: виключно чорний колір тексту, фінальний результат ЛР №3 без проміжних релізів.
"""

import os
import docx
from docx.shared import Inches, Pt, RGBColor, Mm
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

BLACK = RGBColor(0, 0, 0)

def set_cell_background(cell, fill_hex):
    shading_xml = f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>'
    cell._tc.get_or_add_tcPr().append(parse_xml(shading_xml))

def set_cell_margins(cell, top=100, bottom=100, left=140, right=140):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def set_cell_borders(cell, top=None, bottom=None, left=None, right=None):
    tcPr = cell._tc.get_or_add_tcPr()
    tcBorders = OxmlElement('w:tcBorders')
    borders = {'top': top, 'bottom': bottom, 'left': left, 'right': right}
    for b_name, b_val in borders.items():
        if b_val:
            node = OxmlElement(f'w:{b_name}')
            node.set(qn('w:val'), b_val.get('val', 'single'))
            node.set(qn('w:sz'), str(b_val.get('sz', 4)))
            node.set(qn('w:space'), '0')
            node.set(qn('w:color'), b_val.get('color', '000000'))
            tcBorders.append(node)
        else:
            node = OxmlElement(f'w:{b_name}')
            node.set(qn('w:val'), 'none')
            tcBorders.append(node)
    tcPr.append(tcBorders)

def add_title_page(doc):
    p1 = doc.add_paragraph()
    p1.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p1.paragraph_format.space_before = Pt(0)
    p1.paragraph_format.space_after = Pt(2)
    p1.paragraph_format.line_spacing = 1.15
    r1 = p1.add_run("КИЇВСЬКИЙ НАЦІОНАЛЬНИЙ УНІВЕРСИТЕТ ІМЕНІ ТАРАСА ШЕВЧЕНКА")
    r1.font.name = "Times New Roman"
    r1.font.size = Pt(13)
    r1.font.bold = True
    r1.font.color.rgb = BLACK

    p2 = doc.add_paragraph()
    p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p2.paragraph_format.space_before = Pt(2)
    p2.paragraph_format.space_after = Pt(40)
    r2 = p2.add_run("Факультет інформаційних технологій\nКафедра програмних систем і технологій")
    r2.font.name = "Times New Roman"
    r2.font.size = Pt(13)
    r2.font.color.rgb = BLACK

    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_before = Pt(30)
    p_title.paragraph_format.space_after = Pt(8)
    r_t = p_title.add_run("ЗВІТ")
    r_t.font.name = "Times New Roman"
    r_t.font.size = Pt(20)
    r_t.font.bold = True
    r_t.font.color.rgb = BLACK

    p_sub = doc.add_paragraph()
    p_sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_sub.paragraph_format.space_before = Pt(4)
    p_sub.paragraph_format.space_after = Pt(14)
    r_s = p_sub.add_run("з лабораторної роботи № 3\nз дисципліни «Об'єктно-орієнтоване програмування»")
    r_s.font.name = "Times New Roman"
    r_s.font.size = Pt(14)
    r_s.font.bold = True
    r_s.font.color.rgb = BLACK

    p_topic = doc.add_paragraph()
    p_topic.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_topic.paragraph_format.space_before = Pt(6)
    p_topic.paragraph_format.space_after = Pt(60)
    r_top = p_topic.add_run("на тему: «Створення проекту і файлу локальної бази даних. Основи LINQ»\n"
                            "Варіант: «Система автоматизації діяльності агентства земельних ділянок (LandAgencyMvc)»")
    r_top.font.name = "Times New Roman"
    r_top.font.size = Pt(13)
    r_top.font.italic = True
    r_top.font.color.rgb = BLACK

    p_meta = doc.add_paragraph()
    p_meta.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p_meta.paragraph_format.space_before = Pt(40)
    p_meta.paragraph_format.space_after = Pt(70)
    p_meta.paragraph_format.line_spacing = 1.25
    r_m = p_meta.add_run(
        "Виконав:\n"
        "студент групи ІПЗ-23\n"
        "Сивопляс Максим\n\n"
        "Перевірила:\n"
        "доцент кафедри ПСТ,\n"
        "канд. техн. наук, доц.\n"
        "Зубик Л. В."
    )
    r_m.font.name = "Times New Roman"
    r_m.font.size = Pt(13)
    r_m.font.color.rgb = BLACK

    p_year = doc.add_paragraph()
    p_year.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_year.paragraph_format.space_before = Pt(20)
    p_year.paragraph_format.space_after = Pt(0)
    r_y = p_year.add_run("Київ — 2026")
    r_y.font.name = "Times New Roman"
    r_y.font.size = Pt(13)
    r_y.font.color.rgb = BLACK

    doc.add_page_break()

def add_heading_1(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(18)
    p.paragraph_format.space_after = Pt(8)
    p.paragraph_format.keep_with_next = True
    p.paragraph_format.first_line_indent = Inches(0)
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(16)
    run.font.bold = True
    run.font.color.rgb = BLACK
    return p

def add_heading_2(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(14)
    p.paragraph_format.space_after = Pt(6)
    p.paragraph_format.keep_with_next = True
    p.paragraph_format.first_line_indent = Inches(0)
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(14)
    run.font.bold = True
    run.font.color.rgb = BLACK
    return p

def add_body_paragraph(doc, text, bold_prefix=None):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.first_line_indent = Inches(0.49)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.25

    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = "Times New Roman"
        r_pre.font.size = Pt(14)
        r_pre.font.bold = True
        r_pre.font.color.rgb = BLACK

    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(14)
    run.font.color.rgb = BLACK
    return p

def add_bullet_item(doc, text, bold_prefix=None):
    p = doc.add_paragraph(style='List Bullet')
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.25

    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = "Times New Roman"
        r_pre.font.size = Pt(14)
        r_pre.font.bold = True
        r_pre.font.color.rgb = BLACK

    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(14)
    run.font.color.rgb = BLACK
    return p

def add_code_listing(doc, title, code_lines):
    p_t = doc.add_paragraph()
    p_t.paragraph_format.space_before = Pt(10)
    p_t.paragraph_format.space_after = Pt(3)
    p_t.paragraph_format.keep_with_next = True
    r_t = p_t.add_run(f"Лістинг — {title}")
    r_t.font.name = "Times New Roman"
    r_t.font.size = Pt(12)
    r_t.font.bold = True
    r_t.font.color.rgb = BLACK

    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = tbl.cell(0, 0)
    cell.width = Inches(6.5)
    set_cell_background(cell, "F9F9F9")
    set_cell_margins(cell, top=100, bottom=100, left=140, right=140)
    b_val = {'val': 'single', 'sz': 4, 'color': '444444'}
    set_cell_borders(cell, top=b_val, bottom=b_val, left=b_val, right=b_val)

    p = cell.paragraphs[0]
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.05

    for i, line in enumerate(code_lines):
        r = p.add_run(line + ("\n" if i < len(code_lines) - 1 else ""))
        r.font.name = "Consolas"
        r.font.size = Pt(9.5)
        r.font.color.rgb = BLACK

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(2)
    p_after.paragraph_format.space_after = Pt(6)

def add_figure(doc, img_path, caption_text, width=Inches(6.2)):
    if os.path.exists(img_path):
        p_img = doc.add_paragraph()
        p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_img.paragraph_format.space_before = Pt(8)
        p_img.paragraph_format.space_after = Pt(3)
        p_img.paragraph_format.keep_with_next = True
        run = p_img.add_run()
        run.add_picture(img_path, width=width)

        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_cap.paragraph_format.space_before = Pt(2)
        p_cap.paragraph_format.space_after = Pt(12)
        r_cap = p_cap.add_run(caption_text)
        r_cap.font.name = "Times New Roman"
        r_cap.font.size = Pt(12)
        r_cap.font.italic = True
        r_cap.font.color.rgb = BLACK

def add_table_data(doc, headers, rows, col_widths=None, title_str=None):
    if title_str:
        p_t = doc.add_paragraph()
        p_t.paragraph_format.space_before = Pt(10)
        p_t.paragraph_format.space_after = Pt(3)
        p_t.paragraph_format.keep_with_next = True
        p_t.paragraph_format.first_line_indent = Inches(0)
        r_t = p_t.add_run(title_str)
        r_t.font.name = "Times New Roman"
        r_t.font.size = Pt(12)
        r_t.font.bold = True
        r_t.font.color.rgb = BLACK

    tbl = doc.add_table(rows=len(rows) + 1, cols=len(headers))
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER

    hdr_border = {'val': 'single', 'sz': 6, 'color': '000000'}
    cell_border = {'val': 'single', 'sz': 4, 'color': '555555'}

    for j, h_text in enumerate(headers):
        cell = tbl.cell(0, j)
        if col_widths and j < len(col_widths):
            cell.width = col_widths[j]
        set_cell_background(cell, "F2F2F2")
        set_cell_margins(cell, top=100, bottom=100, left=120, right=120)
        set_cell_borders(cell, top=hdr_border, bottom=hdr_border, left=cell_border, right=cell_border)
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(2)
        p.paragraph_format.space_after = Pt(2)
        r = p.add_run(h_text)
        r.font.name = "Times New Roman"
        r.font.size = Pt(11)
        r.font.bold = True
        r.font.color.rgb = BLACK

    for i, row in enumerate(rows):
        bg = "FFFFFF" if i % 2 == 0 else "FAFAFA"
        for j, val in enumerate(row):
            cell = tbl.cell(i + 1, j)
            if col_widths and j < len(col_widths):
                cell.width = col_widths[j]
            set_cell_background(cell, bg)
            set_cell_margins(cell, top=80, bottom=80, left=120, right=120)
            set_cell_borders(cell, top=cell_border, bottom=cell_border, left=cell_border, right=cell_border)
            p = cell.paragraphs[0]
            p.paragraph_format.space_before = Pt(2)
            p.paragraph_format.space_after = Pt(2)
            p.paragraph_format.line_spacing = 1.1
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT if j > 0 and len(val) > 15 else WD_ALIGN_PARAGRAPH.CENTER
            r = p.add_run(str(val))
            r.font.name = "Times New Roman"
            r.font.size = Pt(10.5)
            r.font.color.rgb = BLACK

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_after = Pt(6)

def setup_page_numbering(doc):
    for section in doc.sections:
        section.top_margin = Mm(20)
        section.bottom_margin = Mm(20)
        section.left_margin = Mm(25)
        section.right_margin = Mm(15)
        section.footer_distance = Mm(12)
        section.different_first_page_header_footer = True

        first_footer = section.first_page_footer
        for p in first_footer.paragraphs:
            p.text = ""

        footer = section.footer
        p_footer = footer.paragraphs[0] if footer.paragraphs else footer.add_paragraph()
        p_footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_footer.paragraph_format.space_before = Pt(0)
        p_footer.paragraph_format.space_after = Pt(0)
        p_footer.text = ""

        fld = parse_xml(r'''
            <w:fldSimple %s w:instr="PAGE">
                <w:r>
                    <w:rPr>
                        <w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>
                        <w:sz w:val="24"/>
                        <w:szCs w:val="24"/>
                        <w:color w:val="000000"/>
                    </w:rPr>
                </w:r>
            </w:fldSimple>
        ''' % nsdecls('w'))
        p_footer._p.append(fld)

def add_table_of_contents(doc):
    add_heading_1(doc, "ЗМІСТ")
    toc_items = [
        ("1 Тема, мета та завдання роботи", "3"),
        ("2 Архітектура Code First та структура бази даних", "4"),
        ("    2.1 Реалізація підходу Code First та класи моделі", "4"),
        ("    2.2 Ключі, унікальні індекси та обмеження цілісності Restrict", "6"),
        ("    2.3 Особливості обробки типу decimal у SQLite", "8"),
        ("3 Заповнення таблиць даними та ідемпотентний посів", "9"),
        ("    3.1 Початкові та історичні дані", "9"),
        ("    3.2 Статистика наповнення бази даних", "11"),
        ("4 Запити до бази даних (LINQ to Entities та LINQ to Objects)", "12"),
        ("    4.1 Запит 1 — Багатопараметричний відбір ділянок та точне сортування", "12"),
        ("    4.2 Запити 2 і 3 — Персональна фінансова аналітика рієлтора за період", "15"),
        ("    4.3 Фактичні числові результати періодизації закритих угод", "18"),
        ("    4.4 Запит 4 — Комплексний аудит земельного банку (з'єднання 4 таблиць)", "19"),
        ("    4.5 Запити 5–8 — Безпечна автентифікація, додавання, зміна та вилучення", "21"),
        ("    4.6 Запит 9 — Комплексна демонстрація операторів LINQ (Showcase)", "24"),
        ("5 Органічна інтеграція у веб-інтерфейс та рольова безпека", "28"),
        ("    5.1 Бізнес-модулі платформи та строго розділена навігація", "28"),
        ("    5.2 Очищення інтерфейсу від нелогічних і надлишкових елементів", "30"),
        ("    5.3 Компонентна архітектура та захист від витоку даних", "31"),
        ("Висновки", "33"),
        ("Перелік використаних джерел", "34")
    ]

    for title, pg in toc_items:
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(1)
        p.paragraph_format.space_after = Pt(2)
        p.paragraph_format.line_spacing = 1.15
        p.paragraph_format.first_line_indent = Inches(0)
        
        r1 = p.add_run(title)
        r1.font.name = "Times New Roman"
        r1.font.size = Pt(12)
        r1.font.color.rgb = BLACK
        if not title.startswith("    "):
            r1.font.bold = True

        dots_count = max(2, 75 - len(title))
        r_dots = p.add_run(" " + "." * dots_count + " ")
        r_dots.font.name = "Times New Roman"
        r_dots.font.size = Pt(10)
        r_dots.font.color.rgb = BLACK

        r2 = p.add_run(pg)
        r2.font.name = "Times New Roman"
        r2.font.size = Pt(12)
        r2.font.color.rgb = BLACK
        if not title.startswith("    "):
            r2.font.bold = True

    doc.add_page_break()

def build_docx_report():
    doc = docx.Document()

    element = parse_xml(r'<w:updateFields %s w:val="true"/>' % nsdecls('w'))
    doc.settings.element.append(element)

    setup_page_numbering(doc)
    add_title_page(doc)
    add_table_of_contents(doc)

    # 1. ТЕМА, МЕТА ТА ЗАВДАННЯ РОБОТИ
    add_heading_1(doc, "1 Тема, мета та завдання роботи")
    add_body_paragraph(doc, "Створення проєкту і файлу локальної бази даних. Основи LINQ.", bold_prefix="Тема роботи: ")
    add_body_paragraph(doc, "Ознайомитися з основами мови інтегрованих запитів LINQ (Language Integrated Query) у середовищі .NET 10, побудувати реляційну базу даних за підходом Code First на основі концептуальної моделі предметної області «Агентство земельних ділянок», зв'язати таблиці зовнішніми ключами з обмеженнями цілісності Restrict, заповнити їх узгодженими репрезентативними даними та реалізувати комплекс аналітичних і маніпулятивних запитів із відображенням результатів у захищеному веб-інтерфейсі ASP.NET Core MVC.", bold_prefix="Мета роботи: ")

    add_body_paragraph(doc, "Відповідно до методичних вказівок кафедри, у процесі виконання лабораторної роботи № 3 реалізовано такі ключові завдання:")
    add_bullet_item(doc, "розробка локальної бази даних на 8 взаємопов'язаних таблиць, що строго відповідають доменній моделі та архітектурі системи;", bold_prefix="1. Модель даних: ")
    add_bullet_item(doc, "повна реалізація підходу Code First з конфігурацією сутностей через Entity Framework Core 10 Fluent API;", bold_prefix="2. Підхід Code First: ")
    add_bullet_item(doc, "налаштування первинних і зовнішніх ключів, унікальних індексів та захисту реляційної цілісності DeleteBehavior.Restrict;", bold_prefix="3. Цілісність даних: ")
    add_bullet_item(doc, "ідемпотентний посів бази даними обсягом від 10 до 28 рядків у кожній таблиці (сумарно 130 записів) з історією угод за останні 12 місяців;", bold_prefix="4. Наповнення: ")
    add_bullet_item(doc, "розробка 9 різноманітних LINQ-запитів: відбір із багатопараметричною фільтрацією, персональні фінансові підсумки рієлтора за період, з'єднання 4 таблиць, безпечні CRUD-операції з PBKDF2 та комплексна вітрина операторів (Showcase);", bold_prefix="5. Запити LINQ: ")
    add_bullet_item(doc, "повне покриття сімейств операторів LINQ: квантори (All, Any), операції над множинами (Distinct, Intersect, Except, Union), згладжування (SelectMany), згортка (Aggregate), попарне зшивання (Zip), пагінація (Skip/Take) та паралельні запити PLINQ (.AsParallel());", bold_prefix="6. Оператори LINQ: ")
    add_bullet_item(doc, "органічна інтеграція LINQ-запитів у бізнес-модулі реальної програми (Каталог ділянок клієнта, Персональна фінансова аналітика рієлтора, Реєстр земельного банку юриста, CRM клієнтів) з надійним захистом ролей та відсутністю інлайн-стилів;", bold_prefix="7. Веб-інтерфейс: ")
    add_bullet_item(doc, "оформлення академічного звіту відповідно до ДСТУ 3008:2015 з наскрізною нумерацією, змістом та ілюстраціями роботи системи.", bold_prefix="8. Документування: ")

    add_body_paragraph(doc, "Методичні вказівки орієнтовані на створення бази у Visual Studio під ОС Windows через SQL Server Express LocalDB. Оскільки розробка та тестування здійснювалися на платформі macOS (Apple Silicon), де SQL Server LocalDB недоступний на рівні ОС, відповідно до пункту 4 завдань як СУБД використано SQLite за технологією Entity Framework Core Code First. База зберігається в одному файлі landagency.db, забезпечує локальність, повну транзакційну безпеку стандарту ACID та 100% сумісність із провайдером LINQ to Entities.")

    # 2. АРХІТЕКТУРА CODE FIRST ТА СТРУКТУРА БАЗИ ДАНИХ
    add_heading_1(doc, "2 Архітектура Code First та структура бази даних")
    add_heading_2(doc, "2.1 Реалізація підходу Code First та класи моделі")
    add_body_paragraph(doc, "У проєкті застосовано класичний підхід Code First: структура реляційної бази даних формується безпосередньо з C#-класів доменної моделі, розміщених у просторі імен LandAgencyMvc.Models. Контекст даних LandAgencyContext наслідується від Microsoft.EntityFrameworkCore.DbContext та реєструє 8 наборів сутностей DbSet<T>.")

    add_figure(doc, "docs/images/fig01_er_diagram.png", "Рисунок 1 — Реляційна структура бази даних земельного агентства (8 таблиць, зв'язки та ключі)")

    t1_headers = ["Таблиця СУБД", "Клас моделі", "Опис сутності та бізнес-призначення", "Ключі та обмеження"]
    t1_rows = [
        ["Users", "AppUser", "Користувачі платформи (клієнти, продавці, рієлтори, юристи, адмін)", "PK Id; UNIQUE Email; PBKDF2 PasswordHash"],
        ["LandPlots", "LandPlot", "Земельні ділянки для продажу (кадастр, площа, призначення, ціна)", "PK Id; UNIQUE CadastralNumber; FK SellerId; FK RealtorId"],
        ["PlotDocuments", "PlotDocument", "Правовстановлюючі документи (витяги ДРРП, ДЗК, НГО)", "PK Id; FK LandPlotId; SHA-256 FileHash; IsVerified"],
        ["LegalConclusions", "LegalConclusion", "Висновки Due Diligence перевірки обтяжень, арештів та прав", "PK Id; UNIQUE LandPlotId (1:1); FK LawyerId; UNIQUE Number"],
        ["ViewingRequests", "ViewingRequest", "Заявки покупців на фізичний виїзний перегляд ділянок", "PK Id; FK LandPlotId; FK BuyerId; FK RealtorId"],
        ["SelectionRequests", "SelectionRequest", "Запити на автоматичний індивідуальний підбір ділянок", "PK Id; FK BuyerId; критерії площі, бюджету та локації"],
        ["SaleDeals", "SaleDeal", "Нотаріальні договори купівлі-продажу ділянок агентства", "PK Id; UNIQUE ContractNumber; FK LandPlotId; Restrict"],
        ["Payments", "Payment", "Банківські безготівкові платежі (вартість ділянки та комісія)", "PK Id; UNIQUE InvoiceNumber; FK SaleDealId; Restrict"]
    ]
    t1_widths = [Inches(1.1), Inches(1.1), Inches(2.7), Inches(1.6)]
    add_table_data(doc, t1_headers, t1_rows, t1_widths, title_str="Таблиця 1 — Опис реляційних таблиць бази даних LandAgency")

    add_heading_2(doc, "2.2 Ключі, унікальні індекси та обмеження цілісності Restrict")
    add_body_paragraph(doc, "Для забезпечення реляційної цілісності у методі OnModelCreating класу LandAgencyContext налаштовано конфігурацію зв'язків та обмежень:")
    add_bullet_item(doc, "унікальний індекс на Email у таблиці Users виключає дублювання облікових записів;", bold_prefix="Users.Email: ")
    add_bullet_item(doc, "унікальний індекс на CadastralNumber (формат ХХХХХХХХХХ:ХХ:ХХХ:ХХХХ) гарантує однозначну ідентифікацію земельного активу;", bold_prefix="LandPlots.CadastralNumber: ")
    add_bullet_item(doc, "зв'язок 1:1 між LandPlot та LegalConclusion гарантує наявність лише одного правового висновку на одну ділянку;", bold_prefix="LegalConclusions.LandPlotId: ")
    add_bullet_item(doc, "обмеження DeleteBehavior.Restrict застосовано до всіх фінансових та юридичних сутностей (SaleDeals, Payments, ViewingRequests), що блокує випадкове або зловмисне видалення користувачів чи ділянок, за якими зафіксована історія транзакцій.", bold_prefix="DeleteBehavior.Restrict: ")

    add_code_listing(doc, "Налаштування зв'язків та обмежень цілісності (LandAgencyContext.cs)", [
        "protected override void OnModelCreating(ModelBuilder modelBuilder)",
        "{",
        "    base.OnModelCreating(modelBuilder);",
        "",
        "    modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();",
        "    modelBuilder.Entity<LandPlot>().HasIndex(p => p.CadastralNumber).IsUnique();",
        "    modelBuilder.Entity<LegalConclusion>().HasIndex(c => c.ConclusionNumber).IsUnique();",
        "    modelBuilder.Entity<SaleDeal>().HasIndex(d => d.DraftContractNumber).IsUnique();",
        "    modelBuilder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();",
        "",
        "    modelBuilder.Entity<SaleDeal>()",
        "        .HasOne(d => d.Buyer).WithMany().HasForeignKey(d => d.BuyerId).OnDelete(DeleteBehavior.Restrict);",
        "    modelBuilder.Entity<SaleDeal>()",
        "        .HasOne(d => d.LandPlot).WithMany().HasForeignKey(d => d.LandPlotId).OnDelete(DeleteBehavior.Restrict);",
        "    modelBuilder.Entity<Payment>()",
        "        .HasOne(p => p.SaleDeal).WithMany(d => d.Payments).HasForeignKey(p => p.SaleDealId).OnDelete(DeleteBehavior.Restrict);",
        "}"
    ])

    add_heading_2(doc, "2.3 Особливості обробки типу decimal у SQLite")
    add_body_paragraph(doc, "У провайдері Microsoft.EntityFrameworkCore.Sqlite тип C# decimal транслюється у колонку TEXT, оскільки SQLite не підтримує нативний 128-бітний фінансовий тип. Через це SQL-оператор ORDER BY виконує лексикографічне впорядкування (рядок '120000.0' вважається меншим за '18000.0'). Для розв'язання цієї проблеми в сервісі LandAgencyQueries реалізовано двофазний конвеєр: фільтрація умов WHERE транслюється у SQL через LINQ to Entities, після чого дані матеріалізуються через ToListAsync() і сортування за ціною здійснюється в пам'яті (LINQ to Objects) з повною точністю decimal.")

    # 3. ЗАПОВНЕННЯ ТАБЛИЦЬ ДАНИМИ
    add_heading_1(doc, "3 Заповнення таблиць даними та ідемпотентний посів")
    add_body_paragraph(doc, "Базу даних наповнено історичними та валідними даними за 2025–2026 роки через клас LandAgencyHistorySeed. Дані відображають повний операційний цикл земельного агентства: виставлення лотів на перевірку, Due Diligence юристів, замовлення переглядів, нотаріальні договори та безготівкові платежі.")

    t2_headers = ["Таблиця", "Фактична к-ть рядків", "Вимога (>=10)", "Характеристика наповнення"]
    t2_rows = [
        ["Users", "14", "Виконано (14 >= 10)", "1 адмін, 2 рієлтори, 2 юристи, 3 продавці, 6 покупців"],
        ["LandPlots", "16", "Виконано (16 >= 10)", "Київська, Львівська, Івано-Франківська обл. (0.08–5.40 га)"],
        ["PlotDocuments", "16", "Виконано (16 >= 10)", "Витяги ДЗК, свідоцтва ДРРП із розрахованим SHA-256"],
        ["LegalConclusions", "16", "Виконано (16 >= 10)", "12 схвалених (чисті), 3 з ризиками, 1 відхилений"],
        ["ViewingRequests", "16", "Виконано (16 >= 10)", "Заявки на огляд ділянок клієнтами з рієлторами"],
        ["SelectionRequests", "10", "Виконано (10 >= 10)", "Параметричні запити на автопідбір за бюджетом і КВЦПЗ"],
        ["SaleDeals", "14", "Виконано (14 >= 10)", "Нотаріальні договори за 12 місяців на загальну суму $664 500"],
        ["Payments", "28", "Виконано (28 >= 10)", "Безготівкові інвойси: вартість ділянок та комісія 3% ($19 935)"]
    ]
    t2_widths = [Inches(1.4), Inches(1.3), Inches(1.5), Inches(2.3)]
    add_table_data(doc, t2_headers, t2_rows, t2_widths, title_str="Таблиця 2 — Фактичні обсяги наповнення таблиць landagency.db")

    add_body_paragraph(doc, "Сумарна кількість записів у 8 таблицях бази даних становить 130 рядків, що значно перевищує встановлений методичними вказівками мінімум. Завдяки ідемпотентності процедури посіву повторні запуски вебдодатка не продукують дублікатів і не спотворюють фінансові підсумки.")

    # 4. ЗАПИТИ ДО БАЗИ ДАНИХ
    add_heading_1(doc, "4 Запити до бази даних (LINQ to Entities та LINQ to Objects)")
    add_body_paragraph(doc, "Усі аналітичні та транзакційні запити інкапсульовані в інтерфейсі ILandAgencyQueries та його реалізації LandAgencyQueries. Застосовано різні синтаксичні форми LINQ: декларативний Query Syntax (from...in...select) та ланцюжковий Method Syntax з типізованими лямбда-виразами.")

    add_heading_2(doc, "4.1 Запит 1 — Багатопараметричний відбір ділянок та точне сортування")
    add_body_paragraph(doc, "Запит реалізує гнучку фільтрацію за цільовим призначенням, діапазоном площі, максимальним бюджетом, датою внесення та локацією із безрегістровим пошуком кирилицею:")

    add_code_listing(doc, "Запит 1 — Багатопараметрична фільтрація (LandAgencyQueries.cs)", [
        "public async Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter)",
        "{",
        "    var query = _db.LandPlots.AsNoTracking();",
        "    if (!string.IsNullOrWhiteSpace(filter.Region))",
        "        query = query.Where(p => p.Location.Contains(filter.Region));",
        "    if (!string.IsNullOrWhiteSpace(filter.Purpose))",
        "        query = query.Where(p => p.Purpose.Contains(filter.Purpose));",
        "    if (filter.MinPrice.HasValue)",
        "        query = query.Where(p => p.PriceUsd >= filter.MinPrice.Value);",
        "    if (filter.MaxPrice.HasValue)",
        "        query = query.Where(p => p.PriceUsd <= filter.MaxPrice.Value);",
        "    if (filter.MinArea.HasValue)",
        "        query = query.Where(p => p.AreaHectares >= filter.MinArea.Value);",
        "    if (filter.MaxArea.HasValue)",
        "        query = query.Where(p => p.AreaHectares <= filter.MaxArea.Value);",
        "",
        "    var projected = query.Take(300).Select(p => new PlotRow(...));",
        "    string sql = projected.ToQueryString();",
        "    var rows = await projected.ToListAsync();",
        "    rows = filter.Descending ? rows.OrderByDescending(r => r.PriceUsd).ToList() : rows.OrderBy(r => r.PriceUsd).ToList();",
        "    return new QueryResult<PlotRow>(rows, sql);",
        "}"
    ])

    add_figure(doc, "docs/images/fig03_filter_plots.png", "Рисунок 2 — Екранна форма фільтрації ділянок із можливістю впорядкування")
    add_figure(doc, "docs/images/fig04_filter_sort_sql.png", "Рисунок 3 — Відображення трансляції LINQ to Entities у нативний код SQL (ToQueryString)")

    add_heading_2(doc, "4.2 Запити 2 і 3 — Персональна фінансова аналітика рієлтора за період")
    add_body_paragraph(doc, "Запит 2 розраховує агреговані фінансові показники (кількість угод, валовий оборот, сума комісії 3%, середній чек, мінімальна та максимальна ціна) у розрізі категорій земель КВЦПЗ. Відповідно до вимог конфіденційності та персональної звітності, запит параметризовано ідентифікатором поточного рієлтора (d.RealtorId == realtorId), що виключає доступ співробітника до чужої фінансової аналітики. Запит 3 формує реєстр особисто закритих нотаріальних угод рієлтора за обраний інтервал:")

    add_code_listing(doc, "Запит 2 — Агрегація персонального обороту рієлтора (Query Syntax)", [
        "public async Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(",
        "    Period period, TurnoverSort sort = TurnoverSort.Turnover, bool descending = true, int? realtorId = null)",
        "{",
        "    var baseQuery = _db.SaleDeals.AsNoTracking()",
        "        .Where(d => d.NotaryDate >= period.From && d.NotaryDate < period.To)",
        "        .Where(d => d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary);",
        "",
        "    if (realtorId.HasValue)",
        "        baseQuery = baseQuery.Where(d => d.RealtorId == realtorId.Value);",
        "",
        "    var query = from d in baseQuery",
        "                select new { d.LandPlotId, Purpose = d.LandPlot.Purpose, Area = d.LandPlot.AreaHectares, Price = d.FinalPriceUsd, Commission = d.AgencyCommissionUsd };",
        "    var rawDeals = await query.ToListAsync();",
        "",
        "    var grouped = from d in rawDeals",
        "                  group d by d.Purpose into g",
        "                  select new PurposeTurnoverRow(",
        "                      g.Key, g.Count(), Math.Round(g.Sum(x => x.Area), 2),",
        "                      g.Sum(x => x.Price), g.Sum(x => x.Commission),",
        "                      Math.Round(g.Average(x => x.Price), 2), g.Min(x => x.Price), g.Max(x => x.Price));",
        "    return new QueryResult<PurposeTurnoverRow>(grouped.ToList(), query.ToQueryString());",
        "}"
    ])

    add_figure(doc, "docs/images/fig05_period_turnover_month.png", "Рисунок 4 — Персональна фінансова аналітика рієлтора за місячний період (оборот, комісія 3% та категорії)")
    add_figure(doc, "docs/images/fig06_period_turnover_custom.png", "Рисунок 5 — Аналітичний звіт рієлтора за довільний діапазон дат із вибором критерію сортування")
    add_figure(doc, "docs/images/fig07_agent_performance.png", "Рисунок 6 — Деталізація закритих нотаріальних угод та персональної результативності")

    add_heading_2(doc, "4.3 Фактичні числові результати періодизації закритих угод")
    add_body_paragraph(doc, "На основі реальних даних landagency.db сформовано контрольні зрізи за різні звітні періоди:")
    add_bullet_item(doc, "1 угода (ДГ-2026-112, с. Нові Петрівці, 15 соток), оборот $34 000, комісія агентства $1 020;", bold_prefix="День (15.10.2026): ")
    add_bullet_item(doc, "1 угода (ДГ-2026-111, с. Поляниця, 25 соток), оборот $92 000, комісія агентства $2 760;", bold_prefix="Місяць (Вересень 2026): ")
    add_bullet_item(doc, "2 угоди (ДГ-2026-112 та ДГ-2026-113), оборот $62 500, комісія агентства $1 875;", bold_prefix="Місяць (Жовтень 2026): ")
    add_bullet_item(doc, "4 угоди (ДГ-2026-105..108), оборот $170 500, комісія агентства $5 115;", bold_prefix="I півріччя 2026 року: ")
    add_bullet_item(doc, "4 угоди (ДГ-2026-110..113), оборот $186 500, комісія агентства $5 595, топ-лот у с. Поляниця ($92 000).", bold_prefix="Довільний період (01.08.2026 – 31.10.2026): ")

    add_heading_2(doc, "4.4 Запит 4 — Комплексний аудит земельного банку (з'єднання 4 таблиць)")
    add_body_paragraph(doc, "Запит здійснює багатотабличне з'єднання (LandPlots ⨝ Users ⨝ PlotDocuments ⟕ LegalConclusions) для перевірки юридичної чистоти активів агентства:")

    add_figure(doc, "docs/images/fig08_land_bank_audit.png", "Рисунок 7 — Комплексний аудит безпеки активів земельного банку (з'єднання 4 таблиць)")

    add_heading_2(doc, "4.5 Запити 5–8 — Безпечна автентифікація, додавання, зміна та вилучення")
    add_body_paragraph(doc, "Запити 5–8 реалізують операції маніпулювання даними (CRUD) з дотриманням вимог безпеки:")
    add_bullet_item(doc, "Запит 5 здійснює пошук користувача за Email без пароля, а перевірка пароля виконується виключно через захищений POST-запит з автентифікацією хешу PBKDF2 (VerifyHashedPassword), що унеможливлює витік пароля в URL;", bold_prefix="Запит 5 (Auth): ")
    add_bullet_item(doc, "Запит 6 виконує атомарне додавання користувача та закріплення індивідуальної заявки на автопідбір у транзакції BeginTransactionAsync із захистом від дублювання Email;", bold_prefix="Запит 6 (Insert): ")
    add_bullet_item(doc, "Запит 7 оновлює реквізити користувача, підтримує зміну пароля та захищає системного адміністратора (admin@landagency.ua) від блокування IsActive;", bold_prefix="Запит 7 (Update): ")
    add_bullet_item(doc, "Запит 8 контролює обмеження цілісності DeleteBehavior.Restrict. Якщо за користувачем закріплені ділянки або нотаріальні угоди, вилучення безпечно блокується із поверненням інформативного повідомлення про порушення зв'язків цілісності.", bold_prefix="Запит 8 (Delete): ")

    add_figure(doc, "docs/images/fig09_user_search.png", "Рисунок 8 — Пошук картки клієнта в системі CRM за адресою електронної пошти")
    add_figure(doc, "docs/images/fig10_password_verify.png", "Рисунок 9 — Модальне вікно редагування даних клієнта та оновлення пароля")
    add_figure(doc, "docs/images/fig11_add_user_transaction.png", "Рисунок 10 — Реєстрація нового клієнта з внесенням до бази агентства")
    add_figure(doc, "docs/images/fig12_user_management_restrict.png", "Рисунок 11 — Реєстр клієнтів та блокування вилучення за обмеженням цілісності Restrict")

    add_heading_2(doc, "4.6 Запит 9 — Комплексна демонстрація операторів LINQ (Showcase)")
    add_body_paragraph(doc, "Відповідно до матеріалів Лекції № 9 та мети лабораторної роботи, метод GetLinqShowcaseAsync демонструє повне покриття сімейств операторів LINQ:")
    add_bullet_item(doc, "All (перевірка верифікації документів) та Any (наявність завершених нотаріальних угод);", bold_prefix="Квантори: ")
    add_bullet_item(doc, "Distinct (унікальні цільові призначення), Intersect (перетин районів покупців і пропозицій), Except (клієнти без угод), Union (об'єднання регіонів);", bold_prefix="Операції над множинами: ")
    add_bullet_item(doc, "SelectMany (проекція вкладених колекцій документів ділянок у плоский список найменувань);", bold_prefix="Згладжування: ")
    add_bullet_item(doc, "Aggregate (накопичення кадастрового реєстру сертифікованих ділянок за допомогою рядкового акумулятора);", bold_prefix="Агрегація: ")
    add_bullet_item(doc, "Zip (попарне комбінування ринкових ділянок із бенчмарками вартості для розрахунку відхилення ціни);", bold_prefix="Попарне зшивання: ")
    add_bullet_item(doc, "OrderBy + ThenByDescending (багатокритеріальне впорядкування вибірки за локацією та ціною);", bold_prefix="Впорядкування: ")
    add_bullet_item(doc, "Skip(2).Take(4) (секціонування даних для постраничної пагінації);", bold_prefix="Секціонування: ")
    add_bullet_item(doc, "AsParallel().Where(...) (паралельна багатопотокова обробка високобюджетних ділянок за технологією PLINQ).", bold_prefix="Паралельні запити PLINQ: ")

    add_figure(doc, "docs/images/fig13_linq_showcase_set_ops.png", "Рисунок 12 — Детальна картка ділянки та перевірка правовстановлюючих документів")
    add_figure(doc, "docs/images/fig14_linq_showcase_advanced_plinq.png", "Рисунок 13 — Зведена панель показників та фінансовий моніторинг за півріччя")

    # 5. ОРГАНІЧНА ІНТЕГРАЦІЯ У ВЕБ-ІНТЕРФЕЙС ТА РОЛЬОВА БЕЗПЕКА
    add_heading_1(doc, "5 Органічна інтеграція у веб-інтерфейс та рольова безпека")
    add_heading_2(doc, "5.1 Бізнес-модулі платформи та строго розділена навігація")
    add_body_paragraph(doc, "Усі розроблені запити LINQ та транзакційні операції CRUD не винесені у штучну навчальну вкладку, а органічно інтегровані безпосередньо у функціональні модулі платформи «Агентство земельних ділянок»:")
    add_body_paragraph(doc, "1. Каталог ділянок (ClientController.Catalog): Реалізує Запит 1 (відбір за довільним поєднанням 6 критеріїв — цільове призначення, діапазон бюджету, площа, дати публікації, текстовий пошук у ДЗК та точне сортування). Доступ захищено [Authorize(Roles = 'Client')].")
    add_body_paragraph(doc, "2. Фінансова аналітика рієлтора (RealtorController.Analytics): Реалізує Запити 2 та 3 (узагальнення фінансового обороту за день, місяць, півріччя або довільний період у розрізі категорій земель КВЦПЗ та реєстр особисто супроводжених угод). Доступ захищено [Authorize(Roles = 'Realtor,Admin')].")
    add_body_paragraph(doc, "3. Реєстр земельного банку юриста (LawyerController.LandBank): Реалізує Запит 4 (реляційне з'єднання 4 таблиць LandPlots ⨝ Users ⨝ PlotDocuments ⟕ LegalConclusions для комплексного аудиту безпеки активів, перевірки витягів ДРРП та ризиків ДЗК). Доступ захищено [Authorize(Roles = 'Lawyer,Admin')].")
    add_body_paragraph(doc, "4. Управління клієнтами та персоналом CRM (RealtorController.Clients): Реалізує Запити 5–8 (безпечна перевірка пароля методом POST за алгоритмом PBKDF2, атомарне транзакційне створення клієнта із заявкою, редагування профілю та вилучення з гарантією DeleteBehavior.Restrict).")

    add_heading_2(doc, "5.2 Очищення інтерфейсу від нелогічних і надлишкових елементів")
    add_body_paragraph(doc, "У процесі підготовки фінального продукту веб-застосунок очищено від усіх надлишкових та нелогічних елементів, що спотворювали користувацький досвід:")
    add_bullet_item(doc, "у верхньому меню кожного користувача відображаються лише релевантні розділи його ролі (клієнт бачить каталог і кабінет; рієлтор — запити, огляди, угоди, аналітику та клієнтів; юрист — чергу перевірок ДЗК, висновки, договори та земельний банк);", bold_prefix="Ізоляція навігації: ")
    add_bullet_item(doc, "каталог земельних ділянок закрито для неавторизованих користувачів атрибутом [Authorize], а пряме посилання приховано з екранів входу та реєстрації;", bold_prefix="Захист каталогу: ")
    add_bullet_item(doc, "рієлтор бачить лише власні угоди, виручку та комісію, що відповідає комерційній таємниці та принципам обліку результативності агентів;", bold_prefix="Персоналізація аналітики: ")
    add_bullet_item(doc, "вилучено штучні бейджі «AI/AutoMatching Активний», надлишкові кнопки повторного перерахунку збігів та передчасні кнопки зміни статусу оглядів;", bold_prefix="Ліквідація псевдо-елементів: ")
    add_bullet_item(doc, "з публічних форм клієнтського кабінету прибрано відображення сирих хеш-рядків SHA-256 та додано повноцінне редагування контактних даних клієнта.", bold_prefix="Безпека персональних даних: ")

    add_heading_2(doc, "5.3 Компонентна архітектура та захист від витоку даних")
    add_body_paragraph(doc, "Користувацький веб-інтерфейс спроєктовано за принципами адаптивного дизайну без використання інлайн-стилів style='...'. Усі класи оформлення (.la-card, .la-grid-3, .la-grid-4, .la-table, .la-badge, .la-topbar) визначені у централізованому файлі site.css. Доступ до аналітики та персональних даних суворо розмежовано: спроби несанкціонованого доступу перенаправляються на сторінку відмови у доступі (/Account/AccessDenied).")

    add_figure(doc, "docs/images/fig02_dashboard.png", "Рисунок 14 — Головна панель фінансової аналітики та контролю обороту рієлтора")

    # ВИСНОВКИ
    add_heading_1(doc, "Висновки")
    add_body_paragraph(doc, "1. У процесі виконання лабораторної роботи № 3 успішно опановано мову інтегрованих запитів LINQ (Language Integrated Query) та технологію Entity Framework Core 10 для створення, ініціалізації та аналітичної обробки реляційної бази даних на платформі .NET 10 за підходом Code First.")
    add_body_paragraph(doc, "2. Спроєктовано та реалізовано реляційну базу даних із 8 взаємопов'язаних таблиць (Users, LandPlots, PlotDocuments, LegalConclusions, ViewingRequests, SelectionRequests, SaleDeals, Payments), що строго відповідають концептуальній моделі предметної області агентства земельних ділянок.")
    add_body_paragraph(doc, "3. На рівні EF Core Fluent API налаштовано унікальні індекси та обмеження цілісності DeleteBehavior.Restrict, які гарантують збереження аудиторського сліду нотаріальних угод та юридичних висновків.")
    add_body_paragraph(doc, "4. Базу даних наповнено узгодженими даними обсягом від 10 до 28 записів у кожній таблиці (сумарно 130 записів) з історією угод за останні 12 місяців на загальну суму $664 500.")
    add_body_paragraph(doc, "5. Реалізовано 9 різнопланових LINQ-запитів, що поєднують серверне виконання LINQ to Entities, обробку decimal у LINQ to Objects та повне покриття сімейств операторів Лекції № 9 (All, Any, Distinct, Intersect, Except, Union, SelectMany, Aggregate, Zip, OrderBy/ThenBy, Skip/Take та PLINQ).")
    add_body_paragraph(doc, "6. Забезпечено органічну інтеграцію аналітики у функціональні модулі платформи (Каталог ділянок, Персональна фінансова аналітика рієлтора, Реєстр земельного банку юриста, CRM клієнтів), суворе рольове розділення прав доступу та повну ліквідацію штучних чи нелогічних елементів інтерфейсу.")

    # ДЖЕРЕЛА
    add_heading_1(doc, "Перелік використаних джерел")
    sources = [
        "Методичні вказівки до лабораторної роботи № 3 «Створення проекту і файлу локальної бази даних. Основи LINQ» з дисципліни «Об'єктно-орієнтоване програмування».",
        "Зубик Л. В. Лекція 9. LINQ; Лекція 8. Технологія ADO.NET. Проєктування баз даних. CRUD. Матеріали курсу «ООП-3 семестр-2026р.».",
        "ДСТУ 3008:2015. Інформація та документація. Звіти у сфері науки і техніки. Структура та правила оформлювання. — К. : ДП «УкрНДНЦ», 2016. — 26 с.",
        "Price M. J. C# 12 and .NET 8 – Modern Cross-Platform Development Fundamentals. 8th ed. — Birmingham : Packt Publishing, 2023. — 828 p.",
        "Microsoft Learn. Language Integrated Query (LINQ) in C#. URL: https://learn.microsoft.com/dotnet/csharp/linq/ (дата звернення: 08.11.2026).",
        "Microsoft Learn. Querying data with Entity Framework Core. URL: https://learn.microsoft.com/ef/core/querying/ (дата звернення: 08.11.2026).",
        "Microsoft Learn. SQLite EF Core Database Provider Limitations. URL: https://learn.microsoft.com/ef/core/providers/sqlite/limitations (дата звернення: 08.11.2026).",
        "Закон України «Про Державний земельний кадастр» від 07.07.2011 № 3613-VI. Відомості Верховної Ради України, 2012, № 8, ст. 61."
    ]
    for idx, s in enumerate(sources, 1):
        add_body_paragraph(doc, s, bold_prefix=f"{idx}. ")

    out_file = "/Users/mac/Desktop/projects/LandAgencyMvc/Звіт_ЛР3_БД_LINQ_Агентство_земельних_ділянок_Сивопляс_ІПЗ-23.docx"
    doc.save(out_file)
    print(f"Документ успішно збережено у {out_file}")

if __name__ == "__main__":
    build_docx_report()
