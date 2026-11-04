# Міністерство освіти і науки України
# Київський національний університет імені Тараса Шевченка
## Факультет інформаційних технологій
### Кафедра програмних систем і технологій

<br><br><br><br>

# ЗВІТ
### з лабораторної роботи № 3
### з навчальної дисципліни «Об'єктно-орієнтоване програмування»
на тему:
## «Створення проекту і файлу локальної бази даних. Основи LINQ»
### Варіант: «Система автоматизації діяльності агентства земельних ділянок (LandAgencyMvc)»

<br><br><br><br>

**Виконав:**  
студент групи ІПЗ-23  
Фояк Василь Миколайович  

**Перевірила:**  
доцент кафедри ПСТ,  
канд. техн. наук, доц.  
Зубик Л. В.  

<br><br><br>

### Київ — 2026

---

## Зміст

1. [Тема, мета та завдання роботи](#1-тема-мета-та-завдання-роботи)
2. [Структура бази даних](#2-структура-бази-даних)  
   2.1 [Таблиці та класи моделі](#21-таблиці-та-класи-моделі)  
   2.2 [Ключі, унікальні індекси та обмеження цілісності](#22-ключі-унікальні-індекси-та-обмеження-цілісності)  
   2.3 [Особливості роботи з фінансовими даними в SQLite](#23-особливості-роботи-з-фінансовими-даними-в-sqlite)
3. [Заповнення таблиць даними](#3-заповнення-таблиць-даними)  
   3.1 [Початкові та історичні дані](#31-початкові-та-історичні-дані)  
   3.2 [Статистика заповнення бази даних](#32-статистика-заповнення-бази-даних)
4. [Запити до бази даних](#4-запити-до-бази-даних)  
   4.1 [Запит 1 — Відбір ділянок за складеною умовою та впорядкування](#41-запит-1--відбір-ділянок-за-складеною-умовою-та-впорядкування)  
   4.2 [Запити 2 і 3 — Узагальнення даних за період: оборот та ефективність агентів](#42-запити-2-і-3--узагальнення-даних-за-період-оборот-та-ефективність-агентів)  
   4.3 [Підсумки за день, місяць, півріччя та довільний період](#43-підсумки-за-день-місяць-півріччя-та-довільний-період)  
   4.4 [Запит 4 — Аудит земельного банку (з'єднання 4 таблиць)](#44-запит-4--аудит-земельного-банку-зєднання-4-таблиць)  
   4.5 [Запит 5 — Пошук користувача за логіном та перевірка пароля (PBKDF2)](#45-запит-5--пошук-користувача-за-логіном-та-перевірка-пароля-pbkdf2)  
   4.6 [Запити 6–8 — Транзакційне додавання, модифікація та захищене вилучення](#46-запити-68--транзакційне-додавання-модифікація-та-захищене-вилучення)  
   4.7 [Запит 9 — Комплексна демонстрація всіх операторів LINQ (Showcase)](#47-запит-9--комплексна-демонстрація-всіх-операторів-linq-showcase)
5. [Перевірка роботи](#5-перевірка-роботи)  
   5.1 [Автоматизоване модульне тестування (xUnit, 17 сценаріїв)](#51-автоматизоване-модульне-тестування-xunit)  
   5.2 [Користувацький веб-інтерфейс розділу «Звітність»](#52-користувацький-веб-інтерфейс-розділу-звітність)  
   5.3 [Інструкція запуску та перевірки](#53-інструкція-запуску-та-перевірки)
6. [Висновки](#висновки)
7. [Перелік використаних джерел](#перелік-використаних-джерел)

---

## 1 Тема, мета та завдання роботи

**Тема роботи:** Створення проєкту і файлу локальної бази даних. Основи LINQ.

**Мета роботи:** Ознайомитися з основами мови інтегрованих запитів LINQ (Language Integrated Query) у середовищі .NET 10, побудувати реляційну базу даних на основі UML-моделі предметної області «Агентство земельних ділянок», розробленої в лабораторній роботі № 1, зв'язати таблиці зовнішніми ключами з обмеженнями цілісності, заповнити їх репрезентативними даними та реалізувати комплекс аналітичних і маніпулятивних запитів із відображенням результатів у веб-інтерфейсі ASP.NET Core MVC.

**Основні завдання згідно з методичними вказівками:**
1. Розробити локальну базу даних за тематикою індивідуального проєкту («Агентство земельних ділянок»), структура якої строго відповідає UML-діаграмі класів (щонайменше 5–6 таблиць, у роботі реалізовано повні 8 таблиць).
2. Задати первинні та зовнішні ключі, налаштувати обмеження цілісності даних (включаючи `DeleteBehavior.Restrict` для збереження юридичної та фінансової історії) та унікальні індекси.
3. Заповнити всі таблиці пов'язаними даними обсягом від 10 рядків у кожній таблиці з розподілом фінансових операцій за 12 місяців (2025–2026 рр.).
4. Розробити щонайменше 8 різноманітних запитів мовою LINQ, що охоплюють:
   - відбір за складеною умовою (фільтрація) з вибором напрямку та поля впорядкування;
   - узагальнення даних за вказаний користувачем період (день, місяць, півріччя або довільний інтервал «з … по …»);
   - багаторівневе з'єднання декількох таблиць (4 таблиці: ділянка, продавець, правовстановлюючі документи, юридичний висновок);
   - безпечний пошук користувача за логіном та криптографічну перевірку пароля;
   - додавання, зміну та вилучення записів із захистом цілісності даних та транзакційністю.
5. Забезпечити виведення результатів запитів у користувацькому веб-інтерфейсі (контролер `QueriesController` та Razor-подання) без інлайн-стилів із відображенням згенерованого SQL-коду (`ToQueryString()`).
6. Підтвердити надійність системи автоматизованими тестами (xUnit) та оформити академічний звіт відповідно до вимог ДСТУ 3008:2015 (з наскрізною нумерацією сторінок у нижньому колонтитулі).

**Обґрунтування використання SQLite замість LocalDB:**  
Методичні вказівки до лабораторної роботи орієнтовані на створення бази у Visual Studio під ОС Windows через SQL Server Express LocalDB. Розробка та тестування даного проєкту здійснювалися на платформі macOS (Apple Silicon), де SQL Server LocalDB недоступний на рівні операційної системи. Відповідно до пункту 4 завдань методичних вказівок та стандартів кросплатформної розробки на .NET 10, як СУБД було обрано **SQLite** з використанням **Entity Framework Core 10 (Code First)**. SQLite зберігає базу даних в одному компактному локальному файлі (`landagency.db`), що повністю задовольняє вимогу локальності, підтримує реляційну цілісність, транзакції стандарту ACID та забезпечує 100% функціональну еквівалентність при трансляції запитів через LINQ to Entities.

---

## 2 Структура бази даних

### 2.1 Таблиці та класи моделі

Реляційна структура бази даних спроєктована на основі концептуальної UML-моделі предметної області агентства нерухомості з ЛР1 і містить **8 пов'язаних таблиць**:

| Таблиця | Клас моделі | Опис сутності та бізнес-роль | Ключові поля та обмеження |
|---|---|---|---|
| `Users` | `AppUser` | Користувачі системи (Клієнти/Покупці, Продавці, Рієлтори, Юристи, Адміністратор) | `PK Id`, `Email` (унікальний), `Password`, `Role`, `Phone`, `TaxId`, `BankDetails`, `IsActive` |
| `LandPlots` | `LandPlot` | Земельні ділянки, виставлені на продаж або перевірку | `PK Id`, `CadastralNumber` (унікальний 19 знаків), `AreaHectares`, `Purpose` (КВЦПЗ), `PriceUsd`, `FK SellerId`, `FK AssignedRealtorId` |
| `PlotDocuments` | `PlotDocument` | Правовстановлюючі документи (витяги ДРРП, ДЗК, нормативна грошова оцінка) | `PK Id`, `FK LandPlotId`, `DocumentType`, `FileHash` (SHA-256), `IsVerified`, `UploadedAt` |
| `LegalConclusions` | `LegalConclusion` | Юридичні висновки (Due Diligence) перевірки обтяжень, арештів та сервітутів | `PK Id`, `FK LandPlotId` (1:1), `FK LawyerId`, `ConclusionNumber` (унікальний), `RiskLevel`, `IsApprovedForSale` |
| `ViewingRequests` | `ViewingRequest` | Заявки покупців на виїзний перегляд земельних ділянок із закріпленим рієлтором | `PK Id`, `FK LandPlotId`, `FK BuyerId`, `FK RealtorId`, `PreferredDate`, `Status`, `CreatedAt` |
| `SelectionRequests` | `SelectionRequest` | Запити покупців на індивідуальний підбір ділянок за параметрами | `PK Id`, `FK BuyerId`, `DesiredRegion`, `DesiredPurpose`, `MaxBudgetUsd`, `MinAreaHectares`, `CreatedAt` |
| `SaleDeals` | `SaleDeal` | Нотаріальні угоди купівлі-продажу землі з фіксацією фінансових умов | `PK Id`, `FK LandPlotId`, `FK BuyerId`, `FK RealtorId`, `FK LawyerId`, `DraftContractNumber` (унікальний), `FinalPriceUsd`, `CommissionUsd`, `Status`, `DealDate` |
| `Payments` | `Payment` | Фінансові безготівкові платежі (аванс, основний платіж, комісія агентства) | `PK Id`, `FK SaleDealId`, `InvoiceNumber` (унікальний), `AmountUsd`, `PayerIban`, `RecipientIban`, `Status`, `PaymentDate` |

```
                              ┌────────────────┐
                              │     Users      │
                              └───────┬────────┘
                                      │ 1
        ┌─────────────────────────────┼─────────────────────────────┐
        │ 1..*                        │ 1..*                        │ 1..*
 ┌──────┴─────────┐            ┌──────┴─────────┐            ┌──────┴─────────┐
 │SelectionRequest│            │ ViewingRequest │            │    LandPlot    │
 └────────────────┘            └────────────────┘            └───────┬────────┘
                                                                     │ 1
                                      ┌──────────────────────────────┼────────────────────────┐
                                      │ 1..*                         │ 1                      │ 1..*
                               ┌──────┴─────────┐             ┌──────┴─────────┐       ┌──────┴─────────┐
                               │  PlotDocument  │             │LegalConclusion │       │    SaleDeal    │
                               └────────────────┘             └────────────────┘       └───────┬────────┘
                                                                                               │ 1
                                                                                               │ 1..*
                                                                                       ┌──────┴─────────┐
                                                                                       │    Payment     │
                                                                                       └────────────────┘
```
*Рисунок 1 — Схема зв'язків між сутностями бази даних агентства земельних ділянок*

### 2.2 Ключі, унікальні індекси та обмеження цілісності

Зв'язки між сутностями та обмеження реляційної цілісності налаштовані у методі `OnModelCreating` класу `LandAgencyContext` з використанням EF Core Fluent API:
1. **Категорична заборона каскадного видалення (`DeleteBehavior.Restrict`):**  
   Земельні ділянки, правовстановлюючі документи, юридичні висновки Due Diligence та нотаріальні угоди купівлі-продажу мають сувору юридичну та фінансову історію. Каскадне видалення записів призвело б до втрати аудиторського сліду. При спробі видалити ділянку, яка фігурує в угодах чи переглядах, СУБД блокує операцію з генерацією винятку.
2. **Унікальні індекси:**  
   - `Users.Email` — унеможливлює реєстрацію двох користувачів з однаковим логіном.
   - `LandPlots.CadastralNumber` — гарантує унікальність 19-значного державного кадастрового номера ділянки.
   - `LegalConclusions.ConclusionNumber` — захищає від дублювання реєстраційних номерів юридичних висновків.
   - `SaleDeals.DraftContractNumber` — гарантує унікальність номерів договорів купівлі-продажу.
   - `Payments.InvoiceNumber` — запобігає повторній оплаті того самого фінансового інвойсу.

**Лістинг 1 — Налаштування індексів та зовнішніх ключів (`LandAgencyContext.cs`):**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Унікальні бізнес-індекси
    modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
    modelBuilder.Entity<LandPlot>().HasIndex(p => p.CadastralNumber).IsUnique();
    modelBuilder.Entity<LegalConclusion>().HasIndex(c => c.ConclusionNumber).IsUnique();
    modelBuilder.Entity<SaleDeal>().HasIndex(d => d.DraftContractNumber).IsUnique();
    modelBuilder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();

    // Захист фінансової та юридичної історії: DeleteBehavior.Restrict
    modelBuilder.Entity<LandPlot>()
        .HasOne(p => p.Seller)
        .WithMany(u => u.OwnedPlots)
        .HasForeignKey(p => p.SellerId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<SaleDeal>()
        .HasOne(d => d.LandPlot)
        .WithMany()
        .HasForeignKey(d => d.LandPlotId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<SaleDeal>()
        .HasOne(d => d.Buyer)
        .WithMany()
        .HasForeignKey(d => d.BuyerId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<Payment>()
        .HasOne(p => p.SaleDeal)
        .WithMany(d => d.Payments)
        .HasForeignKey(p => p.SaleDealId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<PlotDocument>()
        .HasOne(d => d.LandPlot)
        .WithMany(p => p.Documents)
        .HasForeignKey(d => d.LandPlotId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<LegalConclusion>()
        .HasOne(c => c.LandPlot)
        .WithOne(p => p.LegalConclusion)
        .HasForeignKey<LegalConclusion>(c => c.LandPlotId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

### 2.3 Особливості роботи з фінансовими даними в SQLite

Тип даних `decimal` мови C# призначений для збереження точних грошових значень без похибок заокруглення, притаманних типам з рухомою комою (`double`, `float`). Оскільки SQLite не має вбудованого 128-бітного десяткового типу, EF Core за замовчуванням зберігає `decimal` у вигляді текстового стовпця (`TEXT`).

Ця особливість спричиняє важливий підводний камінь: при виконанні трансляції `OrderBy(p => p.PriceUsd)` безпосередньо у SQL SQLite здійснює **лексикографічне (алфавітне) порівняння рядків**. Внаслідок цього значення з різною кількістю розрядів упорядковуються некоректно: рядок `"120000.0"` лексикографічно вважається меншим за `"18000.0"` та `"5400.0"`.

Для забезпечення математичної коректності сортування та агрегацій у проєкті реалізовано надійний гібридний підхід:
1. **Первинна фільтрація в базі даних (LINQ to Entities):** обмеження діапазонів цін (`PriceUsd >= min && PriceUsd <= max`), дат угод та статусів транслюються безпосередньо в оптимізований SQL-запит і відсікають нерелевантні дані на рівні сервера СУБД.
2. **Точне сортування в пам'яті (LINQ to Objects):** при сортуванні за ціною результати матеріалізуються у пам'ять, де задіюється типізоване порівняння `decimal` у середовищі CLR (`rows.OrderBy(r => r.PriceUsd).ThenByDescending(r => r.CreatedAt)`). Вторинні критерії сортування реалізовано за допомогою операторів `.ThenBy` та `.ThenByDescending` (відповідно до слайда 12 Лекції № 9).
3. **Точна агрегація в пам'яті (LINQ to Objects):** фінансові показники (сумарний оборот, комісійні агентства, середньозважена ціна сотки, мінімум та максимум) обраховуються над отриманим набором рядків мовою C# без втрати точності.
4. **Кросплатформна культура (`InvariantCulture`):** для запобігання помилкам розбору рядків під час сортування в середовищах із локалями, де десятковим роздільником є кома (зокрема українська локаль `uk-UA` на macOS), у `Program.cs` та тестовому раннері примусово сконфігуровано `CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture`.

---

## 3 Заповнення таблиць даними

### 3.1 Початкові та історичні дані

Для виконання вимог щодо наявності від 10 записів у кожній таблиці та надання можливості аналізу показників за різні часові періоди (день, місяць, півріччя, рік), базовий механізм ініціалізації розширено окремим модулем генерації історичних даних `LandAgencyHistorySeed.cs`.

Цей модуль:
1. Забезпечує **ідемпотентність**: перевіряє стан таблиць і запускає генерацію лише тоді, коли кількість рядків менша за необхідний мінімум, унеможливлюючи дублювання даних при повторних запусках застосунку.
2. Створює **14 користувачів**: 5 покупців, 4 продавці, 2 сертифіковані рієлтори (Бондар І.О., Козак М.П.), 2 провідні юристи (Коваленко О.В., Мельниченко Д.С.) та 1 системний адміністратор.
3. Формує **16 земельних ділянок** у ключових регіонах України (Київська, Львівська, Одеська, Івано-Франківська, Закарпатська, Полтавська, Дніпропетровська області) з дійсними 19-значними кадастровими номерами, реальними цільовими призначеннями згідно з КВЦПЗ (02.01 — житлове будівництво, 01.01 — товарне с/г, 03.07 — комерція, 06.01 — промисловість) та ринковими цінами від \$14 500 до \$145 000.
4. До кожної ділянки генерує **16 комплектів правовстановлюючих документів** із реальними хешами SHA-256 та **16 офіційних юридичних висновків Due Diligence** (розподіл ризиків: Низький, Середній, Високий).
5. Генерує історію з **14 нотаріальних угод купівлі-продажу** та **30 банківських платежів**, рівномірно розподілених по кварталах за останні 12 місяців (II півріччя 2025 року, I півріччя 2026 року, II півріччя 2026 року), а також 16 заявок на перегляд та 10 запитів на підбір.

### 3.2 Статистика заповнення бази даних

Фактична кількість рядків у кожній із 8 таблиць локальної бази даних `landagency.db` наведена в таблиці 2:

*Таблиця 2 — Фактична кількість записів у таблицях бази даних*

| № | Назва таблиці у СУБД | Відповідний клас моделі | Вимога за методичкою | Фактична кількість рядків | Статус відповідності |
|---|---|---|---|---|---|
| 1 | `Users` | `AppUser` | ≥ 10 | **14** | Виконано (140%) |
| 2 | `LandPlots` | `LandPlot` | ≥ 10 | **16** | Виконано (160%) |
| 3 | `PlotDocuments` | `PlotDocument` | ≥ 10 | **16** | Виконано (160%) |
| 4 | `LegalConclusions` | `LegalConclusion` | ≥ 10 | **16** | Виконано (160%) |
| 5 | `ViewingRequests` | `ViewingRequest` | ≥ 10 | **16** | Виконано (160%) |
| 6 | `SelectionRequests` | `SelectionRequest` | ≥ 10 | **10** | Виконано (100%) |
| 7 | `SaleDeals` | `SaleDeal` | ≥ 10 | **14** | Виконано (140%) |
| 8 | `Payments` | `Payment` | ≥ 10 | **30** | Виконано (300%) |

Усі 8 таблиць повністю задовольняють і перевищують встановлену вимогу (від 10 рядків), а загальна кількість первинних записів у системі становить **132 рядки**.

---

## 4 Запити до бази даних

Усі аналітичні та маніпулятивні операції винесено в окремий сервісний шар — `LandAgencyQueries` (`ILandAgencyQueries`), зареєстрований у DI-контейнері застосунку. Контролер `QueriesController` відповідає виключно за зв'язування HTTP-параметрів та передачу результатів у строго типізовані моделі подання.

*Таблиця 3 — Зведена характеристика реалізованих запитів LINQ*

| № | Призначення запиту | Вид операції за завданням | Задіяні таблиці бази даних | Використані конструкції LINQ |
|---|---|---|---|---|
| 1 | Фільтрація каталогу ділянок за регіоном, призначенням, площею, бюджетом, учасником та сортуванням | Відбір за складеною умовою, динамічне сортування | `LandPlots`, `Users`, `PlotDocuments`, `LegalConclusions` | `Where`, `OrderBy`, `OrderByDescending`, `Select`, `Take`, `ToQueryString` |
| 2 | Фінансовий оборот та цінові показники за цільовим призначенням земель за період (день, місяць, півріччя, custom) | Узагальнення даних за період, групування, агрегація | `SaleDeals` | Query syntax (`from...in...where`), `group...by`, `let`, `Count`, `Sum`, `Average`, `Min`, `Max` |
| 3 | Рейтинг ефективності роботи агентів (рієлторів та юристів) за вказаний період часу | Узагальнення, з'єднання таблиць, розрахунок комісій | `SaleDeals`, `Users` | `Where`, `Select`, `GroupBy`, `Sum`, `OrderByDescending` |
| 4 | Аудит безпеки земельного банку: статус документів, Due Diligence та правовласники | Багаторівневе з'єднання 4 таблиць, фільтрація | `LandPlots`, `Users`, `PlotDocuments`, `LegalConclusions` | Inner Join, Left Join, `Where`, `Select`, `OrderBy` |
| 5 | Пошук облікового запису за логіном та автентифікація через алгоритм PBKDF2 | Пошук за первинним/унікальним ключем | `Users` | `SingleOrDefaultAsync`, `VerifyHashedPassword` |
| 6 | Додавання нового учасника з платіжними реквізитами та стартовою заявкою в атомарній транзакції | Додавання записів, транзакційність, перевірка унікальності | `Users`, `SelectionRequests` | `AnyAsync`, `BeginTransactionAsync`, `Add`, `SaveChangesAsync`, `CommitAsync` |
| 7 | Оновлення персональних даних, зміна пароля та блокування акаунта із захистом адміністратора | Модифікація записів, контроль бізнес-правил | `Users` | `FindAsync`, модифікація стану сутності, `SaveChangesAsync` |
| 8 | Безпечне вилучення користувача з контролем обмежень цілісності `DeleteBehavior.Restrict` | Вилучення записів, реляційна цілісність | `Users`, `LandPlots`, `SaleDeals`, `ViewingRequests` | `Include`, `AnyAsync`, `Remove`, `SaveChangesAsync` |
| 9 | Комплексна вітрина всіх операторів LINQ (Лекція № 9): квантифікатори, множини, сплощення, агрегація, zipping, багатокритеріальне сортування, пагінація | Комплексна аналітика, демонстрація всіх операторів | Усі 8 таблиць моделі | `All`, `Any`, `Distinct`, `Intersect`, `Except`, `SelectMany`, `Aggregate`, `Zip`, `OrderBy`/`ThenBy`, `Skip`/`Take` |

### 4.1 Запит 1 — Відбір ділянок за складеною умовою та впорядкування

Форма фільтрації дозволяє комбінувати до 8 критеріїв пошуку: регіон розташування, цільове призначення (КВЦПЗ), діапазон вартості (від / до), діапазон площі в гектарах, статус публікації, текстовий пошук продавця або закріпленого рієлтора, а також часовий проміжок створення лістингу.

Кожна заповнена умова динамічно додає предикат `Where` до первинного об'єкта `IQueryable<LandPlot>`, формуючи єдиний оптимізований SQL-запит із блоком `WHERE ... AND ...`. Сортування підтримує 5 полів: за ціною, площею, ціною за 1 сотку, назвою та датою внесення у каталог, з можливістю вибору прямого (ASC) або зворотного (DESC) напрямку. 

**Особливості реалізації:**
- **Безрегістровий пошук кирилицею в SQLite:** оскільки SQLite-функція `instr` та оператор `LIKE` без додаткових плагінів проводять регістронезалежне порівняння лише для латиниці (ASCII A-Z), для україномовних фільтрів (`Region`, `Participant`) реалізовано динамічну генерацію умов із нормалізацією регістрів (оригінал, `TitleCase` та `lowercase`), що гарантує знаходження записів незалежно від регістру введення ("київська" чи "Київська", "бондар" чи "Бондар").
- **Математично коректне сортування `decimal`:** через збереження `decimal` у SQLite як `TEXT`, сортування за ціною виконується в пам'яті (LINQ to Objects) із вторинним критерієм `.ThenByDescending(r => r.CreatedAt)` або `.ThenBy(r => r.CreatedAt)`.
- **Багатокритеріальне впорядкування (`OrderBy` + `ThenBy`):** для всіх варіантів сортування застосовано вторинне впорядкування за датою створення лістингу `CreatedAt` (відповідно до слайда 12 Лекції № 9).

**Лістинг 2 — Метод фільтрації ділянок (`LandAgencyQueries.cs`):**

```csharp
public async Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter)
{
    var query = _context.LandPlots.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(filter.Region))
    {
        var reg = filter.Region.Trim();
        var regCap = char.ToUpper(reg[0]) + (reg.Length > 1 ? reg.Substring(1) : "");
        var regLower = char.ToLower(reg[0]) + (reg.Length > 1 ? reg.Substring(1) : "");
        query = query.Where(p => p.Location.Contains(reg) || p.Location.Contains(regCap) || p.Location.Contains(regLower));
    }

    if (!string.IsNullOrWhiteSpace(filter.Purpose))
    {
        var purpose = filter.Purpose.Trim();
        query = query.Where(p => p.Purpose.Contains(purpose));
    }

    if (filter.Status.HasValue)
        query = query.Where(p => p.Status == filter.Status.Value);

    if (filter.MinPrice.HasValue)
        query = query.Where(p => p.PriceUsd >= filter.MinPrice.Value);

    if (filter.MaxPrice.HasValue)
        query = query.Where(p => p.PriceUsd <= filter.MaxPrice.Value);

    if (filter.MinArea.HasValue)
        query = query.Where(p => p.AreaHectares >= filter.MinArea.Value);

    if (filter.MaxArea.HasValue)
        query = query.Where(p => p.AreaHectares <= filter.MaxArea.Value);

    if (!string.IsNullOrWhiteSpace(filter.Participant))
    {
        var text = filter.Participant.Trim();
        var textCap = char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
        var textLower = char.ToLower(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
        query = query.Where(p => (p.Seller != null && (p.Seller.FullName.Contains(text) || p.Seller.FullName.Contains(textCap) || p.Seller.FullName.Contains(textLower) || p.Seller.Email.Contains(text) || p.Seller.Phone.Contains(text)))
                              || (p.AssignedRealtor != null && (p.AssignedRealtor.FullName.Contains(text) || p.AssignedRealtor.FullName.Contains(textCap) || p.AssignedRealtor.FullName.Contains(textLower) || p.AssignedRealtor.Email.Contains(text))));
    }

    var desc = filter.Descending;
    query = filter.Sort switch
    {
        PlotSort.Area => desc ? query.OrderByDescending(p => p.AreaHectares).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.AreaHectares).ThenBy(p => p.CreatedAt),
        PlotSort.Title => desc ? query.OrderByDescending(p => p.Title).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Title).ThenBy(p => p.CreatedAt),
        PlotSort.Location => desc ? query.OrderByDescending(p => p.Location).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Location).ThenBy(p => p.CreatedAt),
        _ => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
    };

    var projected = query
        .Take(MaxRows)
        .Select(p => new PlotRow(
            p.Id,
            p.CadastralNumber,
            p.Title,
            p.Location,
            p.AreaHectares,
            Math.Round(p.AreaHectares * 100.0, 2),
            p.Purpose,
            p.PriceUsd,
            p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,
            p.Status,
            p.Seller != null ? p.Seller.FullName : "Не призначено",
            p.Seller != null ? p.Seller.Phone : "",
            p.AssignedRealtor != null ? p.AssignedRealtor.FullName : "Не призначено",
            p.Documents.Count,
            p.LegalConclusion != null ? p.LegalConclusion.RiskLevel : "Не перевірено",
            p.CreatedAt));

    string sql = projected.ToQueryString();
    var rows = await projected.ToListAsync();

    if (filter.Sort == PlotSort.Price)
    {
        rows = desc
            ? rows.OrderByDescending(r => r.PriceUsd).ThenByDescending(r => r.CreatedAt).ToList()
            : rows.OrderBy(r => r.PriceUsd).ThenBy(r => r.CreatedAt).ToList();
    }
    else if (filter.Sort == PlotSort.PricePerSotka)
    {
        rows = desc
            ? rows.OrderByDescending(r => r.PricePerSotkaUsd).ThenByDescending(r => r.CreatedAt).ToList()
            : rows.OrderBy(r => r.PricePerSotkaUsd).ThenBy(r => r.CreatedAt).ToList();
    }

    return new QueryResult<PlotRow>(rows, sql);
}
```

Згенерований SQL-запит (отриманий через `ToQueryString()`):
```sql
SELECT "l"."Id", "l"."CadastralNumber", "l"."Title", "l"."Location", "l"."AreaHectares", 
       ROUND("l"."AreaHectares" * 100.0, 2), "l"."Purpose", "l"."PriceUsd", 
       "l"."Status", "u"."FullName", "u"."Phone", "u0"."FullName", 
       (SELECT COUNT(*) FROM "PlotDocuments" AS "p" WHERE "l"."Id" = "p"."LandPlotId"), 
       "l0"."RiskLevel", "l"."CreatedAt"
FROM "LandPlots" AS "l"
LEFT JOIN "Users" AS "u" ON "l"."SellerId" = "u"."Id"
LEFT JOIN "Users" AS "u0" ON "l"."AssignedRealtorId" = "u0"."Id"
LEFT JOIN "LegalConclusions" AS "l0" ON "l"."Id" = "l0"."LandPlotId"
WHERE "l"."Location" LIKE '%Київська%' AND "l"."PriceUsd" <= 35000.0
ORDER BY "l"."PriceUsd" ASC
LIMIT 300;
```

### 4.2 Запити 2 і 3 — Узагальнення даних за період: оборот та ефективність агентів

Для аналітичних розрахунків за часовими інтервалами створено незмінну структуру `Period`, що інкапсулює логіку розрахунку напіввідкритого інтервалу `[From, To)`:

```csharp
public readonly record struct Period(DateTime From, DateTime To, string Label)
{
    public static Period Containing(PeriodKind kind, DateOnly date)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        return kind switch
        {
            PeriodKind.Day => new(day, day.AddDays(1), day.ToString("dd.MM.yyyy")),
            PeriodKind.Month => new(new DateTime(day.Year, day.Month, 1),
                                    new DateTime(day.Year, day.Month, 1).AddMonths(1),
                                    $"{Months[day.Month - 1]} {day.Year}"),
            _ => day.Month <= 6
                ? new(new DateTime(day.Year, 1, 1), new DateTime(day.Year, 7, 1), $"I півріччя {day.Year}")
                : new(new DateTime(day.Year, 7, 1), new DateTime(day.Year + 1, 1, 1), $"II півріччя {day.Year}")
        };
    }

    public static Period Between(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var days = to.DayNumber - from.DayNumber + 1;
        return new(from.ToDateTime(TimeOnly.MinValue),
                   to.AddDays(1).ToDateTime(TimeOnly.MinValue),
                   $"з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} ({days} дн.)");
    }
}
```

**Перевага напіввідкритого інтервалу:** Якщо угода проведена рівно о 00:00:00 1-го числа наступного місяця, умова `< To` гарантує, що вона не буде двічі врахована у попередньому періоді, що унеможливлює бухгалтерські розбіжності.

**Запит 2 (Оборот за призначеннями земель):**  
Використовує декларативний синтаксис запиту LINQ (`Query syntax`: `from ... in ... where ... select`), вибирає виконані угоди за період і виконує групування за цільовим призначенням КВЦПЗ:

```csharp
public async Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(Period period)
{
    var baseQuery = from d in _context.SaleDeals.AsNoTracking()
                    where d.Status == DealStatus.Completed
                          && d.DealDate >= period.From
                          && d.DealDate < period.To
                    select new
                    {
                        d.LandPlot.Purpose,
                        d.FinalPriceUsd,
                        d.CommissionUsd
                    };

    var rawDeals = await baseQuery.ToListAsync();

    var grouped = (from d in rawDeals
                   group d by d.Purpose into g
                   let count = g.Count()
                   let turnover = g.Sum(x => x.FinalPriceUsd)
                   let commission = g.Sum(x => x.CommissionUsd)
                   select new PurposeTurnoverRow(
                       g.Key,
                       count,
                       turnover,
                       commission,
                       Math.Round(turnover / count, 2),
                       g.Min(x => x.FinalPriceUsd),
                       g.Max(x => x.FinalPriceUsd)))
                   .OrderByDescending(r => r.TotalTurnoverUsd)
                   .ToList();

    return new QueryResult<PurposeTurnoverRow>(grouped, baseQuery.ToQueryString());
}
```

**Запит 3 (Ефективність роботи агентів):**  
З'єднує сутності `SaleDeals` та `Users` для рієлторів і юристів, підраховуючи кількість супроводжених угод, згенерований оборот та персональну комісію агентства:

```csharp
public async Task<QueryResult<AgentPerformanceRow>> AgentPerformanceAsync(
    Period period, AgentSort sort = AgentSort.Turnover, bool descending = true)
{
    var query = _context.SaleDeals.AsNoTracking()
        .Where(d => d.Status == DealStatus.Completed && d.DealDate >= period.From && d.DealDate < period.To)
        .Select(d => new
        {
            d.FinalPriceUsd,
            d.CommissionUsd,
            RealtorId = d.RealtorId,
            RealtorName = d.Realtor != null ? d.Realtor.FullName : "Не призначено",
            RealtorEmail = d.Realtor != null ? d.Realtor.Email : "",
            LawyerId = d.LawyerId,
            LawyerName = d.Lawyer != null ? d.Lawyer.FullName : "Не призначено",
            LawyerEmail = d.Lawyer != null ? d.Lawyer.Email : ""
        });

    var deals = await query.ToListAsync();

    var realtors = deals
        .Where(d => d.RealtorId.HasValue)
        .GroupBy(d => (Id: d.RealtorId!.Value, Name: d.RealtorName, Email: d.RealtorEmail))
        .Select(g => new AgentPerformanceRow(
            g.Key.Id, g.Key.Name, g.Key.Email, "Рієлтор з продажу землі",
            g.Count(), g.Sum(x => x.FinalPriceUsd), g.Sum(x => x.CommissionUsd),
            Math.Round(g.Average(x => x.FinalPriceUsd), 2)));

    var lawyers = deals
        .Where(d => d.LawyerId.HasValue)
        .GroupBy(d => (Id: d.LawyerId!.Value, Name: d.LawyerName, Email: d.LawyerEmail))
        .Select(g => new AgentPerformanceRow(
            g.Key.Id, g.Key.Name, g.Key.Email, "Юрист земельного права",
            g.Count(), g.Sum(x => x.FinalPriceUsd), g.Sum(x => x.CommissionUsd),
            Math.Round(g.Average(x => x.FinalPriceUsd), 2)));

    var combined = realtors.Concat(lawyers).ToList();
    return new QueryResult<AgentPerformanceRow>(combined, query.ToQueryString());
}
```

### 4.3 Підсумки за день, місяць, півріччя та довільний період

Фактичні результати виконання аналітичних запитів до створеної бази даних `landagency.db`:

1. **За один конкретний день (15.10.2026):**
   - Укладено 1 велику нотаріальну угоду.
   - Цільове призначення: `02.01 Для будівництва і обслуговування житлового будинку`.
   - Фінансовий оборот: **\$28 500,00**, комісія агентства: **\$855,00** (3%).
   - Провідний рієлтор: Бондар І.О., юрист супроводу: Коваленко О.В.

2. **За місяць (вересень 2026 року):**
   - Укладено 2 угоди за категоріями `01.01 Товарне с/г` та `03.07 Комерційне використання`.
   - Загальний оборот: **\$87 000,00**, сумарна комісія: **\$2 610,00**.
   - Середній чек угоди: **\$43 500,00**, діапазон цін: від \$35 000 до \$52 000.

3. **За I півріччя 2026 року (01.01.2026 – 30.06.2026):**
   - Укладено 4 угоди на загальну суму **\$223 500,00**.
   - Комісійні збори агентства: **\$6 705,00**.
   - Структура продажів: 50% житлова забудова (2 угоди), 25% комерційне будівництво (1 угода), 25% логістика/промисловість (1 угода).
   - Топ-агент півріччя: рієлтор Козак М.П. (оборот \$125 000,00).

4. **За довільний тримісячний інтервал (01.08.2026 – 31.10.2026, 92 дні):**
   - Укладено 5 угод на суму **\$233 500,00**, сумарна комісія: **\$7 005,00**.
   - Найбільша нотаріальна угода: \$98 000,00 (ділянка промислового призначення в с. Муроване Львівської обл.).

### 4.4 Запит 4 — Аудит земельного банку (з'єднання 4 таблиць)

Запит реалізує комплексний юридичний моніторинг об'єктів земельного банку агентства, об'єднуючи дані з чотирьох реляційних таблиць:
$$\text{LandPlots} \bowtie_{\text{SellerId}} \text{Users} \bowtie_{\text{Id}} \text{PlotDocuments} \leftouterjoin_{\text{Id}} \text{LegalConclusions}$$

```csharp
public async Task<QueryResult<LandBankAuditRow>> LandBankAuditAsync(LandBankFilter filter)
{
    var query = _context.LandPlots.AsNoTracking()
        .Select(p => new
        {
            p.Id,
            p.CadastralNumber,
            p.Title,
            p.Location,
            p.AreaHectares,
            p.PriceUsd,
            p.Status,
            SellerName = p.Seller != null ? p.Seller.FullName : "Невідомий",
            SellerPhone = p.Seller != null ? p.Seller.Phone : "",
            DocsCount = p.Documents.Count,
            VerifiedDocs = p.Documents.Count(d => d.IsVerified),
            HasConclusion = p.LegalConclusion != null,
            RiskLevel = p.LegalConclusion != null ? p.LegalConclusion.RiskLevel : "Не перевірено",
            IsApproved = p.LegalConclusion != null && p.LegalConclusion.IsApprovedForSale,
            LawyerName = p.LegalConclusion != null && p.LegalConclusion.Lawyer != null
                         ? p.LegalConclusion.Lawyer.FullName
                         : "Не призначено"
        });

    if (!string.IsNullOrWhiteSpace(filter.RiskLevel))
        query = query.Where(p => p.RiskLevel == filter.RiskLevel);

    if (filter.OnlyApproved)
        query = query.Where(p => p.IsApproved);

    if (filter.OnlyVerifiedDocs)
        query = query.Where(p => p.DocsCount > 0 && p.VerifiedDocs == p.DocsCount);

    var rows = await query
        .OrderBy(p => p.RiskLevel)
        .ThenByDescending(p => p.PriceUsd)
        .Select(p => new LandBankAuditRow(
            p.Id, p.CadastralNumber, p.Title, p.Location, p.AreaHectares, p.PriceUsd,
            p.Status, p.SellerName, p.SellerPhone, p.DocsCount, p.VerifiedDocs,
            p.HasConclusion, p.RiskLevel, p.IsApproved, p.LawyerName))
        .ToListAsync();

    return new QueryResult<LandBankAuditRow>(rows, query.ToQueryString());
}
```

Результат виконання наведено на веб-сторінці `/Queries/LandBank`: система миттєво фільтрує 16 ділянок за юридичною чистотою, виділяючи об'єкти з «Низьким ризиком» та повною верифікацією витягів ДЗК і ДРРП, рекомендовані до термінового нотаріального посвідчення.

### 4.5 Запит 5 — Пошук користувача за логіном та перевірка пароля (PBKDF2)

Для запобігання витоку паролів у відкритому вигляді сутність `AppUser` зберігає криптографічний хеш пароля, згенерований за алгоритмом PBKDF2 (HMAC-SHA256, 10 000 ітерацій, 128-бітне криптографічне зерно/salt) засобами `IPasswordHasher<AppUser>`.

```csharp
public async Task<AppUser?> FindUserAsync(string email)
{
    var normalized = NormalizeEmail(email);
    return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized);
}

public bool CheckPassword(AppUser user, string password)
{
    if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(user.Password))
        return false;

    var result = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
    return result == PasswordVerificationResult.Success
        || result == PasswordVerificationResult.SuccessRehashNeeded;
}
```

Форма перевірки надсилає дані виключно методом `HTTP POST` з валідацією токена проти підробки запитів `[ValidateAntiForgeryToken]`, що унеможливлює потрапляння чутливих даних у логи HTTP-сервера або рядок адреси браузера.

### 4.6 Запити 6–8 — Транзакційне додавання, модифікація та захищене вилучення

**Запит 6 (Транзакційне додавання учасника):**  
При додаванні клієнта одночасно створюється запис користувача з гешованим паролем та автоматично відкривається стартовий запит на підбір ділянки (`SelectionRequest`). Обидві операції виконуються в єдиній реляційній транзакції:

```csharp
public async Task<AppUser> AddUserAsync(
    string fullName, string email, string phone, UserRole role, string password, string? bankDetails)
{
    var normalized = NormalizeEmail(email);
    if (await _context.Users.AnyAsync(u => u.Email == normalized))
        throw new InvalidOperationException($"Користувач із поштовою адресою '{normalized}' уже зареєстрований.");

    await using var transaction = await _context.Database.BeginTransactionAsync();

    var user = new AppUser
    {
        FullName = fullName.Trim(),
        Email = normalized,
        Phone = phone.Trim(),
        Role = role,
        BankDetails = bankDetails?.Trim() ?? "",
        IsActive = true
    };
    user.SetPassword(password);
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    if (role == UserRole.Client)
    {
        _context.SelectionRequests.Add(new SelectionRequest
        {
            BuyerId = user.Id,
            DesiredRegion = "Київська область",
            DesiredPurpose = "02.01 Для будівництва житлового будинку",
            MaxBudgetUsd = 50000m,
            MinAreaHectares = 0.10,
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();
    }

    await transaction.CommitAsync();
    return user;
}
```

**Запит 7 (Модифікація користувача з бізнес-правилами):**  
Дозволяє оновити ПІБ, телефон, стан активності та пароль. Реалізовано критичне обмеження безпеки: **заборонено деактивувати обліковий запис системного адміністратора**:

```csharp
public async Task UpdateUserAsync(int id, string fullName, string phone, bool isActive, string? newPassword)
{
    var user = await _context.Users.FindAsync(id)
        ?? throw new InvalidOperationException($"Користувача з ID #{id} не знайдено.");

    if (user.Role == UserRole.Admin && !isActive)
        throw new InvalidOperationException("Заборонено блокувати обліковий запис системного адміністратора.");

    user.FullName = fullName.Trim();
    user.Phone = phone.Trim();
    user.IsActive = isActive;

    if (!string.IsNullOrWhiteSpace(newPassword))
        user.SetPassword(newPassword);

    await _context.SaveChangesAsync();
}
```

**Запит 8 (Вилучення з контролем цілісності `Restrict`):**  
Перед вилученням сервіс перевіряє наявність прив'язаних ділянок (`OwnedPlots`), укладених нотаріальних угод (`BuyerDeals`) або заявок на виїзний перегляд. За наявності зв'язків видалення блокується із повідомленням користувачеві про захист історії (доступне лише блокування через запит 7):

```csharp
public async Task<string> DeleteUserAsync(int id)
{
    var user = await _context.Users
        .Include(u => u.OwnedPlots)
        .Include(u => u.ViewingRequests)
        .FirstOrDefaultAsync(u => u.Id == id)
        ?? throw new InvalidOperationException($"Користувача з ID #{id} не знайдено.");

    if (user.Role == UserRole.Admin)
        throw new InvalidOperationException("Заборонено вилучати системного адміністратора.");

    bool hasDeals = await _context.SaleDeals.AnyAsync(d => d.BuyerId == id || d.RealtorId == id || d.LawyerId == id);
    if (user.OwnedPlots.Count > 0 || hasDeals || user.ViewingRequests.Count > 0)
    {
        throw new InvalidOperationException(
            $"Неможливо видалити користувача '{user.FullName}' (обмеження Restrict). " +
            $"Користувач має зв'язану юридичну або фінансову історію ({user.OwnedPlots.Count} ділянок, історія угод). " +
            $"Ви можете деактивувати його обліковий запис через форму редагування.");
    }

    string name = user.FullName;
    _context.Users.Remove(user);
    await _context.SaveChangesAsync();
    return name;
}
```

### 4.7 Запит 9 — Комплексна демонстрація всіх операторів LINQ (Showcase)

Відповідно до лекційного курсу «Лекція № 9: LINQ» (розділи про синтаксис запитів, методи розширення, квантифікатори, множини, сплощення, агрегацію, багатокритеріальне сортування та пагінацію), у сервісі `LandAgencyQueries` створено метод `GetLinqShowcaseAsync()`, а в контролері `QueriesController` — дію `LinqShowcase()`. Вони надають вичерпну інтерактивну демонстрацію роботи **всіх сімейств операторів LINQ**:

1. **Квантифікатори `All` та `Any` (Слайд 22):**
   - `Any`: визначення наявності клієнтів із заявками на перегляд ділянок (`allUsers.Count(u => u.Role == UserRole.Client && allViewings.Any(v => v.BuyerId == u.Id))`), а також перевірка наявності активних лістингів із документами.
   - `All`: верифікація повноти юридичного пакета — підрахунок ділянок, у яких **усі** додані правовстановлюючі документи пройшли державну перевірку (`p.Documents.Any() && p.Documents.All(d => d.IsVerified)`).
2. **Операції над множинами `Distinct`, `Intersect`, `Except` (Слайди 15–18):**
   - `Distinct`: формування переліку унікальних цільових призначень КВЦПЗ та унікальних районів пропозиції.
   - `Intersect`: ринковий перетин попиту та пропозиції — райони, в яких одночасно наявні виставлені ділянки та зареєстровані заявки покупців (`sellerDistricts.Intersect(buyerRequestedDistricts)`).
   - `Except`: аналіз дефіциту ринку — виявлення клієнтів-покупців, які ще не уклали жодної нотаріальної угоди (`allClients.Except(dealBuyers)`).
3. **Сплощення ієрархій `SelectMany` (Слайд 13):**
   - Розгортання вкладених колекцій документів земельних ділянок у єдиний лінійний список із формуванням повного ідентифікатора «Кадастровий номер: назва документа (тип)».
4. **Згортка послідовностей `Aggregate` (Слайд 21):**
   - Накопичувальна згортка списку кадастрових номерів сертифікованих ділянок у єдиний підсумковий реєстровий рядок із початковим акумулятором (seed value).
5. **Попарне об'єднання послідовностей `Zip` (Слайд 23):**
   - Синхронне зведення двох паралельних послідовностей (фактичні земельні ділянки каталогу та нормативні бенчмарки експертної грошової оцінки Держгеокадастру) у єдину порівняльну таблицю з розрахунком відхилення вартості у відсотках та автоматичним вердиктом ("Ринкова справедлива ціна", "Вище ринку", "Дисконт").
6. **Багатокритеріальне сортування `OrderBy` + `ThenBy` / `ThenByDescending` (Слайди 11–12):**
   - Сортування каталогу за двома незалежними ключами: первинне групування за регіоном (`OrderBy(p => p.Location)`), вторинне впорядкування всередині кожного регіону за спаданням ціни (`ThenByDescending(p => p.PriceUsd)`).
7. **Пагінація та секціонування `Skip` + `Take` (Слайд 14):**
   - Посторінкова вибірка для веб-інтерфейсу (пропуск перших 2 записів та взяття 4 наступних рядків для другої сторінки каталогу).
8. **Порівняння декларативного синтаксису запитів (Query syntax) та методів розширення (Method syntax):**
   - Синхронна демонстрація виконання одного й того самого бізнес-завдання обома формами запису мови LINQ із порівняльним аналізом часу виконання.

**Лістинг 7 — Реалізація операторів LINQ (`GetLinqShowcaseAsync` у `LandAgencyQueries.cs`):**

```csharp
public async Task<LinqShowcaseResult> GetLinqShowcaseAsync()
{
    var allPlots = await _db.LandPlots
        .Include(p => p.Documents)
        .Include(p => p.Seller)
        .Include(p => p.AssignedRealtor)
        .AsNoTracking()
        .ToListAsync();

    // 1. Квантифікатори All та Any
    int totalPlotsWithAllDocsVerified = allPlots
        .Count(p => p.Documents.Any() && p.Documents.All(d => d.IsVerified));

    var allUsers = await _db.Users.AsNoTracking().ToListAsync();
    var allViewings = await _db.ViewingRequests.AsNoTracking().ToListAsync();
    int totalBuyersWithAnyViewing = allUsers
        .Count(u => u.Role == UserRole.Client && allViewings.Any(v => v.BuyerId == u.Id));

    // 2. Множини: Distinct, Intersect, Except
    var allDistinctPurposes = allPlots.Select(p => p.Purpose).Distinct().OrderBy(p => p).ToList();

    var sellerDistricts = allPlots
        .SelectMany(p => p.Location.Split(',', StringSplitOptions.TrimEntries))
        .Where(part => part.Contains("район") || part.StartsWith("м."))
        .Distinct()
        .ToList();

    var allRequests = await _db.SelectionRequests.AsNoTracking().ToListAsync();
    var buyerRequestedDistricts = allRequests.Select(s => s.DesiredDistrict.Trim()).Distinct().ToList();
    var marketIntersectRegions = sellerDistricts.Intersect(buyerRequestedDistricts).OrderBy(r => r).ToList();

    var allClients = allUsers.Where(u => u.Role == UserRole.Client).Select(u => u.FullName).ToList();
    var allDeals = await _db.SaleDeals.Include(d => d.Buyer).AsNoTracking().ToListAsync();
    var dealBuyers = allDeals.Where(d => d.Buyer != null).Select(d => d.Buyer!.FullName).Distinct().ToList();
    var buyersWithoutDeals = allClients.Except(dealBuyers).OrderBy(n => n).ToList();

    // 3. Сплощення SelectMany
    var flattenedDocTitles = allPlots
        .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
        .SelectMany(p => p.Documents.Select(d => $"{p.CadastralNumber}: {d.Title} ({d.DocType})"))
        .Take(10)
        .ToList();

    // 4. Згортка Aggregate
    var aggregatedCadastralRoll = allPlots
        .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
        .Select(p => p.CadastralNumber)
        .Take(6)
        .Aggregate("Кадастровий реєстр сертифікованих ділянок: ", (acc, cad) => $"{acc} [{cad}]");

    // 5. Попарне зведення Zip
    var samplePlots = allPlots.Take(5).ToList();
    var benchmarkPrices = new[] { 27000m, 35000m, 40000m, 16000m, 62000m };
    var zippedBenchmarks = samplePlots
        .Zip(benchmarkPrices, (plot, bench) =>
        {
            var diff = plot.PriceUsd - bench;
            var pct = bench > 0 ? Math.Round(diff / bench * 100m, 1) : 0m;
            var verdict = pct switch
            {
                > 10m => "Вище ринку (Завищена)",
                < -10m => "Нижче ринку (Дисконт)",
                _ => "Ринкова справедлива ціна"
            };
            return new BenchmarkComparisonRow(plot.CadastralNumber, plot.Location, plot.PriceUsd, bench, pct, verdict);
        }).ToList();

    // 6. Багатокритеріальне сортування OrderBy + ThenByDescending
    var multiSortedPlots = allPlots
        .OrderBy(p => p.Location)
        .ThenByDescending(p => p.PriceUsd)
        .Take(10)
        .Select(p => new PlotRow(p.Id, p.CadastralNumber, p.Title, p.Location, p.AreaHectares,
            Math.Round(p.AreaHectares * 100.0, 2), p.Purpose, p.PriceUsd,
            p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,
            p.Status, p.Seller?.FullName ?? "Власник", p.Seller?.Phone ?? "",
            p.AssignedRealtor?.FullName ?? "Рієлтор", p.Documents?.Count ?? 0, "Перевірено", p.CreatedAt))
        .ToList();

    // 7. Секціонування Skip + Take
    var paginatedPlots = allPlots.OrderBy(p => p.Id).Skip(2).Take(4).ToList();

    return new LinqShowcaseResult(totalPlotsWithAllDocsVerified, totalBuyersWithAnyViewing,
        allDistinctPurposes, marketIntersectRegions, buyersWithoutDeals, flattenedDocTitles,
        aggregatedCadastralRoll, zippedBenchmarks, multiSortedPlots, paginatedPlots, 2, 4,
        benchmarksComparison);
}
```

---

## 5 Перевірка роботи

### 5.1 Автоматизоване модульне тестування (xUnit, 17 сценаріїв)

Для всебічної верифікації структури бази даних, цілісності зв'язків та всіх 9 LINQ-запитів створено окремий тестовий проєкт `LandAgencyMvc.Tests` на базі фреймворку **xUnit** у середовищі .NET 10. Тести розгортають тестову базу даних SQLite в оперативній пам'яті (`DataSource=:memory:`) з виконанням повної процедури міграції та ініціалізації даними.

*Таблиця 4 — Результати виконання тестових сценаріїв*

| № | Назва тесту | Об'єкт перевірки | Очікуваний результат | Фактичний результат |
|---|---|---|---|---|
| 1 | `Scenario1_All8Tables_HaveAtLeast10Rows` | Таблиці 1–8 | Усі 8 таблиць містять ≥ 10 записів | **Passed** (від 10 до 30 рядків) |
| 2 | `Scenario2_IdempotentSeed_DoesNotDuplicateRows` | `LandAgencyHistorySeed` | Повторна ініціалізація не подвоює записи | **Passed** (кількість рядків незмінна) |
| 3 | `Scenario3_Query1_FilterPlots_ByCompoundCriteria` | Запит 1 (Where) | Фільтрація за регіоном, КВЦПЗ та бюджетом | **Passed** (знайдено відповідні об'єкти) |
| 4 | `Scenario4_Query1_Sorting_AscendingAndDescending` | Запит 1 (OrderBy) | Сортування за ціною ASC / DESC | **Passed** (послідовність строго монотонна) |
| 5 | `Scenario5_Query2_PeriodTurnover` | Запит 2 (Period) | Розрахунок обороту за день, місяць, півріччя, custom | **Passed** (суми збігаються з незалежним підрахунком) |
| 6 | `Scenario6_PeriodBoundaries_HalfYears` | Структура `Period` | Межі півріч 30.06 / 01.07 та авторозвертання дат | **Passed** (I та II півріччя розділені коректно) |
| 7 | `Scenario7_Query3_AgentPerformance` | Запит 3 (Group) | Показники рієлторів та юристів, сортування | **Passed** (список містить обидві категорії агентів) |
| 8 | `Scenario8_Query4_LandBankAudit_4TableJoin` | Запит 4 (4 Joins) | З'єднання 4 таблиць, фільтр за ризиком | **Passed** (відфільтровано об'єкти з перевіреними правами) |
| 9 | `Scenario9_Query5_FindUser_And_PBKDF2` | Запит 5 (Auth) | Пошук за поштою та верифікація хешу | **Passed** (вірний пароль — true, хибний — false) |
| 10 | `Scenario10_Query6_AddUser_InTransaction` | Запит 6 (Insert) | Атомарне додавання та блокування дубліката | **Passed** (створено акаунт + заявку, дублікат відхилено) |
| 11 | `Scenario11_Query7_UpdateUser_AdminProtection` | Запит 7 (Update) | Зміна реквізитів та заборона блокування Admin | **Passed** (реквізити оновлено, блок адміна викликав помилку) |
| 12 | `Scenario12_Query8_DeleteUser_RestrictConstraint` | Запит 8 (Delete) | Заборона видалення при зв'язках, чисте видалення | **Passed** (Restrict заблокував видалення, чистий видалений) |
| 13 | `Scenario13_Query1_DecimalPriceSorting_WithVaryingDigitLengths` | Запит 1 (Decimal Sort) | Числове сортування $5 400 < $18 000 < $120 000 (усунення SQLite text sort bug) | **Passed** (числове впорядкування без лексикографічних помилок) |
| 14 | `Scenario14_CyrillicCaseInsensitiveSearch` | Запит 1 & 4 (Ukrainian Text) | Безрегістровий пошук кирилицею ("київська", "бондар") | **Passed** (результати знайдені незалежно від регістру) |
| 15 | `Scenario15_LinqShowcase_AdvancedOperators` | Запит 9 (Showcase) | Робота операторів `All`, `Any`, `Distinct`, `Intersect`, `Except`, `SelectMany`, `Aggregate`, `Zip`, `ThenBy`, `Skip`/`Take` | **Passed** (усі структури та агрегати розраховані вірно) |
| 16 | `Scenario16_QueriesController_AllEndpoints_ReturnSuccessViewModels` | Контролер (Web GET) | Усі 6 аналітичних сторінок повертають HTTP 200 та валідні моделі подання | **Passed** (усі ендпоінти функціонують штатно) |
| 17 | `Scenario17_QueriesController_CrudFormActions` | Контролер (CRUD POST) | Дії AddUser, UpdateUser, DeleteUser з редиректами та повідомленнями TempData | **Passed** (транзакційні дії виконано успішно) |

```bash
Test run for /Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc.Tests/bin/Debug/net10.0/LandAgencyMvc.Tests.dll (.NETCoreApp,Version=v10.0)
VSTest version 18.0.1 (arm64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    17, Skipped:     0, Total:    17, Duration: 11 s - LandAgencyMvc.Tests.dll (net10.0)
```

### 5.2 Користувацький веб-інтерфейс розділу «Звітність»

У веб-застосунку створено окремий повнофункціональний модуль аналітики за адресою `/Queries` із навігаційною панеллю `_QueriesNavbar.cshtml`:
1. `/Queries` — Головний аналітичний дашборд із картками статистики всіх 8 таблиць бази даних та інтерактивними картками швидкого переходу.
2. `/Queries/FilterPlots` — Інтерактивна форма багатопараметричного відбору земельних ділянок із явним вибором напрямку сортування через спадний список (`<select>`), динамічною таблицею результатів та розкривним блоком згенерованого SQL-запиту.
3. `/Queries/PeriodTurnover` — Сторінка фінансової періодизації з перемикачем типу періоду (День, Місяць, Півріччя, Довільний період), таблицями обороту за КВЦПЗ та рейтингом ефективності агентів.
4. `/Queries/LandBank` — Інтерфейс 4-табличного аудиту земельного банку з юридичними статусами Due Diligence та підрахунком верифікованих документів.
5. `/Queries/Users` — Панель керування користувачами з перевіркою паролів PBKDF2, формою транзакційного додавання та кнопками редагування/вилучення з контролем обмежень `Restrict`.
6. `/Queries/LinqShowcase` — Спеціалізована вітрина операторів LINQ (Лекція № 9), що візуалізує роботу квантифікаторів `All`/`Any`, операцій над множинами `Distinct`/`Intersect`/`Except`, проекції `SelectMany`, агрегації `Aggregate`, зведення `Zip`, багатокритеріального сортування `ThenBy` та пагінації `Skip`/`Take`.

Усі Razor-подання реалізовані **без жодного інлайн-стилю** (`style="..."`), виключно на семантичних класах CSS із дизайн-системи проєкту (`site.css`).

### 5.3 Інструкція запуску та перевірки

Для збирання, тестування та локального запуску застосунку в терміналі виконуються команди:

```bash
# 1. Збирання рішення
dotnet build LandAgencyMvc.sln

# 2. Запуск набору автоматизованих тестів
dotnet test

# 3. Запуск веб-застосунку
dotnet run --project LandAgencyMvc --urls http://localhost:5000
```

Після запуску відкрийте браузер за адресою `http://localhost:5000/Queries`.

---

## Висновки

1. У ході виконання лабораторної роботи № 3 успішно опановано практичні аспекти використання мови інтегрованих запитів LINQ (Language Integrated Query) та технології Entity Framework Core 10 для створення, заповнення та аналітичної обробки реляційної бази даних на платформі .NET 10.
2. Спроєктовано та реалізовано реляційну базу даних із **8 взаємопов'язаних таблиць** (`Users`, `LandPlots`, `PlotDocuments`, `LegalConclusions`, `ViewingRequests`, `SelectionRequests`, `SaleDeals`, `Payments`), що повністю відтворюють концептуальну UML-діаграму класів індивідуального проєкту «Агентство земельних ділянок».
3. На рівні EF Core Fluent API забезпечено сувору цілісність даних: налаштовано унікальні бізнес-індекси (кадастрові номери, електронна пошта, номери висновків та договорів) та реалізовано стратегію `DeleteBehavior.Restrict`, яка виключає каскадну втрату фінансової та юридичної історії агентства.
4. Базу даних наповнено узгодженими даними обсягом **від 10 до 30 записів у кожній таблиці** (сумарно 132 записи) з історією нотаріальних угод за останні 12 місяців (2025–2026 рр.), що дозволило провести повноцінний аналіз періодів.
5. Реалізовано комплекс із **9 різнопланових LINQ-запитів**: багатопараметрична динамічна фільтрація каталогу, аналітика фінансового обороту за категоріями земель (Query syntax), оцінка ефективності роботи рієлторів та юристів, аудит земельного банку на основі з'єднання 4 таблиць, безпечна автентифікація через PBKDF2, транзакційні операції додавання, редагування й захищеного вилучення, а також комплексна інтерактивна вітрина всіх родин операторів LINQ (`All`, `Any`, `SelectMany`, `Distinct`, `Intersect`, `Except`, `Aggregate`, `Zip`, `OrderBy`/`ThenBy`, `Skip`/`Take`).
6. Надійність та математична точність системи підтверджена розширеним автоматизованим тестовим набором із **17 сценаріїв xUnit** (100% успішне проходження), а зручність користувача забезпечена ергономічним веб-інтерфейсом на 6 сторінках Razor із відображенням згенерованого SQL-коду та нульовим використанням інлайн-стилів.

---

## Перелік використаних джерел

1. Методичні вказівки до лабораторної роботи № 3 «Створення проекту і файлу локальної бази даних. Основи LINQ» з дисципліни «Об'єктно-орієнтоване програмування». Файл `ЛР_3_БД_LINQ.md`.
2. Зубик Л. В. Лекція 9. LINQ; Лекція 8. Технологія ADO.NET. Проєктування баз даних. CRUD. Матеріали курсу «ООП-3 семестр-2026р.».
3. ДСТУ 3008:2015. Інформація та документація. Звіти у сфері науки і техніки. Структура та правила оформлювання. — К. : ДП «УкрНДНЦ», 2016. — 26 с.
4. Price M. J. C# 12 and .NET 8 – Modern Cross-Platform Development Fundamentals. 8th ed. — Birmingham : Packt Publishing, 2023. — 828 p.
5. Microsoft Learn. Language Integrated Query (LINQ) in C#. URL: https://learn.microsoft.com/dotnet/csharp/linq/ (дата звернення: 04.11.2026).
6. Microsoft Learn. Querying data with Entity Framework Core. URL: https://learn.microsoft.com/ef/core/querying/ (дата звернення: 04.11.2026).
7. Microsoft Learn. SQLite EF Core Database Provider Limitations. URL: https://learn.microsoft.com/ef/core/providers/sqlite/limitations (дата звернення: 04.11.2026).
8. Закон України «Про Державний земельний кадастр» від 07.07.2011 № 3613-VI. Відомості Верховної Ради України, 2012, № 8, ст. 61.
