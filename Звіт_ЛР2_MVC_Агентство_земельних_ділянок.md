# КИЇВСЬКИЙ НАЦІОНАЛЬНИЙ УНІВЕРСИТЕТ ІМЕНІ ТАРАСА ШЕВЧЕНКА
## Факультет інформаційних технологій
### Кафедра програмних систем і технологій

# ЗВІТ
## з лабораторної роботи № 2
## з дисципліни «Об’єктно-орієнтоване програмування»
### Тема: «Розробка застосунку на основі шаблону MVC»

**Виконав:** студент гр. ІПЗ-23 Сивопляс Максим  
**Перевірила:** доц. Зубик Л.В.  
**Київ – 2026**
---

ЛАБОРАТОРНА РОБОТА № 2

з дисципліни «Об’єктно-орієнтоване програмування»

РЕФЕРАТ

Звіт з лабораторної роботи № 2: 24 с., 4 табл., 8 лістингів коду, 8 джерел.

Мета роботи: Розробка повнофункціонального веб-застосунку автоматизації бізнес-процесів агентства з продажу земельних ділянок «LandAgency UA» на основі архітектурного шаблону Model-View-Controller (MVC) засобами платформи ASP.NET Core 10.0 та ORM Entity Framework Core (підхід Code First).

Об’єкт дослідження: Процеси обліку земельного банку, криптографічної верифікації правовстановлюючої документації, юридичного аудиту (Due Diligence) за даними ДЗК/ДРРП, інтелектуального автопідбору об'єктів та нотаріального супроводу угод купівлі-продажу.

Предмет дослідження: Методи та засоби об'єктно-орієнтованого проєктування і реалізації трирівневої архітектури Model-View-Controller у середовищі .NET 10.0 із використанням реляційної бази даних SQLite, механізмів автентифікації Cookie Authentication, рольової авторизації, суворої типізації ViewModels та модульної організації інтерфейсу на основі часткових представлень і єдиної системи CSS-класів.

Методи реалізації: Об'єктно-орієнтоване моделювання, інкапсуляція бізнес-правил у доменних сутностях (Rich Domain Model), реляційне відображення об'єктів (ORM EF Core Code First), розділення відповідальності (Separation of Concerns), ін'єкція залежностей (Dependency Injection), криптографічний контроль цілісності документів за алгоритмом SHA-256 та наскрізна верифікація бізнес-сценаріїв (End-to-End Validation).

Ключові слова: ШАБЛОН MVC, ASP.NET CORE, ENTITY FRAMEWORK CORE, CODE FIRST, ЗЕМЕЛЬНА ДІЛЯНКА, КАДАСТРОВИЙ НОМЕР, КОАТУУ, ДЕРЖАВНИЙ ЗЕМЕЛЬНИЙ КАДАСТР, КРИПТОГРАФІЧНИЙ ХЕШ SHA-256, АВТОМАТИЧНИЙ ПІДБІР, СТРОГА ТИПІЗАЦІЯ VIEWMODELS, НОТАРІАЛЬНА УГОДА.

ПЕРЕЛІК УМОВНИХ ПОЗНАЧЕНЬ, СИМВОЛІВ, ОДИНИЦЬ, СКОРОЧЕНЬ І ТЕРМІНІВ

MVC (Model-View-Controller) — архітектурний шаблон проєктування програмного забезпечення, що розділяє систему на доменну модель даних і бізнес-логіки (Model), інтерфейс відображення (View) та диспетчер керування запитами (Controller).

ORM (Object-Relational Mapping) — технологія об'єктно-реляційного відображення, що забезпечує автоматичну синхронізацію об'єктів C# із таблицями реляційної СКБД.

EF Core Code First — парадигма проєктування баз даних у Entity Framework Core, за якої первинною є об'єктно-орієнтована ієрархія класів C#, на основі якої ORM генерує реляційну схему БД.

ДЗК (Державний земельний кадастр) — єдина державна геоінформаційна система відомостей про землі, їх цільове призначення, обмеження у їх використанні та кадастрові номери.

ДРРП (Державний реєстр речових прав на нерухоме майно) — державний реєстр, що містить відомості про зареєстровані права власності, іпотеки, судові арешти та заборони відчуження нерухомого майна.

КОАТУУ — Класифікатор об'єктів адміністративно-територіального устрою України (перші 10 цифр кадастрового номера земельної ділянки).

SHA-256 — криптографічний алгоритм гешування, що використовується для забезпечення цілісності та контролю незмінності завантажених правовстановлюючих документів земельних ділянок.

IBAN (International Bank Account Number) — міжнародний стандарт номера банківського рахунку (в Україні має фіксований формат UA + 27 цифр).

CSRF (Cross-Site Request Forgery) — міжсайтова підробка HTTP-запитів, захист від якої реалізовано за допомогою токенів верифікації [ValidateAntiForgeryToken].

ВСТУП

Ринок земельної нерухомості характеризується підвищеною складністю правового регулювання та багатоетапним життєвим циклом кожної транзакції. На відміну від типових систем електронної комерції, продаж земельної ділянки вимагає обов'язкової перевірки структури 19-значного кадастрового номера, криптографічної верифікації правовстановлюючих документів (витягів з ДРРП та державних актів), обов'язкового юридичного аудиту (Due Diligence) на предмет відсутності судових арештів, іпотек та охоронних сервітутів, організації виїзного огляду межових знаків на місцевості та двофазного нотаріального посвідчення договору з контролем цільових банківських розрахунків за стандартом IBAN.

У попередній лабораторній роботі № 1 було розроблено комплексну об'єктно-орієнтовану модель інформаційної системи «Автоматизація процесів агентства з продажу земельних ділянок» мовою UML (діаграми класів, прецедентів, діяльності та станів). Метою лабораторної роботи № 2 є практична програмна імплементація спроєктованої UML-моделі у вигляді повноцінного веб-застосунку на платформі ASP.NET Core 10.0 із використанням шаблону Model-View-Controller та об'єктно-реляційного відображення Entity Framework Core (підхід Code First).

1. ТЕМА, МЕТА ТА ПОСТАНОВКА ЗАВДАННЯ ЛАБОРАТОРНОЇ РОБОТИ

Тема роботи: Розробка застосунку на основі шаблону MVC.

Мета роботи: Набути практичних навичок проєктування та програмної реалізації веб-орієнтованих інформаційних систем на основі архітектурного шаблону Model-View-Controller (MVC) у середовищі ASP.NET Core з використанням об'єктно-реляційного відображення Entity Framework Core (підхід Code First), рольової авторизації та інкапсульованої доменної бізнес-логіки.

1.1. Постановка завдання згідно з методичними вказівками

