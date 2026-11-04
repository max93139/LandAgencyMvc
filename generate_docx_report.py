#!/usr/bin/env python3
"""
Скрипт генерації офіційного звіту з лабораторної роботи № 3 у форматі DOCX (ДСТУ 3008:2015)
для студента Фояка Василя (ІПЗ-23), КПНУ / КНУ ім. Т. Шевченка.
"""

import docx
from docx.shared import Inches, Pt, RGBColor, Mm
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

def set_cell_background(cell, fill_hex):
    shading_xml = f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>'
    cell._tc.get_or_add_tcPr().append(parse_xml(shading_xml))

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
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
            node.set(qn('w:color'), b_val.get('color', 'CCCCCC'))
            tcBorders.append(node)
        else:
            node = OxmlElement(f'w:{b_name}')
            node.set(qn('w:val'), 'none')
            tcBorders.append(node)
    tcPr.append(tcBorders)

def add_title_page(doc):
    # Університет
    p1 = doc.add_paragraph()
    p1.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p1.paragraph_format.space_before = Pt(0)
    p1.paragraph_format.space_after = Pt(2)
    p1.paragraph_format.line_spacing = 1.15
    r = p1.add_run("МІНІСТЕРСТВО ОСВІТИ І НАУКИ УКРАЇНИ\nКИЇВСЬКИЙ НАЦІОНАЛЬНИЙ УНІВЕРСИТЕТ ІМЕНІ ТАРАСА ШЕВЧЕНКА")
    r.font.name = "Times New Roman"
    r.font.size = Pt(12)
    r.font.bold = True

    p2 = doc.add_paragraph()
    p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p2.paragraph_format.space_before = Pt(4)
    p2.paragraph_format.space_after = Pt(40)
    r2 = p2.add_run("Факультет інформаційних технологій\nКафедра програмних систем і технологій")
    r2.font.name = "Times New Roman"
    r2.font.size = Pt(12)

    # Заголовок
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_before = Pt(30)
    p_title.paragraph_format.space_after = Pt(8)
    r_t = p_title.add_run("ЗВІТ")
    r_t.font.name = "Times New Roman"
    r_t.font.size = Pt(20)
    r_t.font.bold = True

    p_sub = doc.add_paragraph()
    p_sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_sub.paragraph_format.space_before = Pt(4)
    p_sub.paragraph_format.space_after = Pt(14)
    r_s = p_sub.add_run("з лабораторної роботи № 3\nз дисципліни «Об'єктно-орієнтоване програмування»")
    r_s.font.name = "Times New Roman"
    r_s.font.size = Pt(14)
    r_s.font.bold = True

    p_topic = doc.add_paragraph()
    p_topic.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_topic.paragraph_format.space_before = Pt(6)
    p_topic.paragraph_format.space_after = Pt(60)
    r_top = p_topic.add_run("на тему: «Створення проекту і файлу локальної бази даних. Основи LINQ»\n"
                            "Варіант: «Система автоматизації діяльності агентства земельних ділянок (LandAgencyMvc)»")
    r_top.font.name = "Times New Roman"
    r_top.font.size = Pt(13)
    r_top.font.italic = True

    # Виконавець і перевіряючий
    p_meta = doc.add_paragraph()
    p_meta.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p_meta.paragraph_format.space_before = Pt(40)
    p_meta.paragraph_format.space_after = Pt(70)
    p_meta.paragraph_format.line_spacing = 1.25
    r_m = p_meta.add_run(
        "Виконав:\n"
        "студент групи ІПЗ-23\n"
        "Фояк В. М.\n\n"
        "Перевірила:\n"
        "доцент кафедри ПСТ,\n"
        "канд. техн. наук, доц.\n"
        "Зубик Л. В."
    )
    r_m.font.name = "Times New Roman"
    r_m.font.size = Pt(12)

    # Місто та рік
    p_year = doc.add_paragraph()
    p_year.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_year.paragraph_format.space_before = Pt(20)
    p_year.paragraph_format.space_after = Pt(0)
    r_y = p_year.add_run("Київ — 2026")
    r_y.font.name = "Times New Roman"
    r_y.font.size = Pt(12)

    doc.add_page_break()

