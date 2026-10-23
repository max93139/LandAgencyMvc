# Повний посібник із проєкту «Земельне Агентство» (LandAgencyMvc)

Цей документ містить вичерпний опис архітектури, структури файлів, бізнес-логіки та практичні інструкції з редагування коду вебзастосунку **LandAgencyMvc**.

---

## 1. Загальний огляд проєкту

**LandAgencyMvc** — це інформаційна система автоматизації діяльності земельного агентства нерухомості, побудована на платформі **ASP.NET Core MVC** (.NET 10 / C#) з використанням **Entity Framework Core** та локальної реляційної бази даних **SQLite**.

### Ключові можливості системи:
1. **Багаторольова модель доступу**:
   - **Клієнт (Client)**: реєстрація, додавання власних земельних ділянок на продаж, пошук та фільтрація в каталозі перевірених ділянок, створення заявок на підбір, запис на перегляди, проведення оплат за договором.
   - **Рієлтор (Realtor)**: обробка заявок на підбір ділянок, планування переглядів, створення чернеток договорів купівлі-продажу, розрахунок комісії (за замовчуванням 3%), взаємодія з нотаріусом, друк договору.
   - **Юрист (Lawyer)**: юридичний аудит ділянок (перевірка через симулятор ДЗК / ДРРП на арешти, сервітути, межі), формування висновків/актів ризиків, погодження проєктів договорів перед нотаріальним посвідченням.
2. **Інтегрований симулятор ДЗК та ДРРП (`DzkRegistrySimulator`)**:
   - Автоматична перевірка коректності кадастрового номера згідно зі стандартом України (`^\d{10}:\d{2}:\d{3}:\d{4}$`).
   - Симуляція витягу з Державного земельного кадастру (ДЗК) та Державного реєстру речових прав на нерухоме майно (ДРРП).
   - Автоматичний розрахунок нормативної грошової оцінку (НГО) за категоріями цільового призначення.
   - Виявлення штучних факторів ризику (арешти, іпотеки, сервітути, накладки меж).
3. **Фінансовий модуль та угоди (`SaleDeal`, `Payment`)**:
   - Повний життєвий цикл угоди: від чернетки рієлтора до погодження юристом, посвідчення нотаріусом та реєстрації права власності.
   - Автоматичне генерування платіжних доручень з реквізитами у форматі IBAN.
   - Генерація друкованих форм договорів та квитанцій про оплату.

---

## 2. Архітектура та принцип роботи (ASP.NET Core MVC)

Застосунок реалізовано за класичним архітектурним патерном **Model-View-Controller (MVC)**:

```
[ Браузер користувача (HTTP Request) ]
                 │
                 ▼
       [ Program.cs (Routing & Middleware) ]
                 │
                 ▼
      [ Controllers (Контролери) ]
       ├── AccountController (Аутентифікація)
       ├── ClientController  (Каталог, кабінет клієнта)
       ├── RealtorController (Заявки, перегляди, угоди)
       └── LawyerController  (Черга перевірки, договори)
                 │
                 ├──► Взаємодія з базою даних через [ LandAgencyContext ]
                 │    та моделі даних [ Models ] (Entity Framework Core)
                 │    └── Файл SQLite: landagency.db
                 │
                 ▼
      [ Views (.cshtml) (Представлення) ]
       └── Генерація HTML на стороні сервера з даними з моделей
                 │
                 ▼
[ Браузер користувача (Згенерований HTML / CSS / JS) ]
```

### Життєвий цикл запиту:
1. **Вхідний запит**: Користувач звертається за певною URL-адресою (наприклад, `/Client/Catalog` або `/Realtor/Requests`).
2. **Маршрутизація (`Program.cs`)**: Фреймворк ASP.NET Core зіставляє URL з відповідним Контролером та Action-методом.
3. **Ідентифікація користувача**: Контролери використовують криптографічно захищену аутентифікаційну сесію ASP.NET Core Cookie Authentication (`LandAgency.Auth`) та отримують `ClaimTypes.NameIdentifier` і `ClaimTypes.Role` із `HttpContext.User`.
4. **Обробка логіки (Controller)**:
   - Контролер звертається до `LandAgencyContext` та DI-сервісів (`ICadastralValidationService`, `IAutoMatchingService`).
   - Отримує або змінює дані в моделях (`LandPlot`, `SaleDeal`, `ViewingRequest` тощо).
   - Зберігає зміни через `await _db.SaveChangesAsync()`.
5. **Генерація відповіді (View)**:
   - Контролер передає підготовлені дані через суворо типізовану модель або ViewModel (без використання `ViewBag`) у файл представлення Razor (`.cshtml`).
   - Razor генерує фінальний HTML, який повертається браузеру клієнта.

---

## 3. Структура файлів та каталогів

```
LandAgencyMvc/
├── LandAgencyMvc.sln                 # Файл рішення для Visual Studio / Rider
├── LandAgencyMvc.Tests/              # Проєкт модульних тестів (xUnit, 52 тести)
└── LandAgencyMvc/                    # Основний проєкт застосунку
    ├── Program.cs                    # Точка входу, конфігурація DI, middleware, ініціалізація БД
    ├── LandAgencyMvc.csproj          # Файл конфігурації проєкту .NET та NuGet-пакетів
    ├── appsettings.json              # Загальні налаштування застосунку
    ├── appsettings.Development.json  # Налаштування для середовища розробки
    ├── landagency.db                 # Файл бази даних SQLite
    │
    ├── Models/                       # Доменні сутності, моделі БД та ViewModels
    │   ├── Enums.cs                  # Переліки: UserRole, PlotStatus, ViewingStatus, DealStatus, PaymentStatus
    │   ├── AppUser.cs                # Сутність користувача (Клієнт, Рієлтор, Юрист)
    │   ├── LandPlot.cs               # Земельна ділянка (кадастровий номер, площа, ціна, статус, URL-посилання, StandardPurposes)
    │   ├── PlotDocument.cs           # Документи ділянки (SHA-256 хеш, КЕП/ЕЦП, розмір)
    │   ├── SelectionRequest.cs       # Заявка клієнта на підбір земельної ділянки та доменні правила підбору
    │   ├── ViewingRequest.cs         # Заявка на виїзд та перегляд ділянки
    │   ├── LegalConclusion.cs        # Юридичний висновок / акт ризиків юриста
    │   ├── SaleDeal.cs               # Угода купівлі-продажу, статус, комісія, нотаріус
    │   ├── Payment.cs                # Платіж (вартість ділянки, комісія агентства, IBAN)
    │   ├── DzkRegistrySimulator.cs   # Симулятор перевірки в реєстрах ДЗК та ДРРП (включає DzkRegistryExtract)
    │   ├── LandAgencyContext.cs      # Контекст Entity Framework Core (DbContext)
    │   ├── CabinetViewModel.cs       # Типізовані ViewModels (CabinetViewModel, CatalogViewModel, RealtorDealsViewModel, LawyerQueueViewModel, LawyerConclusionViewModel)
    │   ├── RealtorRequestsViewModel.cs # Модель даних для сторінки заявок рієлтора
    │   └── ErrorViewModel.cs         # Модель для відображення системних помилок
    │
    ├── Services/                     # Прикладні сервіси бізнес-логіки (Dependency Injection)
    │   ├── AutoMatchingService.cs    # Автоматичний підбір ділянок під критерії клієнтів
    │   └── CadastralValidationService.cs # Перевірка КОАТУУ, НГО, ДЗК/ДРРП та цінових аномалій
    │
    ├── Controllers/                  # Контролери обробки HTTP-запитів
    │   ├── AccountController.cs      # Вхід (Login), реєстрація (Register), вихід (Logout)
    │   ├── ClientController.cs       # Каталог, кабінет, додавання ділянки, перегляди, заявка на купівлю, оплата
    │   ├── RealtorController.cs      # Заявки на підбір, перегляди, оформлення угод, нотаріус
    │   ├── LawyerController.cs       # Черга експертизи ділянок, висновки ДЗК, погодження угод
    │   └── HomeController.cs         # Головна сторінка, редиректи, обробка помилок
    │
    ├── Views/                        # Чисті Razor-представлення (суворо без бізнес-логіки, без ViewBag та без inline-стилів)
    │   ├── Account/                  # Сторінки авторизації та реєстрації (Login, Register, AccessDenied)
    │   ├── Client/                   # Інтерфейс клієнта (Catalog, Cabinet, CreatePlot, Details, Receipt)
    │   ├── Realtor/                  # Інтерфейс рієлтора (Requests, Viewings, Deals, PrintContract)
    │   ├── Lawyer/                   # Інтерфейс юриста (Queue, Conclusion, Contracts)
    │   ├── Shared/                   # Спільні компоненти (_Layout, _PlotCard, _DzkLinks, _AlertMessages, рольові навбари)
    │   ├── _ViewImports.cshtml       # Глобальні using-директиви для Razor
    │   └── _ViewStart.cshtml         # Налаштування базового шаблону за замовчуванням
    │
    └── wwwroot/                      # Статичні ресурси клієнтської частини
        ├── css/                      # Єдина таблиця стилів (site.css) з атомарними UI-класами
        ├── js/                       # Клієнтські JavaScript-скрипти
        └── lib/                      # Бібліотеки (Bootstrap, jQuery)
```

---

## 4. Детальний опис моделей даних (`Models/`)

### 1. `Enums.cs` — Ключові стани системи:
- **`UserRole`**:
  - `Client` — звичайний користувач (покупець або продавець).
  - `Realtor` — сертифікований рієлтор агентства.
  - `Lawyer` — юрист агентства із земельними правами.
- **`PlotStatus`**:
  - `PendingLegalCheck` — додано клієнтом, очікує перевірки юристом у реєстрах ДЗК/ДРРП.
  - `VerifiedInCatalog` — схвалено юристом, опубліковано у відкритому каталозі.
  - `RejectedByLawyer` — виявлено арешти/обтяження, сформовано акт ризиків, відхилено.
  - `Sold` — право власності переоформлено, ділянку продано.
- **`ViewingStatus`**:
  - `NewRequest` -> `Scheduled` -> `Completed` -> `PlotRejectedByBuyer` / `Cancelled`.
- **`DealStatus`**:
  - `DraftPreparedByRealtor` (Рієлтор сформував чернетку).
  - `ReturnedForRevision` (Юрист повернув договір на доопрацювання).
  - `ApprovedByLawyerForNotary` (Юрист погодив договір до нотаріуса).
  - `SignedAtNotary` (Посвідчено нотаріусом).
  - `CompletedAtNotary` (Договір зареєстровано в ДРРП, угоду закрито).
  - `Cancelled` (Угоду скасовано).

### 2. `LandPlot.cs` — Земельна ділянка:
Містить кадастровий номер (`CadastralNumber`), назву, локацію, площу в гектарах (`AreaHectares`), цільове призначення (`Purpose`), ціну в USD (`PriceUsd`), номер документа про право власності, банківський IBAN продавця (`SellerBankIban`) та прив'язку до продавця (`SellerId`).
- Обчислювані властивості (`[NotMapped]`): `AreaSotky`, `PricePerSotkaUsd`, `KadastrLiveUrl`, `OpenDataBotUrl`, `EffectiveSellerIban`, `PrimaryDocument`, `StatusDisplayName`, `StatusBadgeClass`.
- Статичний довідник `StandardPurposes` — централізований перелік цільових призначень за УКЦВЗ (`Code`, `FullValue`, `ShortLabel`) для всіх форм і фільтрів.
- Метод `ValidateCadastralFormat()` перевіряє регулярним виразом формат кадастрового номера: `^\d{10}:\d{2}:\d{3}:\d{4}$`.

### 3. `SaleDeal.cs` та `Payment.cs` — Угода та розрахунки:
- Фіксує ціну (`FinalPriceUsd`), комісію агентства (`AgencyCommissionUsd`), нотаріальну контору, дату посвідчення, номер у реєстрі нотаріуса.
- Метод `CalculateCommission(double ratePercent = 3.0)` автоматично обраховує 3% комісійних.
- Метод `CreateDefaultPayments(...)` створює 2 платіжні записи:
  1. Оплата вартості ділянки безпосередньо продавцю на його IBAN.
  2. Оплата винагороди агентства на офіційний IBAN агентства.

### 4. `DzkRegistrySimulator.cs` — Симулятор державних реєстрів:
Аналізує кадастровий номер та параметри ділянки за офіційними кадастровими правилами:
- Якщо кадастровий номер закінчується на `:0000` або `:9999` — симулюється виявлення обтяження чи судового арешту в ДРРП з високим рівнем ризику.
- Якщо закінчується на `:0009` — симулюється наявність земельного сервітуту або охоронної зони інженерних мереж у ДЗК.
- Перевіряє валідність структури КОАТУУ та динамічно визначає область України за першими двома цифрами коду.
- Розраховує нормативну грошову оцінку (НГО) за ставками залежно від коду призначення (`02.*` — будівництво, `03.*` — комерція тощо).

---

## 5. Контролери та їхня відповідальність (`Controllers/`)

| Контролер | Основні дії (Actions) | Призначення |
| :--- | :--- | :--- |
| **`AccountController`** | `Login`, `Register`, `Logout`, `AccessDenied` | Безпечна автентифікація на базі ASP.NET Core Cookie Authentication (`LandAgency.Auth`) та криптографічного хешування паролів PBKDF2 (`PasswordHasher<AppUser>`). Реєстрація клієнтів суворо призначає роль `Client`. |
| **`ClientController`** | `Catalog`, `Details`, `CreatePlot`, `Cabinet`, `CreateViewing`, `RequestPurchaseFromViewing`, `SubmitSelectionRequest`, `SubmitPayment`, `DownloadReceipt`, `ChangePassword` | Перегляд публічного каталогу, пошук та фільтрація, подача ділянки на продаж із КЕП та SHA-256 хешуванням документів, автоматичний підбір (`AutoMatchingService`), запис на огляд, подача заявки на купівлю після проведеного огляду, повідомлення про оплату за IBAN. |
| **`RealtorController`** | `Requests`, `MatchPlotsForRequest`, `Viewings`, `UpdateViewingStatus`, `Deals`, `CreateDeal`, `VerifyClientPayment`, `RejectClientPayment`, `SignDealAtNotary`, `CompleteNotaryDeal`, `CancelDeal`, `PrintContract` | Робота рієлтора: автоматизований підбір ділянок, узгодження виїзних оглядів, створення проєктів угод з виставленням рахунків-фактур, верифікація банківських виписок, супровід у нотаріуса та реєстрація в ДРРП. |
| **`LawyerController`** | `Queue`, `Conclusion`, `SubmitLegalConclusion`, `Contracts`, `ApproveDealContract` | Робота юриста: черга ділянок на аудит у ДЗК/ДРРП (`CadastralValidationService` + `DzkRegistrySimulator`), затвердження або відхилення ділянки з актом ризиків, правова віза договорів перед нотаріусом. |

---

## 6. Як працює база даних (`LandAgencyContext.cs`)

База даних створюється та наповнюється початковими демонстраційними даними автоматично при старті програми в методі `InitializeDatabase()`:
- `EnsureSchemaUpToDate()` перевіряє актуальність схеми таблиць.
- За замовчуванням створюються демонстраційні облікові записи та набір земельних ділянок, документів з SHA-256/КЕП, юридичних висновків, заявок та угод:
  - **Рієлтор**: `realtor@landagency.ua` / пароль: `realtor123`
  - **Юрист**: `lawyer@landagency.ua` / пароль: `lawyer123`
  - **Клієнт (Продавець)**: `seller@landagency.ua` / пароль: `client123`
  - **Клієнт (Покупець)**: `buyer@landagency.ua` / пароль: `client123`
- Файл бази даних зберігається локально: `LandAgencyMvc/landagency.db`.

---

## 7. Практичний посібник: Як редагувати цей код

### Сценарій А: Зміна бізнес-правила (наприклад, зміна ставки комісії)
1. Відкрийте файл [`SaleDeal.cs`](file:///Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc/Models/SaleDeal.cs).
2. Знайдіть метод `CalculateCommission(double ratePercent = 3.0)` та замініть значення за замовчуванням або алгоритм обрахунку.
3. Якщо потрібно змінити ставку за замовчуванням для рієлтора, відкрийте [`AppUser.cs`](file:///Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc/Models/AppUser.cs) та змініть значення поля `CommissionRate`.

### Сценарій Б: Додавання нового поля до сутності (наприклад, «Газифікація ділянки» у `LandPlot`)
1. **Зміна моделі**:
   - Відкрийте [`LandPlot.cs`](file:///Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc/Models/LandPlot.cs).
   - Додайте нову властивість:
     ```csharp
     public bool HasGasSupply { get; set; } = false;
     ```
2. **Оновлення контролера**:
   - Відкрийте [`ClientController.cs`](file:///Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc/Controllers/ClientController.cs).
   - У методі `CreatePlot(...)` збережіть нову властивість моделі.
3. **Оновлення форми у View**:
   - Відкрийте форму створення [`Views/Client/CreatePlot.cshtml`](file:///Users/mac/Desktop/projects/LandAgencyMvc/LandAgencyMvc/Views/Client/CreatePlot.cshtml) та додайте відповідне поле вводу.

---

## 8. Запуск та тестування проєкту

### Запуск через термінал:
```bash
cd /Users/mac/Desktop/projects/LandAgencyMvc

# Збірка та запуск модульних тестів (xUnit)
dotnet test

# Запуск веб-сервера
dotnet run --project LandAgencyMvc
```

### Тестові дані для швидкого входу:
- **Рієлтор**: email `realtor@landagency.ua`, пароль `realtor123`
- **Юрист**: email `lawyer@landagency.ua`, пароль `lawyer123`
- **Клієнт-Продавець**: email `seller@landagency.ua`, пароль `client123`
- **Клієнт-Покупець**: email `buyer@landagency.ua`, пароль `client123`