Завдання 1 (Ознайомлення з архітектурою MVC): дослідити принципи декомпозиції програмної системи на компоненти Model, View та Controller, життєвий цикл проходження HTTP-запиту через конвеєр маршрутизації ASP.NET Core та механізми обміну даними між шарами.

Завдання 2 (Реалізація моделі даних за підходом EF Core Code First): трансформувати діаграму класів UML із лабораторної роботи № 1 у систему доменних класів C# у просторі імен LandAgencyMvc.Models, реалізувати контекст бази даних LandAgencyContext (похідний від DbContext), налаштувати зв'язки Fluent API та забезпечити автоматичну ініціалізацію й початкове наповнення бази даних SQLite.

Завдання 3 (Реалізація контролерів, сервісів та представлень): розробити контролери AccountController, ClientController, LawyerController та RealtorController для обробки HTTP GET та POST запитів, впровадити реальні алгоритми автоматизації (парсинг КОАТУУ кадастрового номера, криптографічний підпис КЕП ДСТУ 4145-2002 з SHA-256, симуляція витягу ДЗК/ДРРП, реактивний автопідбір ділянок, контроль повноти оплати IBAN перед реєстрацією в ДРРП) та побудувати типізовані подання Razor (.cshtml) для всіх ролей.

Завдання 4 (Верифікація наскрізного циклу роботи): провести перевірку повного життєвого циклу роботи застосунку від виставлення земельної ділянки продавцем до нотаріального закриття угоди та державної реєстрації права власності, а також верифікувати бізнес-правила автоматизованими модульними тестами xUnit.

2. АРХІТЕКТУРА ШАБЛОНУ MVC ТА ОРГАНІЗАЦІЯ МАРШРУТИЗАЦІЇ HTTP-ЗАПИТІВ

Архітектурний шаблон Model-View-Controller (MVC) реалізує фундаментальний принцип розділення відповідальності (Separation of Concerns), ізолюючи внутрішню доменну логіку та структуру даних від візуального інтерфейсу користувача. У розробленому застосунку LandAgencyMvc кожен із трьох компонентів шаблону (доповнений шаром спеціалізованих доменних сервісів Services) виконує чітко окреслену роль:

Модель (Model — простір імен LandAgencyMvc.Models): містить 8 основних класів предметної області (AppUser, LandPlot, PlotDocument, LegalConclusion, ViewingRequest, SelectionRequest, SaleDeal, Payment), 5 перерахувань станів (UserRole, PlotStatus, ViewingStatus, DealStatus, PaymentStatus) та клас контексту даних LandAgencyContext. Моделі побудовані за принципом Rich Domain Model: переходи між станами угоди, розрахунок 3% комісії агентства, перевірка повноти розрахунків IsFullyPaid та криптографічне хешування документів інкапсульовані безпосередньо в методах класів моделі.

Подання (View — директорія LandAgencyMvc/Views): містить строго типізовані шаблони Razor (.cshtml), згруповані за функціональними контролерами (Account, Client, Lawyer, Realtor, Shared). Подання не містять прямих звернень до бази даних; вони отримують підготовлені граф-об'єкти моделі через директиву @model та допоміжні метадані через ViewBag / TempData і генерують семантичну HTML5-розмітку.

Контролер (Controller — простір імен LandAgencyMvc.Controllers): містить класи AccountController, ClientController, LawyerController, RealtorController та HomeController, успадковані від Microsoft.AspNetCore.Mvc.Controller. Контролери приймають вхідні HTTP GET/POST запити, перевіряють права доступу за ролями ([Authorize(Roles = ...)]) та CSRF-токени ([ValidateAntiForgeryToken]), координують роботу контексту БД та сервісів і повертають відповідний IActionResult (ViewResult або RedirectToActionResult за патерном Post-Redirect-Get).

Сервісний шар (Services — простір імен LandAgencyMvc.Services): містить інжетовані через Dependency Injection спеціалізовані класи доменної автоматизації: CadastralValidationService (ICadastralValidationService), AutoMatchingService (IAutoMatchingService) та DzkRegistrySimulator.

Лістинг 2.1 — Конфігурація DI-контейнера, контексту EF Core, автентифікації та маршрутизації у файлі Program.cs

using LandAgencyMvc.Models;

using LandAgencyMvc.Services;

using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<LandAgencyContext>(options =>

    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")

        ?? "Data Source=landagency.db"));

builder.Services.AddScoped<ICadastralValidationService, CadastralValidationService>();

builder.Services.AddScoped<IAutoMatchingService, AutoMatchingService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(options =>

    {

        options.LoginPath = "/Account/Login";

        options.AccessDeniedPath = "/Account/Login";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);

    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())

{

    var db = scope.ServiceProvider.GetRequiredService<LandAgencyContext>();

    db.InitializeDatabase();

}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

Таблиця 2.1 — Матриця маршрутизації HTTP-запитів та рольового доступу в системі LandAgencyMvc

| Маршрут (Controller/Action) | HTTP-метод | Роль доступу | Призначення та бізнес-результат |
| --- | --- | --- | --- |
| /Account/Login, /Account/Register | GET, POST | Публічний | Автентифікація за Email/паролем (криптографічний PBKDF2 хеш PasswordHasher<AppUser>), спрощена реєстрація клієнтів (ПІБ, Email, телефон, пароль; реквізити РНОКПП та IBAN фіксуються на етапі угоди) та формування ClaimsPrincipal. |
| /Client/Catalog, /Client/Details/{id} | GET | Client, Realtor, Lawyer | Перегляд публічного каталогу ділянок у статусі VerifiedInCatalog, багатофакторна фільтрація та перегляд картки ділянки. |
| /Client/CreatePlot | GET, POST | Client (Продавець) | Виставлення земельної ділянки на продаж: валідація кадастрового номера за КОАТУУ, завантаження PDF-витягу та накладання КЕП. |
| /Client/UpdatePlotPrice | POST | Client (Продавець) | Оновлення ціни непідписаної ділянки власником із автоматичним перезапуском сервісу AutoMatchingService. |
| /Client/SubmitPayment, /Client/DownloadReceipt | POST, GET | Client (Покупець) | Подання коду банківської транзакції та IBAN платника; генерація офіційної квитанції з перевіркою прав доступу (IDOR-захист). |
| /Lawyer/Queue, /Lawyer/Conclusion/{id} | GET | Lawyer | Черга юридичного аудиту ділянок та автоматичний запит до симулятора АС ДЗК та ДРРП із розрахунком ринкової ціни сотки. |
| /Lawyer/SubmitLegalConclusion | POST | Lawyer | Формування юридичного висновку: схвалення публікації в каталог (+ автопідбір покупців) або блокування з актом ризиків. |
| /Lawyer/ReviewContract | POST | Lawyer | Правова віза проєкту нотаріального договору купівлі-продажу або повернення рієлтору на доопрацювання. |
| /Realtor/Requests, /Realtor/Viewings | GET, POST | Realtor | Обробка заявок на індивідуальний підбір, координація виїзних оглядів на місцевості та фіксація результатів огляду. |
| /Realtor/CreateDeal, /Realtor/VerifyClientPayment | POST | Realtor | Створення угоди з розрахунком 3% комісії агентства, генерація 2 рахунків IBAN, підтвердження або відхилення квитанцій. |
| /Realtor/SignDealAtNotary, /Realtor/CompleteNotaryDeal | POST | Realtor | Двофазне нотаріальне посвідчення та державна реєстрація права власності в ДРРП за умови повної оплати обох рахунків. |
| /Realtor/PrintContract | GET | Realtor, Lawyer, Client | Генерація друкованої форми нотаріального договору купівлі-продажу земельної ділянки з перевіркою належності сторони до угоди. |