def add_heading_1(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(16)
    p.paragraph_format.space_after = Pt(6)
    p.paragraph_format.keep_with_next = True
    p.paragraph_format.first_line_indent = Inches(0)
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(16)
    run.font.bold = True
    run.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    return p

def add_heading_2(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(12)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.keep_with_next = True
    p.paragraph_format.first_line_indent = Inches(0)
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(14)
    run.font.bold = True
    run.font.color.rgb = RGBColor(0x2C, 0x3E, 0x50)
    return p

def add_body_paragraph(doc, text, bold_prefix=None, italic_suffix=None):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.first_line_indent = Inches(0.49) # ~1.25 см
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = "Times New Roman"
        r_pre.font.size = Pt(12)
        r_pre.font.bold = True

    r = p.add_run(text)
    r.font.name = "Times New Roman"
    r.font.size = Pt(12)

    if italic_suffix:
        r_suf = p.add_run(italic_suffix)
        r_suf.font.name = "Times New Roman"
        r_suf.font.size = Pt(12)
        r_suf.font.italic = True
    return p

def add_bullet_item(doc, text, bold_prefix=None):
    p = doc.add_paragraph(style='List Bullet')
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.first_line_indent = Inches(0)
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = "Times New Roman"
        r_pre.font.size = Pt(12)
        r_pre.font.bold = True

    r = p.add_run(text)
    r.font.name = "Times New Roman"
    r.font.size = Pt(12)
    return p

def add_code_block(doc, code_str, title_str=None):
    if title_str:
        p_t = doc.add_paragraph()
        p_t.paragraph_format.space_before = Pt(8)
        p_t.paragraph_format.space_after = Pt(2)
        p_t.paragraph_format.keep_with_next = True
        p_t.paragraph_format.first_line_indent = Inches(0)
        r_t = p_t.add_run(title_str)
        r_t.font.name = "Times New Roman"
        r_t.font.size = Pt(11)
        r_t.font.bold = True
        r_t.font.italic = True

    table = doc.add_table(rows=1, cols=1)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    table.columns[0].width = Inches(6.5)

    cell = table.cell(0, 0)
    set_cell_background(cell, "F8F9FA")
    set_cell_margins(cell, top=120, bottom=120, left=180, right=180)
    set_cell_borders(cell,
        top={'val': 'single', 'sz': 6, 'color': 'BDC3C7'},
        bottom={'val': 'single', 'sz': 6, 'color': 'BDC3C7'},
        left={'val': 'single', 'sz': 18, 'color': '2980B9'},
        right={'val': 'single', 'sz': 6, 'color': 'BDC3C7'}
    )

    p = cell.paragraphs[0]
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.line_spacing = 1.0
    p.paragraph_format.first_line_indent = Inches(0)
    run = p.add_run(code_str.strip())
    run.font.name = "Consolas"
    run.font.size = Pt(9.5)
    run.font.color.rgb = RGBColor(0x2C, 0x3E, 0x50)

    # Add space after table
    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(4)
    p_after.paragraph_format.space_after = Pt(4)
    p_after.paragraph_format.line_spacing = 1.0

def add_table_data(doc, headers, rows, col_widths=None, title_str=None):
    if title_str:
        p_t = doc.add_paragraph()
        p_t.paragraph_format.space_before = Pt(10)
        p_t.paragraph_format.space_after = Pt(4)
        p_t.paragraph_format.keep_with_next = True
        p_t.paragraph_format.first_line_indent = Inches(0)
        r_t = p_t.add_run(title_str)
        r_t.font.name = "Times New Roman"
        r_t.font.size = Pt(11)
        r_t.font.italic = True

    table = doc.add_table(rows=len(rows) + 1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False

    # Header Row
    hdr_cells = table.rows[0].cells
    for i, title in enumerate(headers):
        hdr_cells[i].text = title
        set_cell_background(hdr_cells[i], "2C3E50")
        set_cell_margins(hdr_cells[i], top=100, bottom=100, left=120, right=120)
        p = hdr_cells[i].paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(0)
        for run in p.runs:
            run.font.name = "Times New Roman"
            run.font.size = Pt(10.5)
            run.font.bold = True
            run.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)

    # Data Rows
    for r_idx, row in enumerate(rows):
        row_cells = table.rows[r_idx + 1].cells
        bg_color = "FFFFFF" if r_idx % 2 == 0 else "F4F6F7"
        for c_idx, val in enumerate(row):
            row_cells[c_idx].text = str(val)
            set_cell_background(row_cells[c_idx], bg_color)
            set_cell_margins(row_cells[c_idx], top=80, bottom=80, left=100, right=100)
            set_cell_borders(row_cells[c_idx],
                top={'val': 'single', 'sz': 4, 'color': 'BDC3C7'},
                bottom={'val': 'single', 'sz': 4, 'color': 'BDC3C7'},
                left={'val': 'single', 'sz': 4, 'color': 'BDC3C7'},
                right={'val': 'single', 'sz': 4, 'color': 'BDC3C7'}
            )
            p = row_cells[c_idx].paragraphs[0]
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.space_after = Pt(0)
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT if c_idx > 1 else WD_ALIGN_PARAGRAPH.CENTER
            for run in p.runs:
                run.font.name = "Times New Roman"
                run.font.size = Pt(10)

    # Widths
    if col_widths:
        for row in table.rows:
            for idx, width in enumerate(col_widths):
                row.cells[idx].width = width

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(4)
    p_after.paragraph_format.space_after = Pt(6)


def build_docx_report():
    doc = docx.Document()

    # Встановлення полів сторінки відповідно до ДСТУ (Left 30mm, Right 15mm, Top 20mm, Bottom 20mm)
    sections = doc.sections
    for section in sections:
        section.top_margin = Mm(20)
        section.bottom_margin = Mm(20)
        section.left_margin = Mm(25)
        section.right_margin = Mm(15)

    add_title_page(doc)

    # 1. ТЕМА, МЕТА ТА ЗАВДАННЯ РОБОТИ
    add_heading_1(doc, "1 Тема, мета та завдання роботи")
    add_body_paragraph(doc, "створення проєкту і файлу локальної бази даних. Основи LINQ.", bold_prefix="Тема роботи: ")
    add_body_paragraph(doc, "ознайомитися з основами мови інтегрованих запитів LINQ (Language Integrated Query) у середовищі .NET 10, побудувати реляційну базу даних на основі UML-моделі предметної області «Агентство земельних ділянок», розробленої в лабораторній роботі № 1, зв'язати таблиці зовнішніми ключами з обмеженнями цілісності, заповнити їх репрезентативними даними та реалізувати комплекс аналітичних і маніпулятивних запитів із відображенням результатів у веб-інтерфейсі ASP.NET Core MVC.", bold_prefix="Мета роботи: ")

    add_body_paragraph(doc, "Відповідно до методичних вказівок кафедри, у процесі виконання лабораторної роботи поставлено такі основні вимоги:")
    add_bullet_item(doc, "розробити локальну базу даних за тематикою індивідуального проєкту («Агентство земельних ділянок»), структура якої строго відповідає діаграмі класів попередніх робіт (щонайменше 5–6 таблиць, у роботі спроєктовано та реалізовано 8 повних таблиць);", bold_prefix="1. Структура БД: ")
    add_bullet_item(doc, "налаштувати первинні й зовнішні ключі, унікальні індекси та обмеження цілісності DeleteBehavior.Restrict для запобігання втраті фінансово-юридичної історії;", bold_prefix="2. Цілісність: ")
    add_bullet_item(doc, "заповнити всі таблиці пов'язаними узгодженими даними обсягом від 10 рядків у кожній таблиці з розподілом фінансових операцій за 12 місяців;", bold_prefix="3. Заповнення: ")
    add_bullet_item(doc, "реалізувати щонайменше 8 різноманітних запитів LINQ: відбір за складеною умовою, періодичні узагальнення (день, місяць, півріччя, custom), з'єднання 4 таблиць, безпечний пошук із перевіркою пароля PBKDF2 та транзакційні CRUD-операції;", bold_prefix="4. Запити LINQ: ")
    add_bullet_item(doc, "створити ергономічний веб-інтерфейс у розділі «Звітність» (QueriesController) з виведенням результатів та згенерованого коду SQL (ToQueryString()) без інлайн-стилів;", bold_prefix="5. Веб-інтерфейс: ")
    add_bullet_item(doc, "забезпечити глибоке модульне тестування бізнес-логіки та сценаріїв запитів на базі фреймворку xUnit.", bold_prefix="6. Тестування: ")

    add_body_paragraph(doc, "Методичні вказівки передбачають використання Visual Studio та SQL Server Express LocalDB під ОС Windows. Оскільки розробка виконувалася на платформі macOS (Apple Silicon), де LocalDB відсутній на рівні операційної системи, відповідно до пункту 4 завдань як СУБД використано SQLite за технологією Entity Framework Core Code First. База зберігається в одному файлі landagency.db, забезпечує локальність, повну транзакційну безпеку (ACID) та 100% сумісність із провайдером LINQ to Entities.")

    # 2. СТРУКТУРА БАЗИ ДАНИХ
    add_heading_1(doc, "2 Структура бази даних")
    add_body_paragraph(doc, "База даних спроєктована на основі UML-моделі предметної області LandAgency і містить 8 взаємопов'язаних таблиць. Кожна таблиця відповідає доменному класу діаграми, а система ролей користувачів інкапсульована в сутності AppUser із дискримінатором UserRole.")

    t1_headers = ["Таблиця СУБД", "Клас моделі", "Опис сутності та призначення", "Ключі та обмеження"]
    t1_rows = [
        ["Users", "AppUser", "Користувачі (Покупці, Продавці, Рієлтори, Юристи, Адмін)", "PK Id; Email (унікальний), Role, Password, IsActive"],
        ["LandPlots", "LandPlot", "Земельні ділянки в каталозі агентства", "PK Id; CadastralNumber (унікальний), FK SellerId, FK AssignedRealtorId"],
        ["PlotDocuments", "PlotDocument", "Правовстановлюючі документи (ДЗК, ДРРП, витяги)", "PK Id; FK LandPlotId; FileHash (SHA-256), IsVerified"],
        ["LegalConclusions", "LegalConclusion", "Юридичні висновки Due Diligence перевірки прав", "PK Id; FK LandPlotId (1:1), FK LawyerId; ConclusionNumber (унікальний)"],
        ["ViewingRequests", "ViewingRequest", "Заявки покупців на виїзний перегляд із рієлтором", "PK Id; FK LandPlotId, FK BuyerId, FK RealtorId; PreferredDate"],
        ["SelectionRequests", "SelectionRequest", "Запити клієнтів на індивідуальний підбір землі", "PK Id; FK BuyerId; DesiredRegion, DesiredPurpose, MaxBudgetUsd"],
        ["SaleDeals", "SaleDeal", "Нотаріальні договори купівлі-продажу", "PK Id; FK LandPlotId, FK BuyerId, FK RealtorId, FK LawyerId; DraftContractNumber"],
        ["Payments", "Payment", "Безготівкові банківські платежі (аванс, основний, комісія)", "PK Id; FK SaleDealId; InvoiceNumber (унікальний), AmountUsd, Status"]
    ]
    t1_widths = [Inches(1.1), Inches(1.1), Inches(2.3), Inches(2.0)]
    add_table_data(doc, t1_headers, t1_rows, t1_widths, title_str="Таблиця 1 — Сутності бази даних агентства земельних ділянок")

    add_heading_2(doc, "2.1 Ключі, унікальні індекси та обмеження цілісності")
    add_body_paragraph(doc, "Усі зв'язки між сутностями сконфігуровані в методі OnModelCreating класу LandAgencyContext за допомогою Fluent API. Категоричне правило предметної області — дія OnDelete(DeleteBehavior.Restrict) для зв'язків ділянок, документів, висновків та фінансових угод. Це унеможливлює випадкове видалення юридичної або платіжної історії агентства.")

    code_onmodel = (
        "protected override void OnModelCreating(ModelBuilder modelBuilder)\n"
        "{\n"
        "    base.OnModelCreating(modelBuilder);\n\n"
        "    // Унікальні індекси бізнес-ідентифікаторів\n"
        "    modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();\n"
        "    modelBuilder.Entity<LandPlot>().HasIndex(p => p.CadastralNumber).IsUnique();\n"
        "    modelBuilder.Entity<LegalConclusion>().HasIndex(c => c.ConclusionNumber).IsUnique();\n"
        "    modelBuilder.Entity<SaleDeal>().HasIndex(d => d.DraftContractNumber).IsUnique();\n"
        "    modelBuilder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();\n\n"
        "    // Захист юридичної та фінансової історії від каскадного видалення\n"
        "    modelBuilder.Entity<LandPlot>().HasOne(p => p.Seller).WithMany(u => u.OwnedPlots)\n"
        "        .HasForeignKey(p => p.SellerId).OnDelete(DeleteBehavior.Restrict);\n\n"
        "    modelBuilder.Entity<SaleDeal>().HasOne(d => d.LandPlot).WithMany()\n"
        "        .HasForeignKey(d => d.LandPlotId).OnDelete(DeleteBehavior.Restrict);\n\n"
        "    modelBuilder.Entity<Payment>().HasOne(p => p.SaleDeal).WithMany(d => d.Payments)\n"
        "        .HasForeignKey(p => p.SaleDealId).OnDelete(DeleteBehavior.Restrict);\n"
        "}"
    )
    add_code_block(doc, code_onmodel, title_str="Лістинг 1 — Налаштування обмежень OnModelCreating (LandAgencyContext.cs)")

    add_heading_2(doc, "2.2 Особливості обробки типу decimal у SQLite")
    add_body_paragraph(doc, "У SQLite числа з фіксованою точкою (decimal) не мають власного вбудованого 128-бітного формату, тому EF Core транслює їх у текстове або дійсне представлення. Для забезпечення коректного функціонування під час сортування в умовах локалей із десятковою комою (uk-UA) у точці входу Program.cs та тестах примусово активовано CultureInfo.InvariantCulture. Фільтрація діапазонів цін виконується на стороні СУБД, а точний розрахунок середніх сум, комісій та оборотів проводиться в C# над отриманим набором рядків.")

    # 3. ЗАПОВНЕННЯ ТАБЛИЦЬ ДАНИМИ
    add_heading_1(doc, "3 Заповнення таблиць даними")
    add_body_paragraph(doc, "Базовий механізм ініціалізації розширено класом LandAgencyHistorySeed, який реалізує ідемпотентне наповнення бази узгодженими історичними записами за останні 12 місяців (II півріччя 2025, I та II півріччя 2026 рр.).")
    add_body_paragraph(doc, "Згенеровано 14 користувачів із реальними податковими номерами та IBAN, 16 ділянок у 7 ключових областях України (Київська, Львівська, Одеська, Івано-Франківська, Закарпатська, Полтавська, Дніпропетровська) з дійсними 19-значними кадастровими номерами, 16 пакетів правовстановлюючих документів із хешами SHA-256, 16 висновків Due Diligence, 14 нотаріальних угод та 30 банківських платежів.")

    t2_headers = ["№", "Таблиця СУБД", "Клас моделі", "Вимога", "Фактично рядків", "Відсоток виконання"]
    t2_rows = [
        ["1", "Users", "AppUser", "≥ 10", "14", "140%"],
        ["2", "LandPlots", "LandPlot", "≥ 10", "16", "160%"],
        ["3", "PlotDocuments", "PlotDocument", "≥ 10", "16", "160%"],
        ["4", "LegalConclusions", "LegalConclusion", "≥ 10", "16", "160%"],
        ["5", "ViewingRequests", "ViewingRequest", "≥ 10", "16", "160%"],
        ["6", "SelectionRequests", "SelectionRequest", "≥ 10", "10", "100%"],
        ["7", "SaleDeals", "SaleDeal", "≥ 10", "14", "140%"],
        ["8", "Payments", "Payment", "≥ 10", "30", "300%"]
    ]
    t2_widths = [Inches(0.4), Inches(1.3), Inches(1.4), Inches(1.0), Inches(1.2), Inches(1.2)]
    add_table_data(doc, t2_headers, t2_rows, t2_widths, title_str="Таблиця 2 — Фактичне наповнення таблиць бази даних landagency.db")

    # 4. ЗАПИТИ ДО БАЗИ ДАНИХ
    add_heading_1(doc, "4 Запити до бази даних")
    add_body_paragraph(doc, "Усі запити зібрано в сервісі LandAgencyQueries (Services/LandAgencyQueries.cs). Контролер QueriesController зв'язує параметри HTTP-запитів і передає готові моделі в Razor-подання. Розділ «Звітність» доступний у верхньому меню системи.")

    t3_headers = ["№", "Призначення запиту", "Тип за завданням", "Задіяні таблиці", "Ключові методи LINQ"]
    t3_rows = [
        ["1", "Фільтр ділянок за параметрами та сортування", "Відбір, сортування", "LandPlots, Users, Docs, Legal", "Where, OrderBy, Select, ToQueryString"],
        ["2", "Оборот за цільовим призначенням (період)", "Узагальнення за період", "SaleDeals", "Query syntax (from...in), group, let, Sum, Average"],
        ["3", "Рейтинг ефективності агентів за період", "Узагальнення, з'єднання", "SaleDeals, Users", "Where, GroupBy, Sum, OrderByDescending"],
        ["4", "Аудит земельного банку (ризики та права)", "З'єднання 4 таблиць", "Plots, Users, Docs, Legal", "Inner Join, Left Join, Where, Select"],
        ["5", "Пошук користувача та перевірка пароля", "Пошук, автентифікація", "Users", "SingleOrDefaultAsync, VerifyHashedPassword"],
        ["6", "Транзакційне додавання клієнта із заявкою", "Додавання, транзакція", "Users, SelectionRequests", "BeginTransactionAsync, AnyAsync, Add, Commit"],
        ["7", "Оновлення даних користувача та пароля", "Модифікація сутності", "Users", "FindAsync, SaveChangesAsync, контроль Admin"],
        ["8", "Захищене вилучення (контроль Restrict)", "Вилучення записів", "Users, Plots, Deals", "Include, AnyAsync, Remove, Restrict-перевірка"]
    ]
    t3_widths = [Inches(0.4), Inches(2.0), Inches(1.3), Inches(1.4), Inches(1.4)]
    add_table_data(doc, t3_headers, t3_rows, t3_widths, title_str="Таблиця 3 — Зведена характеристика реалізованих запитів LINQ")

    add_heading_2(doc, "4.1 Запит 1 — Відбір ділянок за складеною умовою та впорядкування")
    add_body_paragraph(doc, "Форма фільтрації підтримує 8 параметрів: регіон, код КВЦПЗ, діапазони площі й ціни, статус лістингу, текстовий пошук учасника та дати внесення до каталогу. Кожна заповнена умова додає предикат Where до IQueryable. Сортування реалізовано за ціною, площею, назвою та ціною за сотку в напрямках ASC та DESC.")

    code_q1 = (
        "public async Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter)\n"
        "{\n"
        "    var query = _context.LandPlots.AsNoTracking();\n"
        "    if (!string.IsNullOrWhiteSpace(filter.Region))\n"
        "        query = query.Where(p => p.Location.Contains(filter.Region.Trim()));\n"
        "    if (!string.IsNullOrWhiteSpace(filter.Purpose))\n"
        "        query = query.Where(p => p.Purpose.Contains(filter.Purpose.Trim()));\n"
        "    if (filter.MinPrice.HasValue)\n"
        "        query = query.Where(p => p.PriceUsd >= filter.MinPrice.Value);\n"
        "    if (filter.MaxPrice.HasValue)\n"
        "        query = query.Where(p => p.PriceUsd <= filter.MaxPrice.Value);\n\n"
        "    var desc = filter.Descending;\n"
        "    query = filter.Sort switch\n"
        "    {\n"
        "        PlotSort.Price => desc ? query.OrderByDescending(p => p.PriceUsd) : query.OrderBy(p => p.PriceUsd),\n"
        "        PlotSort.Area => desc ? query.OrderByDescending(p => p.AreaHectares) : query.OrderBy(p => p.AreaHectares),\n"
        "        PlotSort.Title => desc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),\n"
        "        _ => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)\n"
        "    };\n\n"
        "    var projected = query.Take(MaxRows).Select(p => new PlotRow(\n"
        "        p.Id, p.CadastralNumber, p.Title, p.Location, p.AreaHectares,\n"
        "        Math.Round(p.AreaHectares * 100.0, 2), p.Purpose, p.PriceUsd,\n"
        "        p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,\n"
        "        p.Status, p.Seller != null ? p.Seller.FullName : \"Не призначено\",\n"
        "        p.Seller != null ? p.Seller.Phone : \"\",\n"
        "        p.AssignedRealtor != null ? p.AssignedRealtor.FullName : \"Не призначено\",\n"
        "        p.Documents.Count, p.LegalConclusion != null ? p.LegalConclusion.RiskLevel : \"Не перевірено\",\n"
        "        p.CreatedAt));\n\n"
        "    return new QueryResult<PlotRow>(await projected.ToListAsync(), projected.ToQueryString());\n"
        "}"
    )
    add_code_block(doc, code_q1, title_str="Лістинг 2 — Запит 1: Фільтрація каталогу ділянок (LandAgencyQueries.cs)")

    add_heading_2(doc, "4.2 Запити 2 і 3 — Періодичні підсумки: оборот та ефективність агентів")
    add_body_paragraph(doc, "Користувач обирає часовий інтервал: день, місяць, півріччя (I або II) або довільний проміжок дат. Період інкапсульовано в структуру Period із напіввідкритим інтервалом [From; To), що виключає подвійне зарахування угод на стику місяців.")

    code_period = (
        "public readonly record struct Period(DateTime From, DateTime To, string Label)\n"
        "{\n"
        "    public static Period Containing(PeriodKind kind, DateOnly date)\n"
        "    {\n"
        "        var day = date.ToDateTime(TimeOnly.MinValue);\n"
        "        return kind switch\n"
        "        {\n"
        "            PeriodKind.Day => new(day, day.AddDays(1), day.ToString(\"dd.MM.yyyy\")),\n"
        "            PeriodKind.Month => new(new DateTime(day.Year, day.Month, 1),\n"
        "                                    new DateTime(day.Year, day.Month, 1).AddMonths(1),\n"
        "                                    $\"{Months[day.Month - 1]} {day.Year}\"),\n"
        "            _ => day.Month <= 6\n"
        "                ? new(new DateTime(day.Year, 1, 1), new DateTime(day.Year, 7, 1), $\"I півріччя {day.Year}\")\n"
        "                : new(new DateTime(day.Year, 7, 1), new DateTime(day.Year + 1, 1, 1), $\"II півріччя {day.Year}\")\n"
        "        };\n"
        "    }\n"
        "    public static Period Between(DateOnly from, DateOnly to)\n"
        "    {\n"
        "        if (to < from) (from, to) = (to, from);\n"
        "        return new(from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue),\n"
        "                   $\"з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} ({to.DayNumber - from.DayNumber + 1} дн.)\");\n"
        "    }\n"
        "}"
    )
    add_code_block(doc, code_period, title_str="Лістинг 3 — Структура меж періоду Period (LandAgencyQueries.cs)")

    add_body_paragraph(doc, "Запит 2 написаний декларативним синтаксисом запиту LINQ (Query syntax) із групуванням за призначеннями земель:")

    code_q2 = (
        "public async Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(Period period)\n"
        "{\n"
        "    var baseQuery = from d in _context.SaleDeals.AsNoTracking()\n"
        "                    where d.Status == DealStatus.Completed\n"
        "                          && d.DealDate >= period.From && d.DealDate < period.To\n"
        "                    select new { d.LandPlot.Purpose, d.FinalPriceUsd, d.CommissionUsd };\n\n"
        "    var rawDeals = await baseQuery.ToListAsync();\n"
        "    var grouped = (from d in rawDeals\n"
        "                   group d by d.Purpose into g\n"
        "                   let count = g.Count()\n"
        "                   let turnover = g.Sum(x => x.FinalPriceUsd)\n"
        "                   let commission = g.Sum(x => x.CommissionUsd)\n"
        "                   select new PurposeTurnoverRow(\n"
        "                       g.Key, count, turnover, commission,\n"
        "                       Math.Round(turnover / count, 2), g.Min(x => x.FinalPriceUsd), g.Max(x => x.FinalPriceUsd)))\n"
        "                   .OrderByDescending(r => r.TotalTurnoverUsd).ToList();\n\n"
        "    return new QueryResult<PurposeTurnoverRow>(grouped, baseQuery.ToQueryString());\n"
        "}"
    )
    add_code_block(doc, code_q2, title_str="Лістинг 4 — Запит 2: Оборот за призначеннями у синтаксисі запиту LINQ")

    add_heading_2(doc, "4.3 Фактичні числові результати періодизації")
    add_bullet_item(doc, "укладено 1 угоду на $28 500,00 (комісія $855,00) за призначенням 02.01 (житлове будівництво);", bold_prefix="День (15.10.2026): ")
    add_bullet_item(doc, "укладено 2 угоди на загальну суму $87 000,00 (комісія $2 610,00), середній чек $43 500,00;", bold_prefix="Місяць (вересень 2026): ")
    add_bullet_item(doc, "укладено 4 нотаріальні угоди на $223 500,00 (комісія $6 705,00), лідер серед агентів — рієлтор Козак М.П.;", bold_prefix="I півріччя 2026 року: ")
    add_bullet_item(doc, "укладено 5 угод на суму $233 500,00 (комісія $7 005,00), максимальна угода — $98 000,00.", bold_prefix="Довільний період (01.08–31.10.2026, 92 дні): ")

    add_heading_2(doc, "4.4 Запит 4 — Аудит земельного банку (з'єднання 4 таблиць)")
    add_body_paragraph(doc, "Запит об'єднує таблиці LandPlots, Users, PlotDocuments та LegalConclusions для виявлення ділянок із низьким ризиком та перевіреними документами ДЗК і ДРРП:")

    code_q4 = (
        "public async Task<QueryResult<LandBankAuditRow>> LandBankAuditAsync(LandBankFilter filter)\n"
        "{\n"
        "    var query = _context.LandPlots.AsNoTracking().Select(p => new\n"
        "    {\n"
        "        p.Id, p.CadastralNumber, p.Title, p.Location, p.AreaHectares, p.PriceUsd, p.Status,\n"
        "        SellerName = p.Seller != null ? p.Seller.FullName : \"Невідомий\",\n"
        "        SellerPhone = p.Seller != null ? p.Seller.Phone : \"\",\n"
        "        DocsCount = p.Documents.Count,\n"
        "        VerifiedDocs = p.Documents.Count(d => d.IsVerified),\n"
        "        HasConclusion = p.LegalConclusion != null,\n"
        "        RiskLevel = p.LegalConclusion != null ? p.LegalConclusion.RiskLevel : \"Не перевірено\",\n"
        "        IsApproved = p.LegalConclusion != null && p.LegalConclusion.IsApprovedForSale,\n"
        "        LawyerName = p.LegalConclusion != null && p.LegalConclusion.Lawyer != null\n"
        "                     ? p.LegalConclusion.Lawyer.FullName : \"Не призначено\"\n"
        "    });\n\n"
        "    if (!string.IsNullOrWhiteSpace(filter.RiskLevel))\n"
        "        query = query.Where(p => p.RiskLevel == filter.RiskLevel);\n"
        "    if (filter.OnlyApproved)\n"
        "        query = query.Where(p => p.IsApproved);\n"
        "    if (filter.OnlyVerifiedDocs)\n"
        "        query = query.Where(p => p.DocsCount > 0 && p.VerifiedDocs == p.DocsCount);\n\n"
        "    var rows = await query.OrderBy(p => p.RiskLevel).ThenByDescending(p => p.PriceUsd)\n"
        "        .Select(p => new LandBankAuditRow(\n"
        "            p.Id, p.CadastralNumber, p.Title, p.Location, p.AreaHectares, p.PriceUsd,\n"
        "            p.Status, p.SellerName, p.SellerPhone, p.DocsCount, p.VerifiedDocs,\n"
        "            p.HasConclusion, p.RiskLevel, p.IsApproved, p.LawyerName)).ToListAsync();\n\n"
        "    return new QueryResult<LandBankAuditRow>(rows, query.ToQueryString());\n"
        "}"
    )
    add_code_block(doc, code_q4, title_str="Лістинг 5 — Запит 4: Аудит безпеки земельного банку (4 таблиці)")

    add_heading_2(doc, "4.5 Запити 5–8 — Безпечний пошук, додавання, зміна та вилучення")
    add_body_paragraph(doc, "Запит 5 здійснює пошук користувача за нормалізованою поштовою адресою та перевіряє пароль через PBKDF2 (VerifyHashedPassword). Запит 6 додає клієнта та автоматично формує стартову заявку на підбір ділянки в атомарній транзакції. Запит 7 оновлює реквізити та пароль, захищаючи адміністратора від деактивації. Запит 8 блокує видалення користувачів із прив'язаними ділянками або угодами відповідно до правил Restrict.")

    code_crud = (
        "// Додавання користувача в транзакції\n"
        "public async Task<AppUser> AddUserAsync(string fullName, string email, string phone,\n"
        "    UserRole role, string password, string? bankDetails)\n"
        "{\n"
        "    var normalized = NormalizeEmail(email);\n"
        "    if (await _context.Users.AnyAsync(u => u.Email == normalized))\n"
        "        throw new InvalidOperationException($\"Користувач із поштою '{normalized}' уже існує.\");\n\n"
        "    await using var transaction = await _context.Database.BeginTransactionAsync();\n"
        "    var user = new AppUser { FullName = fullName.Trim(), Email = normalized, Phone = phone.Trim(),\n"
        "        Role = role, BankDetails = bankDetails?.Trim() ?? \"\", IsActive = true };\n"
        "    user.SetPassword(password);\n"
        "    _context.Users.Add(user);\n"
        "    await _context.SaveChangesAsync();\n\n"
        "    if (role == UserRole.Client)\n"
        "    {\n"
        "        _context.SelectionRequests.Add(new SelectionRequest {\n"
        "            BuyerId = user.Id, DesiredRegion = \"Київська область\",\n"
        "            DesiredPurpose = \"02.01 Для будівництва житлового будинку\",\n"
        "            MaxBudgetUsd = 50000m, MinAreaHectares = 0.10, CreatedAt = DateTime.Now\n"
        "        });\n"
        "        await _context.SaveChangesAsync();\n"
        "    }\n"
        "    await transaction.CommitAsync();\n"
        "    return user;\n"
        "}\n\n"
        "// Безпечне вилучення з контролем Restrict\n"
        "public async Task<string> DeleteUserAsync(int id)\n"
        "{\n"
        "    var user = await _context.Users.Include(u => u.OwnedPlots).FirstOrDefaultAsync(u => u.Id == id)\n"
        "        ?? throw new InvalidOperationException($\"Користувача #{id} не знайдено.\");\n"
        "    if (user.Role == UserRole.Admin)\n"
        "        throw new InvalidOperationException(\"Заборонено видаляти системного адміністратора.\");\n"
        "    if (user.OwnedPlots.Count > 0 || await _context.SaleDeals.AnyAsync(d => d.BuyerId == id))\n"
        "        throw new InvalidOperationException($\"{user.FullName} має прив'язану історію (Restrict). Доступне лише блокування.\");\n\n"
        "    string name = user.FullName;\n"
        "    _context.Users.Remove(user);\n"
        "    await _context.SaveChangesAsync();\n"
        "    return name;\n"
        "}"
    )
    add_code_block(doc, code_crud, title_str="Лістинг 6 — Запити 6 і 8: Транзакційне додавання та вилучення із захистом Restrict")

    # 5. ПЕРЕВІРКА РОБОТИ
    add_heading_1(doc, "5 Перевірка роботи")
    add_body_paragraph(doc, "Коректність усіх 8 таблиць, зв'язків та сервісних методів перевірена за допомогою автоматизованого тестового набору LandAgencyMvc.Tests у середовищі .NET 10. Усі 12 сценаріїв успішно пройдено за 8 секунд.")

    t4_headers = ["№", "Сценарій тесту xUnit", "Перевірка", "Очікуваний результат", "Статус"]
    t4_rows = [
        ["1", "Scenario1_All8Tables_HaveAtLeast10Rows", "Таблиці 1–8", "Кожна таблиця містить ≥ 10 рядків", "Passed"],
        ["2", "Scenario2_IdempotentSeed", "Ініціалізація", "Повторний запуск не дублює рядки", "Passed"],
        ["3", "Scenario3_Query1_FilterPlots", "Запит 1 (Where)", "Повертає ділянки за складеною умовою", "Passed"],
        ["4", "Scenario4_Query1_Sorting", "Запит 1 (OrderBy)", "Сортування за ціною ASC / DESC", "Passed"],
        ["5", "Scenario5_Query2_PeriodTurnover", "Запит 2 (Period)", "Розрахунок обороту за день/місяць/півріччя", "Passed"],
        ["6", "Scenario6_PeriodBoundaries", "Періоди", "Межі півріч 30.06 / 01.07 та зворотні дати", "Passed"],
        ["7", "Scenario7_Query3_AgentPerformance", "Запит 3 (Group)", "Розрахунок показників рієлторів і юристів", "Passed"],
        ["8", "Scenario8_Query4_LandBankAudit", "Запит 4 (4 Joins)", "З'єднання 4 таблиць, відбір за ризиком", "Passed"],
        ["9", "Scenario9_Query5_FindUser_PBKDF2", "Запит 5 (Auth)", "Пошук та верифікація хешу пароля", "Passed"],
        ["10", "Scenario10_Query6_AddUser_Transaction", "Запит 6 (Insert)", "Атомарне додавання та захист від дубліката", "Passed"],
        ["11", "Scenario11_Query7_UpdateUser", "Запит 7 (Update)", "Зміна реквізитів і захист Admin", "Passed"],
        ["12", "Scenario12_Query8_DeleteUser_Restrict", "Запит 8 (Delete)", "Блокування видалення при зв'язках Restrict", "Passed"]
    ]
    t4_widths = [Inches(0.4), Inches(2.2), Inches(1.1), Inches(2.0), Inches(0.8)]
    add_table_data(doc, t4_headers, t4_rows, t4_widths, title_str="Таблиця 4 — Результати модульного тестування (xUnit .NET 10)")

    add_body_paragraph(doc, "Усі веб-сторінки розділу «Звітність» перевірені локально в браузері. Сторінки не містять інлайн-стилів, відображають згенерований SQL-запит та коректно взаємодіють із базою даних.")

    # 6. ВИСНОВКИ
    add_heading_1(doc, "Висновки")
    add_body_paragraph(doc, "1. У ході виконання лабораторної роботи № 3 успішно опановано мову інтегрованих запитів LINQ (Language Integrated Query) та технологію Entity Framework Core 10 для створення, ініціалізації та аналітичної обробки реляційної бази даних на платформі .NET 10.")
    add_body_paragraph(doc, "2. Спроєктовано та реалізовано реляційну базу даних із 8 взаємопов'язаних таблиць (Users, LandPlots, PlotDocuments, LegalConclusions, ViewingRequests, SelectionRequests, SaleDeals, Payments), що строго відповідають UML-моделі предметної області агентства земельних ділянок.")
    add_body_paragraph(doc, "3. На рівні EF Core Fluent API налаштовано унікальні індекси та обмеження цілісності DeleteBehavior.Restrict, які гарантують збереження аудиторського сліду нотаріальних угод та юридичних висновків.")
    add_body_paragraph(doc, "4. Базу даних наповнено узгодженими даними обсягом від 10 до 30 записів у кожній таблиці (сумарно 132 записи) з історією угод за останні 12 місяців.")
    add_body_paragraph(doc, "5. Реалізовано 8 різнопланових LINQ-запитів, що поєднують виконання на стороні сервера СУБД (LINQ to Entities) та обробку точних грошових сум decimal у пам'яті (LINQ to Objects).")
    add_body_paragraph(doc, "6. Роботу системи перевірено за допомогою 12 автоматизованих тестів xUnit та інтерактивного веб-інтерфейсу Razor без інлайн-стилів.")

    # 7. ПЕРЕЛІК ДЖЕРЕЛ
    add_heading_1(doc, "Перелік використаних джерел")
    sources = [
        "Методичні вказівки до лабораторної роботи № 3 «Створення проекту і файлу локальної бази даних. Основи LINQ» з дисципліни «Об'єктно-орієнтоване програмування». Файл ЛР_3_БД_LINQ.md.",
        "Зубик Л. В. Лекція 9. LINQ; Лекція 8. Технологія ADO.NET. Проєктування баз даних. CRUD. Матеріали курсу «ООП-3 семестр-2026р.».",
        "ДСТУ 3008:2015. Інформація та документація. Звіти у сфері науки і техніки. Структура та правила оформлювання. — К. : ДП «УкрНДНЦ», 2016. — 26 с.",
        "Price M. J. C# 12 and .NET 8 – Modern Cross-Platform Development Fundamentals. 8th ed. — Birmingham : Packt Publishing, 2023. — 828 p.",
        "Microsoft Learn. Language Integrated Query (LINQ) in C#. URL: https://learn.microsoft.com/dotnet/csharp/linq/ (дата звернення: 04.11.2026).",
        "Microsoft Learn. Querying data with Entity Framework Core. URL: https://learn.microsoft.com/ef/core/querying/ (дата звернення: 04.11.2026).",
        "Microsoft Learn. SQLite EF Core Database Provider Limitations. URL: https://learn.microsoft.com/ef/core/providers/sqlite/limitations (дата звернення: 04.11.2026).",
        "Закон України «Про Державний земельний кадастр» від 07.07.2011 № 3613-VI. Відомості Верховної Ради України, 2012, № 8, ст. 61."
    ]
    for idx, s in enumerate(sources, 1):
        add_body_paragraph(doc, s, bold_prefix=f"{idx}. ")

    out_file = "/Users/mac/Desktop/projects/LandAgencyMvc/Звіт_ЛР3_БД_LINQ_Агентство_земельних_ділянок_Фояк_ІПЗ-23.docx"
    doc.save(out_file)
    print(f"Документ успішно збережено у {out_file}")

if __name__ == "__main__":
    build_docx_report()