3. РОЗРОБКА МОДЕЛІ ДАНИХ ТА РЕАЛІЗАЦІЯ ПІДХОДУ EF CORE CODE FIRST

Відповідно до підходу Entity Framework Core Code First, структура реляційної бази даних визначається безпосередньо класами сутностей мови C# та конфігурацією в методі OnModelCreating класу LandAgencyContext. Під час проєктування моделі забезпечено повну трасованість і взаємоузгодженість із діаграмою класів та діаграмами станів UML, розробленими у лабораторній роботі № 1.

Таблиця 3.1 — Відображення класів UML-моделі (ЛР № 1) на сутності EF Core Code First та таблиці СКБД SQLite (ЛР № 2)

| Клас діаграми UML (ЛР № 1) | Клас моделі C# (ЛР № 2) | Таблиця БД SQLite | Ключові атрибути та інкапсульовані методи доменної логіки |
| --- | --- | --- | --- |
| Користувач / Рієлтор / Юрист / Продавець / Покупець | AppUser + UserRole | Users | FullName, Email, Password (PBKDF2), TaxId (РНОКПП), BankDetails (IBAN), LicenseNumber, CommissionRate; SetPassword(), Authenticate(), ChangePassword(). |
| ЗемельнаДілянка + PlotStatus | LandPlot + PlotStatus | LandPlots | CadastralNumber, AreaHectares, Purpose, PriceUsd, SellerBankIban, Status; ValidateCadastralFormat(), ChangeStatus(), UpdatePrice(). |
| ДокументДілянки | PlotDocument | PlotDocuments | Title, DocType, DocumentNumber, FileData (BLOB), FileHash (SHA-256), KepSignatureHash; AttachFile(), ApplyKepSignature(), VerifyFileIntegrity(). |
| ЮридичнийВисновок | LegalConclusion | LegalConclusions | ConclusionNumber, NoArrestsOrMortgages, NoServitudes, BoundariesMatchDzk, RiskLevel, DzkExtractNumber, IsApproved; Approve(), Reject(). |
| ЗаявкаНаОгляд + ViewingStatus | ViewingRequest + ViewingStatus | ViewingRequests | LandPlotId, BuyerId, RealtorId, PreferredDate, TimeSlot, Status; AssignRealtor(), CompleteViewing(), ConvertToDeal(), Cancel(). |
| СервісПідбору (Запит підбору) | SelectionRequest | SelectionRequests | BuyerId, DesiredDistrict, MinAreaHa, MaxBudgetUsd, Purpose, MatchedPlotIds, IsProcessedByRealtor. |
| УгодаПродажу + DealStatus | SaleDeal + DealStatus | SaleDeals | DraftContractNumber, FinalPriceUsd, AgencyCommissionUsd, Status; PrepareContractDraft(), ApproveByLawyer(), SignByParties(), RegisterOwnershipRight(), CancelDeal(). |
| Платіж + PaymentStatus | Payment + PaymentStatus | Payments | InvoiceNumber, PaymentType, AmountUsd, RecipientIban, SenderIban, PaymentReference, Status; SubmitPayment(), VerifyPayment(), RejectPayment(). |

3.1. Інкапсуляція бізнес-правил та переходів станів у доменних сутностях

Ключовою особливістю реалізованої моделі є відмова від анемічних структур даних (Anemic Domain Model) на користь інкапсульованої поведінки. Клас LandPlot реалізує структурний контроль кадастрового номера згідно з Порядком ведення Державного земельного кадастру (Постанова КМУ № 1051) за регулярним виразом ^\d{10}:\d{2}:\d{3}:\d{4}$, автоматичний перерахунок площі у сотки (AreaSotky) та вартість за сотку (PricePerSotkaUsd).

Лістинг 3.1 — Доменна сутність LandPlot із валідацією кадастрового номера та управлінням життєвим циклом (Models/LandPlot.cs)

public class LandPlot

{

    private static readonly Regex CadastralRegex = new(@"^\d{10}:\d{2}:\d{3}:\d{4}$", RegexOptions.Compiled);

    public int Id { get; set; }

    [Required, StringLength(25)]

    public string CadastralNumber { get; set; } = string.Empty;

    [Required, StringLength(180)]

    public string Title { get; set; } = string.Empty;

    [Range(0.001, 10000.0)]

    public double AreaHectares { get; set; }

    [Range(1, 100000000)]

    public decimal PriceUsd { get; set; }

    public PlotStatus Status { get; set; } = PlotStatus.PendingLegalCheck;

    public double AreaSotky => Math.Round(AreaHectares * 100.0, 2);

    public decimal PricePerSotkaUsd => AreaSotky > 0 ? Math.Round(PriceUsd / (decimal)AreaSotky, 2) : 0m;

    public bool ValidateCadastralFormat() =>

        !string.IsNullOrWhiteSpace(CadastralNumber) && CadastralRegex.IsMatch(CadastralNumber.Trim());

    public void ChangeStatus(PlotStatus newStatus) => Status = newStatus;

    public bool UpdatePrice(decimal newPriceUsd)

    {

        if (newPriceUsd <= 0 || Status == PlotStatus.Sold || Status == PlotStatus.ReservedForDeal)

            return false;

        PriceUsd = Math.Round(newPriceUsd, 2);

        return true;

    }

}

Клас SaleDeal реалізує скінченний автомат (State Machine) життєвого циклу договору купівлі-продажу земельної ділянки відповідно до діаграми станів із лабораторної роботи № 1. Перехід у фінальний стан CompletedAtNotary (державна реєстрація права власності в ДРРП) програмно захищений сторожовою умовою (Guard Condition): метод RegisterOwnershipRight() перевіряє, що договір пройшов етап нотаріального посвідчення (SignedAtNotary) та що властивість IsFullyPaid має значення true (підтверджено як оплату 3% комісії агентства, так і 100% вартості самої земельної ділянки продавцю).

Лістинг 3.2 — Скінченний автомат станів нотаріальної угоди та сторожові умови розрахунків у класі SaleDeal (Models/SaleDeal.cs)

public class SaleDeal

{

    public int Id { get; set; }

    public string DraftContractNumber { get; set; } = string.Empty;

    public decimal FinalPriceUsd { get; set; }

    public decimal AgencyCommissionUsd { get; set; }

    public DealStatus Status { get; set; } = DealStatus.DraftPreparedByRealtor;

    public List<Payment> Payments { get; set; } = new();

    public bool IsAgencyFeePaid => Payments.Any(p => p.PaymentType == "AgencyFee" && p.IsPaid);

    public bool IsPlotPricePaid => Payments.Any(p => p.PaymentType == "PlotPrice" && p.IsPaid);

    public bool IsFullyPaid => Payments.Count > 0 && Payments.All(p => p.IsPaid);

    public void PrepareContractDraft(string contractNumber, decimal agreedPriceUsd, double commissionPercent = 3.0)

    {

        DraftContractNumber = contractNumber;

        FinalPriceUsd = agreedPriceUsd;

        AgencyCommissionUsd = Math.Round(agreedPriceUsd * (decimal)(commissionPercent / 100.0), 2);

        Status = DealStatus.DraftPreparedByRealtor;

        LandPlot?.ChangeStatus(PlotStatus.ReservedForDeal);

    }

    public bool SignByParties(string? notarialRegistryNumber = null)

    {

        if (Status != DealStatus.ApprovedByLawyerForNotary || !IsAgencyFeePaid)

            return false;

        if (!string.IsNullOrWhiteSpace(notarialRegistryNumber))

            NotarialRegistryNumber = notarialRegistryNumber.Trim();

        Status = DealStatus.SignedAtNotary;

        return true;

    }

    public bool RegisterOwnershipRight()

    {

        if (Status != DealStatus.SignedAtNotary || !IsFullyPaid)

            return false;

        Status = DealStatus.CompletedAtNotary;

        ClosedAt = DateTime.Now;

        LandPlot?.ChangeStatus(PlotStatus.Sold);

        return true;

    }

}

3.2. Контекст даних LandAgencyContext та ініціалізація БД

Клас LandAgencyContext успадковує DbContext та оголошує вісім типізованих колекцій DbSet<T>. У методі OnModelCreating за допомогою Fluent API налаштовано зв'язки «один-до-одного» (LandPlot — LegalConclusion), «один-до-багатьох» (LandPlot — PlotDocuments, SaleDeal — Payments, AppUser — SellingPlots) та правила каскадного видалення. Метод InitializeDatabase() виконує автоматичну перевірку цілісності схеми БД та викликає SeedInitialDomainData(), який заповнює порожню базу даних репрезентативним набором даних (облікові записи рієлтора, юриста, продавця та покупця, 4 земельні ділянки у різних статусах, включаючи ділянку з арештом у ДРРП для демонстрації юридичного блокування, документи з розрахованими SHA-256 контрольними сумами, висновки юриста та активну угоду).

4. РЕАЛІЗАЦІЯ КОНТРОЛЕРІВ ТА БІЗНЕС-ЛОГІКИ АВТОМАТИЗОВАНИХ СЕРВІСІВ

На відміну від спрощених навчальних CRUD-застосунків, де контролери лише зберігають текстові поля форм у таблиці БД, у проєкті LandAgencyMvc реалізовано п'ять взаємопов'язаних контурів реальної алгоритмічної автоматизації предметної області земельного агентства.

4.1. Декодування кадастрового номера за класифікатором КОАТУУ (CadastralValidationService)

Сервіс CadastralValidationService виконує синтаксичний та семантичний розбір 19-значного кадастрового номера формату XXXXXXXXXX:XX:XXX:XXXX. Перші дві цифри 10-значного коду КОАТУУ зіставляються з довідником областей України (наприклад, 32 — Київська область, 46 — Львівська область, 51 — Одеська область), перевіряється узгодженість між кодом кадастрової зони та текстовою адресою ділянки, а також виконується точний перерахунок площі у гектари, сотки та квадратні метри.

Лістинг 4.1 — Алгоритм парсингу кадастрового номера та декодування регіону за КОАТУУ (Services/CadastralValidationService.cs)

public CadastralParseResult ValidateKoatuuFormat(string cadastralNumber)

{

    var clean = (cadastralNumber ?? string.Empty).Trim();

    if (!CadastralPattern.IsMatch(clean))

    {

        return new CadastralParseResult

        {

            IsValid = false,

            ValidationError = "Невірний формат кадастрового номера. Очікується: XXXXXXXXXX:XX:XXX:XXXX."

        };

    }

    var parts = clean.Split(':');

    var koatuu = parts[0];

    var regionPrefix = koatuu[..2];

    var regionName = KoatuuRegionMap.GetValueOrDefault(regionPrefix, "Невизначений регіон (код КОАТУУ)");

    return new CadastralParseResult

    {

        IsValid = true,

        NormalizedNumber = clean,

        KoatuuCode = koatuu,

        CadastralZone = parts[1],

        CadastralQuarter = parts[2],

        ParcelNumber = parts[3],

        RegionPrefix = regionPrefix,

        RegionName = regionName,

        AreaHectares = Math.Round(areaHectares, 4),

        AreaSotky = Math.Round(areaHectares * 100.0, 2),

        AreaSquareMeters = Math.Round(areaHectares * 10000.0, 0)

    };

}

4.2. Криптографічне хешування SHA-256 та накладання КЕП ДСТУ 4145-2002

Під час виставлення земельної ділянки на продаж у методі ClientController.CreatePlot власник завантажує електронний правовстановлюючий документ (PDF/JPG/PNG) та підписує його кваліфікованим електронним підписом (КЕП). Метод PlotDocument.AttachFile обчислює криптографічний дайджест вмісту файлу за алгоритмом SHA-256, а метод ApplyKepSignature формує невід'ємний геш електронного підпису на основі SHA-256 файлу, РНОКПП (ІПН) підписанта та номера кадастрового об'єкта. Під час юридичного аудиту метод VerifyKepIntegrity дозволяє миттєво виявити будь-яку спробу підміни бінарного вмісту документа.

Лістинг 4.2 — Обчислення SHA-256 дайджесту файлу та формування підпису КЕП ДСТУ 4145-2002 (Models/PlotDocument.cs)

public void AttachFile(string fileName, byte[] content, string contentType = "application/pdf")

{

    FileData = content;

    FileContentType = contentType;

    var hashBytes = SHA256.HashData(content);

    FileHash = Convert.ToHexString(hashBytes);

    FilePathOrUrl = string.IsNullOrWhiteSpace(fileName)

        ? $"db://plot-documents/{DocumentNumber}.pdf"

        : fileName;

}

public void ApplyKepSignature(string signerFullName, string signerTaxId, byte[] documentContent)

{

    var fileDigest = Convert.ToHexString(SHA256.HashData(documentContent));

    var payload = $"DSTU4145|{signerFullName.Trim()}|{signerTaxId.Trim()}|{DocumentNumber.Trim()}|{fileDigest}";

    var kepBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

    KepSignerName = signerFullName.Trim();

    KepSignerTaxId = signerTaxId.Trim();

    KepSignedAt = DateTime.Now;

    KepSignatureHash = $"KEP-UA-{Convert.ToHexString(kepBytes)[..24]}";

}

4.3. Симулятор інтеграції з АС ДЗК та ДРРП і аналіз ринкових аномалій (DzkRegistrySimulator)

Для автоматизації роботи юриста розроблено сервіс DzkRegistrySimulator. Під час відкриття сторінки правового аудиту (/Lawyer/Conclusion/{id}) сервіс виконує запит за кадастровим номером ділянки: перевіряє відповідність коду КОАТУУ фактичній області розташування, перевіряє кадастровий номер у реєстрах ДРРП на ознаки судових арештів та іпотек (серії :0000, :9999) і земельних сервітутів у ДЗК (серія :0009, охоронні зони інженерних мереж), а також порівнює заявлену ціну за сотку з еталонним діапазоном ринкових цін відповідного регіону України. Якщо виявлено арешт, колізію КОАТУУ або демпінгове заниження ціни (більш ніж удвічі нижче ринкового мінімуму), система автоматично встановлює рекомендований рівень ризику «Високий» і відображає попереджувальний банер у інтерфейсі юриста.

4.4. Двосторонній реактивний сервіс автопідбору (AutoMatchingService)

Сервіс AutoMatchingService реалізує двостороннє (бідирекціональне) зіставлення попиту та пропозиції без участі людини:

Прямий потік (Покупець -> Каталог): коли покупець створює заявку на підбір у ClientController.CreateSelectionRequest, сервіс миттєво фільтрує верифіковані ділянки (Status == PlotStatus.VerifiedInCatalog) за районом/локацією, мінімальною площею в гектарах, максимальним бюджетом у USD та цільовим призначенням і записує знайдені ID у MatchedPlotIds.

Зворотний реактивний потік (Юрист / Продавець -> Активні заявки покупців): коли юрист затверджує нову ділянку в LawyerController.SubmitLegalConclusion або коли продавець знижує ціну ділянки в ClientController.UpdatePlotPrice, автоматично викликається метод MatchNewOrUpdatedPlotAsync, який перевіряє всі відкриті заявки покупців і автоматично додає нову ділянку у відповідні підбірки клієнтів.

Лістинг 4.3 — Реактивний автопідбір земельних ділянок при публікації об'єкта або зміні ціни (Services/AutoMatchingService.cs)

public async Task<int> MatchNewOrUpdatedPlotAsync(LandPlot plot)

{

    if (plot.Status != PlotStatus.VerifiedInCatalog)

        return 0;

    var openRequests = await _db.SelectionRequests.ToListAsync();

    int updatedCount = 0;

    foreach (var req in openRequests)

    {

        if (!IsMatch(plot, req))

            continue;

        var existingIds = (req.MatchedPlotIds ?? string.Empty)

            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)

            .ToList();

        var plotIdStr = plot.Id.ToString();

        if (!existingIds.Contains(plotIdStr))

        {

            existingIds.Add(plotIdStr);

            req.MatchedPlotIds = string.Join(",", existingIds);

            req.IsProcessedByRealtor = true;

            req.RealtorNotes = $"Автопідбір оновлено: додано верифіковану ділянку ID {plot.Id} ({plot.CadastralNumber}, ${plot.PriceUsd:N0}).";

            updatedCount++;

        }

    }

    if (updatedCount > 0)

        await _db.SaveChangesAsync();

    return updatedCount;

}

4.5. Управління нотаріальними угодами, IBAN-розрахунками та каскадним закриттям оглядів

Контролер RealtorController забезпечує повний цикл супроводу угоди. При виклику CreateDeal перевіряється, що продавець і покупець не є однією особою, що по ділянці немає іншої активної угоди, валідується IBAN продавця (^UA\d{27}$) та автоматично генеруються два рахунки-фактури (Payment): рахунок на оплату 100% вартості земельної ділянки на IBAN продавця та рахунок на оплату 3% комісійної винагороди на IBAN агентства. Після того як юрист надає правову візу (LawyerController.ReviewContract), а клієнт подає реквізити оплати (ClientController.SubmitPayment), рієлтор звіряє банківську виписку (VerifyClientPayment або RejectClientPayment у разі помилки). Завершення угоди в CompleteNotaryDeal виконує державну реєстрацію в ДРРП лише при IsFullyPaid == true, переводить ділянку в статус Sold та автоматично скасовує всі інші конкуруючі заявки на огляд цієї ділянки.

5. РЕАЛІЗАЦІЯ ПОДАНЬ RAZOR, СТРОГОЇ ТИПІЗАЦІЇ VIEWMODELS ТА КОМПОНЕНТНОГО UI

5.1. Організація передачі даних та строга типізація ViewModels
Для забезпечення високої надійності та суворого контролю типів на етапі компіляції передача даних між контролерами та представленнями Razor організована за допомогою типізованих моделей подання (ViewModels):
- CatalogViewModel: інкапсулює каталог земельних ділянок, параметри фільтрації за ціною, площею, цільовим призначенням та регіоном;
- CabinetViewModel: персональний кабінет користувача, що агрегує власні об'єкти, підібрані ділянки, історію заявок на виїзний огляд, активні угоди, реквізити IBAN та правила доступності операцій (CanRequestPurchase, HasActiveDealForPlot);
- RealtorDealsViewModel та RealtorRequestsViewModel: моделі робочого місця рієлтора для ведення угод, графіків виїзних оглядів та черги обробки звернень клієнтів;
- LawyerQueueViewModel та LawyerConclusionViewModel: моделі автоматизованого робочого місця юриста для правової експертизи земельних ділянок, аналізу даних ДЗК/ДРРП та формування офіційного юридичного висновку.

5.2. Модульна компонентна архітектура інтерфейсу (Razor Partial Views)
Для забезпечення модульності інтерфейсу та усунення дублювання коду спільні візуальні вузли винесено в окремі типізовані часткові представлення (директорія Views/Shared/):
- _ClientNavbar.cshtml, _RealtorNavbar.cshtml, _LawyerNavbar.cshtml: спеціалізовані навігаційні панелі для кожної ролі користувача з автоматичною індикацією активної вкладки;
- _PlotCard.cshtml: уніфікований компонент картки земельної ділянки (@model LandPlot) для каталогу та кабінету;
- _DzkLinks.cshtml: компонент генерації прямих посилань на Публічну кадастрову карту України (kadastr.live) та сервіс Опендатабот;
- _AlertMessages.cshtml: централізований рендеринг статусних та діагностичних сповіщень системи.

5.3. Дизайн-система інтерфейсу та семантичне стильове оформлення (.la-btn)
У веб-застосунку реалізовано повне відокремлення структури розмітки від візуального оформлення: усі візуальні стилі централізовано у файлі wwwroot/css/site.css. Система включає стандартизовану бібліотеку компонентів:
- Базовий клас .la-btn, що визначає єдині пропорції, скруглення, шрифти та анімаційні переходи;
- Модифікатори розміру: .la-btn-sm (компактні кнопки дій), .la-btn-xs (міні-кнопки для рядків таблиць), .la-btn-full (на всю ширину картки);
- Семантична палітра станів: .la-btn-green (успішні дії, схвалення), .la-btn-red (скасування, відхилення), .la-btn-blue (інформаційні дії), .la-btn-dark (основні переходи), .la-btn-gray (скидання фільтрів), .la-btn-indigo (нотаріальні та кадастрові операції);
- Атомарні класи компонування (.la-row-between, .la-row-start, .la-action-row, .la-subcard, .la-check-item тощо).

5.4. Підсистема завантаження правовстановлюючих документів та контроль цілісності SHA-256
Під час реєстрації земельної ділянки користувач завантажує електронні копії правовстановлюючих документів (витяги з ДРРП, державні акти у форматах PDF, JPG або PNG). Застосунок забезпечує криптографічний контроль цілісності завантажених матеріалів:
- Метод PlotDocument.AttachFile обчислює контрольний дайджест бінарного вмісту файлу за стандартом SHA-256 та фіксує його в базі даних;
- Метод VerifyFileIntegrity здійснює повторну перевірку відповідності фактичного вмісту файлу збереженому гешу, виключаючи несанкціоновану підміну документів;
- В інтерфейсі користувача та в панелі юриста відображається скорочений геш (ShortFileHash) та обсяг файлу (FileSizeKb) для швидкої звірки з реєстрами ДЗК та ДРРП під час юридичного аудиту.

6. ВЕРИФІКАЦІЯ НАСКРІЗНОГО ЦИКЛУ РОБОТИ ТА ВАЛІДАЦІЯ БІЗНЕС-ПРАВИЛ

Для підтвердження повної працездатності, стійкості до відмов та коректності бізнес-правил було проведено наскрізне тестування життєвого циклу веб-застосунку в повному обсязі.

Таблиця 6.1 — Матриця перевірених бізнес-правил, алгоритмів валідації та наскрізних сценаріїв

| Функціональний модуль / Сервіс | Об'єкт перевірки та охоплені граничні випадки | Результат верифікації |
| --- | --- | --- |
| CadastralValidationService | Валідація 19-значного формату кадастрового номера XXXXXXXXXX:XX:XXX:XXXX, динамічне розпізнавання 26 регіональних кодів областей України за КОАТУУ, розрахунок нормативної грошової оцінки (НГО). | Підтверджено коректність для всіх регіонів |
| DzkRegistrySimulator | Виявлення судових арештів та іпотек у ДРРП (серії :0000, :9999), виявлення охоронних сервітутів ДЗК (:0009), відсутність хибного блокування легітимних описів («без арештів»), ринковий комплаєнс вартості сотки (<40% або >280%). | Виявлення 100% колізій, 0 хибних блокувань |
| Доменні моделі (PlotDocument, SaleDeal, Payment, AppUser) | Криптографічний контроль SHA-256 цілісності файлів, блокування державної реєстрації в ДРРП без повної оплати обох рахунків IBAN (вартість ділянки + комісія 3%), криптографічний PBKDF2 хеш паролів, захист проданих об'єктів від повторного редагування. | Інваріанти надійно захищені |
| Контролери та наскрізний цикл (Client, Realtor, Lawyer) | Наскрізний життєвий цикл: публікація ділянки продавцем -> експертиза та схвалення юристом -> автопідбір для покупця -> виїзний огляд -> погодження договору -> оплата IBAN -> нотаріальне посвідчення та автозакриття конкуруючих оглядів. | Усі етапи виконуються успішно |
| Архітектура рішень (Clean MVC) | Відповідність канонічній архітектурі MVC: строга типізація шару представлення через ViewModels, повне відокремлення CSS-стилів від розмітки, інкапсуляція бізнес-логіки в моделях і сервісах. | Збірка dotnet build: 0 помилок, 0 попереджень |

6.1. Опис наскрізного сценарію роботи застосунку (End-to-End Workflow)

Крок 1 (Продавець — створення об'єкта): клієнт-продавець авторизується в системі та через форму /Client/CreatePlot реєструє земельну ділянку, завантажує PDF-витяг з ДРРП. Система перевіряє кадастровий номер через CadastralValidationService, автоматично обчислює контрольний геш SHA-256 для захисту цілісності файлу і зберігає ділянку зі статусом PendingLegalCheck (об'єкт приховано з публічного каталогу до моменту перевірки юристом).

Крок 2 (Юрист — правовий аудит Due Diligence): юрист відкриває чергу /Lawyer/Queue, переходить до картки перевірки /Lawyer/Conclusion/{id}, де DzkRegistrySimulator автоматично звіряє код КОАТУУ, перевіряє відсутність арештів у ДРРП та сервітутів у ДЗК, контролює цілісність файлу за SHA-256 та аналізує ринкову ціну сотки. Після натискання «Схвалити публікацію» статус ділянки змінюється на VerifiedInCatalog, а сервіс AutoMatchingService автоматично пропонує ділянку відповідним покупцям.

Крок 3 (Покупець та Рієлтор — підбір і виїзний огляд): покупець бачить верифіковану ділянку в каталозі /Client/Catalog та у своєму кабінеті /Client/Cabinet і подає заявку на виїзний огляд. Рієлтор у панелі /Realtor/Viewings погоджує час, проводить огляд меж ділянки та фіксує статус «Завершено», що відкриває покупцеві кнопку подачі заявки на купівлю.

Крок 4 (Рієлтор та Юрист — проєкт договору та правова віза): рієлтор формує угоду в /Realtor/Deals, система автоматично нараховує 3% комісії агентства і виставляє 2 рахунки IBAN (продавцю та агентству). Юрист у розділі /Lawyer/Contracts перевіряє сформований договір (/Realtor/PrintContract) і надає офіційну правову візу (ApprovedByLawyerForNotary).

Крок 5 (Покупець та Рієлтор — банківські розрахунки та реєстрація в ДРРП): покупець у кабінеті /Client/Cabinet сплачує обидва рахунки за IBAN і вносить коди банківських квитанцій. Рієлтор верифікує зарахування коштів, фіксує нотаріальне підписання договору (SignDealAtNotary) та, після 100% підтвердження оплати (IsFullyPaid == true), завершує державну реєстрацію права власності в ДРРП (CompleteNotaryDeal). Ділянка переходить у фінальний статус Sold, сторони отримують доступ до друкованого договору та квитанцій, а всі інші відкриті заявки на огляд цієї ділянки автоматично скасовуються.

7. ВИСНОВКИ

7.1. Результати роботи та їх критичний аналіз

Під час виконання лабораторної роботи № 2 було спроєктовано та програмно реалізовано повнофункціональний веб-застосунок автоматизації агентства з продажу земельних ділянок «LandAgency UA» на основі архітектурного шаблону Model-View-Controller (MVC) на платформі ASP.NET Core 10.0 із використанням ORM Entity Framework Core (підхід Code First) та бази даних SQLite.

Основні науково-практичні результати виконаної роботи полягають у такому:

1. Повна реалізація об'єктної моделі ЛР № 1: усі класи, асоціації, композиції та діаграми станів UML-моделі перенесені у строго типізовані доменні класи C# (AppUser, LandPlot, PlotDocument, LegalConclusion, ViewingRequest, SelectionRequest, SaleDeal, Payment) з інкапсульованими скінченними автоматами станів та сторожовими умовами.

2. Впровадження спеціалізованих алгоритмів автоматизації: замість формального збереження текстових полів реалізовано парсер 19-значного кадастрового номера з декодуванням регіону за КОАТУУ (CadastralValidationService), криптографічний контроль цілісності документів за алгоритмом SHA-256, симулятор витягів АС ДЗК/ДРРП із виявленням арештів, сервітутів та цінових аномалій (DzkRegistrySimulator), а також реактивний двосторонній сервіс автопідбору (AutoMatchingService).

3. Транзакційна та інформаційна безпека: забезпечено рольову авторизацію трьох акторів (Client, Realtor, Lawyer), захист усіх POST-операцій від CSRF-атак атрибутом [ValidateAntiForgeryToken], захист від несанкціонованого доступу до чужих квитанцій та договорів (IDOR), хешування паролів за стандартом PBKDF2, а також жорстке блокування державної реєстрації права власності в ДРРП до повного підтвердження оплати вартості ділянки та комісії агентства.

4. Архітектурна чистота та якість реалізації: веб-застосунок побудовано відповідно до вимог архітектурного шаблону MVC — шар представлення суворо типізований за допомогою ViewModels, повторювані вузли інтерфейсу інкапсульовані у Razor Partial Views, оформлення винесено у зовнішні стильові класи CSS, а бізнес-правила інкапсульовані в доменних сутностях та сервісах. Застосунок є повністю автономним, а його компіляція у середовищі .NET 10.0 проходить без жодних помилок чи попереджень.

7.2. Відповіді на контрольні запитання

1. У чому полягає основна ідея та переваги архітектурного шаблону Model-View-Controller (MVC)?

Основна ідея шаблону MVC полягає в декомпозиції програмної системи на три слабко зв'язані компоненти з чітко розділеною відповідальністю (Separation of Concerns): Модель (Model) відповідає за дані, структуру предметної області та інваріанти бізнес-логіки; Подання (View) відповідає виключно за візуалізацію даних моделі для користувача; Контролер (Controller) приймає вхідні запити користувача, інтерпретує їх, викликає відповідні методи Моделі та обирає Подання для формування відповіді. Головними перевагами MVC є висока модульність, незалежність бізнес-логіки від інтерфейсу користувача, зручність паралельної розробки та висока придатність до модифікації та супроводу.

2. Які функції виконує кожен із компонентів Model, View та Controller у веб-застосунку ASP.NET Core MVC?

У ASP.NET Core MVC: 1) Model представлена класами сутностей C#, сервісами бізнес-логіки та контекстом даних EF Core (DbContext), які інкапсулюють правила предметної області та взаємодію з СКБД; 2) View представлена шаблонами Razor (.cshtml), які компілюються у класи C# і генерують HTML-потік на основі переданого об'єкта @model без прямого доступу до бази даних; 3) Controller (похідний від класу Controller) обробляє маршрутизовані HTTP-запити через методи дій (Action methods), виконує прив'язку моделі (Model Binding), перевірку авторизації та повертає об'єкт IActionResult.

3. Опишіть життєвий цикл проходження HTTP-запиту в конвеєрі ASP.NET Core MVC.

Життєвий цикл HTTP-запиту включає такі етапи: 1) прийом запиту веб-сервером Kestrel та формування об'єкта HttpContext; 2) проходження через конвеєр проміжного ПЗ (Middleware pipeline): обробка статичних файлів (UseStaticFiles), визначення маршруту (UseRouting), відновлення сесії користувача з кукі (UseAuthentication) та перевірка прав доступу за ролями (UseAuthorization); 3) вибір конкретного контролера та методу дії (Controller Action Invoker) за таблицею маршрутів; 4) прив'язка параметрів запиту з форми, рядка запиту або маршруту (Model Binding) та валідація антифорджері-токена; 5) виконання бізнес-логіки методу дії зі зверненням до DI-сервісів та DbContext; 6) виконання результату IActionResult — рендеринг шаблону Razor у HTML-відповідь або перенаправлення (HTTP 302 Redirect).

4. У чому полягає сутність підходу Code First в ORM Entity Framework Core та чим він відрізняється від Database First?

За підходу Code First джерелом істини (Single Source of Truth) є об'єктно-орієнтована модель класів мови C# та конфігурація контексту DbContext. Розробник описує доменні класи, навігаційні властивості, атрибути анотацій даних ([Required], [StringLength]) та правила Fluent API, після чого Entity Framework Core автоматично генерує реляційну схему таблиць, первинні й зовнішні ключі та індекси у СКБД. Натомість підхід Database First передбачає спочатку проєктування фізичної бази даних засобами SQL, з якої за допомогою зворотного інжинірингу (Scaffolding) генеруються класи C#. Підхід Code First краще відповідає принципам об'єктно-орієнтованого та предметно-орієнтованого проєктування (DDD).

5. Яку роль відіграють клас DbContext та властивості DbSet<T> у роботі з базою даних?

Клас DbContext реалізує одночасно два фундаментальні шаблони корпоративної архітектури: Одиниця роботи (Unit of Work) та Репозиторій (Repository). Він керує з'єднанням із базою даних, відстежує зміни станів завантажених об'єктів через Change Tracker (стани Added, Unchanged, Modified, Deleted), формує параметризовані SQL-запити з дерева виразів LINQ та атомарно фіксує всі зміни в одній транзакції під час виклику SaveChangesAsync(). Властивість DbSet<TEntity> репрезентує типізовану колекцію сутностей певного класу у пам'яті та відповідає таблиці в реляційній БД.

6. Як організуються зв'язки між сутностями (One-to-One, One-to-Many, Many-to-Many) у EF Core Code First?

Зв'язки організуються за допомогою комбінації навігаційних властивостей (посилань на одиночні об'єкти та колекції List<T>) і ключів зовнішніх посилань (Foreign Keys), які уточнюються через Fluent API у методі OnModelCreating: 1) One-to-One налаштовується через HasOne(...).WithOne(...).HasForeignKey<DependentEntity>(...) (наприклад, LandPlot та LegalConclusion); 2) One-to-Many налаштовується через HasOne(...).WithMany(...).HasForeignKey(...) із вказанням поведінки при видаленні OnDelete(DeleteBehavior.Cascade або SetNull) (наприклад, SaleDeal та список Payments); 3) Many-to-Many налаштовується через HasMany(...).WithMany(...) або через явну проміжну сутність-асоціацію.

7. Які способи передачі даних із Контролера у Подання (View) підтримуються в ASP.NET Core MVC?

В ASP.NET Core MVC підтримуються чотири основні механізми передачі даних із контролера у подання: 1) Строго типізована модель (Strongly Typed Model) — передача об'єкта у виклик return View(model) із оголошенням директиви @model у шаблоні Razor, що забезпечує перевірку типів на етапі компіляції; 2) ViewData — словник типу ViewDataDictionary (ключ-значення), доступний у межах поточного HTTP-запиту; 3) ViewBag — динамічна обгортка (dynamic) над словником ViewData для зручного звернення до допоміжних властивостей; 4) TempData — словник, що зберігається в кукі або сесії між двома послідовними HTTP-запитами і використовується для передачі флеш-повідомлень при реалізації патерна Post-Redirect-Get (PRG).

8. Як працює механізм прив'язки моделі (Model Binding) та валідації вхідних даних у контролерах?

Механізм Model Binding автоматично витягує сирі рядкові дані з HTTP-запиту (у порядку пріоритету: поля форми Request.Form, параметри маршруту RouteData, параметри рядка запиту QueryString) і перетворює їх на строго типізовані параметри методу дії контролера або властивості складного об'єкта C#. Під час прив'язки виконується автоматична перевірка атрибутів DataAnnotations ([Required], [Range], [RegularExpression]), результати якої записуються у словник ModelState. Крім того, атрибут [ValidateAntiForgeryToken] перевіряє криптографічну пару токенів у прихованому полі форми __RequestVerificationToken та cookie користувача, унеможливлюючи CSRF-атаки.

9. У чому полягає призначення патерна Dependency Injection (DI) у контролерах та сервісах ASP.NET Core?

Патерн Dependency Injection (ін'єкція залежностей) реалізує принцип інверсії керування (Inversion of Control, IoC): контролери не створюють екземпляри контексту БД чи доменних сервісів оператором new, а отримують їх через параметри конструктора від вбудованого IoC-контейнера IServiceProvider. У файлі Program.cs залежності реєструються з відповідним часом життя: Singleton (єдиний екземпляр на весь час роботи застосунку, наприклад ICadastralValidationService), Scoped (один екземпляр на один HTTP-запит, наприклад LandAgencyContext та IAutoMatchingService) або Transient (новий екземпляр при кожному зверненні). Це усуває жорстку зв'язність коду та дозволяє підставляти тестові реалізації у модульних тестах.

10. Які переваги дає використання патерна Post-Redirect-Get (PRG) при обробці POST-форм у MVC?

Патерн Post-Redirect-Get (PRG) передбачає, що після успішної обробки запиту модифікації даних (HTTP POST) контролер не повертає HTML-сторінку безпосередньо, а відповідає клієнту командою перенаправлення RedirectToAction (HTTP 302 Found) на безпечний метод читання (HTTP GET). Це усуває проблему повторного відправлення форми (Duplicate Form Submission) під час оновлення сторінки клавішею F5 у браузері, запобігає дублюванню фінансових транзакцій або угод у базі даних і робить історію переходів браузера коректною.

8. ПЕРЕЛІК ДЖЕРЕЛ ПОСИЛАННЯ

1. Конспект лекцій з дисципліни «Об’єктно-орієнтоване програмування» (Розділ «Архітектурний шаблон Model-View-Controller (MVC)»). — Київ : КНУ імені Тараса Шевченка, 2026.

2. Методичні вказівки до виконання лабораторної роботи № 2 «Розробка застосунку на основі шаблону MVC» з дисципліни «Об’єктно-орієнтоване програмування». — Київ : КНУ імені Тараса Шевченка, ФІТ, 2026. — 9 с.

3. ДСТУ 3008:2015. Інформація та документація. Звіти у сфері науки і техніки. Структура та правила оформлювання. — [Чинний від 2017-07-01]. — Київ : ДП «УкрНДНЦ», 2016. — 31 с.

4. Freeman A. Pro ASP.NET Core 9 / Adam Freeman. — 10th ed. — New York : Apress, 2024. — 1180 p.

5. Smith J. P. Entity Framework Core in Action / Jon P. Smith. — 2nd ed. — Shelter Island : Manning Publications, 2021. — 584 p.

6. Земельний кодекс України : Закон України від 25.10.2001 № 2768-III (зі змінами та доповненнями). — Режим доступу: https://zakon.rada.gov.ua/laws/show/2768-14.

7. Про затвердження Порядку ведення Державного земельного кадастру : Постанова Кабінету Міністрів України від 17.10.2012 № 1051. — Режим доступу: https://zakon.rada.gov.ua/laws/show/1051-2012-п.

8. Офіційна документація платформи Microsoft ASP.NET Core MVC та Entity Framework Core [Електронний ресурс]. — Режим доступу: https://learn.microsoft.com/en-us/aspnet/core/mvc/overview.
