# Повний посібник із проєкту «Земельне Агентство» (LandAgencyMvc)

Цей документ містить вичерпний опис архітектури, структури файлів, бізнес-логіки та практичні інструкції з редагування коду вебзастосунку **LandAgencyMvc**.

---

## 1. Загальний огляд проєкту

**LandAgencyMvc** — це інформаційна система автоматизації діяльності земельного агентства нерухомості, побудована на платформі **ASP.NET Core MVC** (.NET 9 / C#) з використанням **Entity Framework Core** та локальної реляційної бази даних **SQLite**.

### Ключові можливості системи:
1. **Багаторольова модель доступу**:
   - **Клієнт (Client)**: реєстрація, додавання власних земельних ділянок на продаж, пошук та фільтрація в каталозі перевірених ділянок, створення заявок на підбір, запис на перегляди, проведення оплат за договором.
   - **Рієлтор (Realtor)**: обробка заявок на підбір ділянок, планування переглядів, створення чернеток договорів купівлі-продажу, розрахунок комісії (за замовчуванням 3%), взаємодія з нотаріусом, друк договору.
   - **Юрист (Lawyer)**: юридичний аудит ділянок (перевірка через симулятор ДЗК / ДРРП на арешти, сервітути, межі), формування висновків/актів ризиків, погодження проєктів договорів перед нотаріальним посвідченням.
2. **Інтегрований симулятор ДЗК та ДРРП (`DzkRegistrySimulator`)**:
   - Автоматична перевірка коректності кадастрового номера згідно зі стандартом України (`^\d{10}:\d{2}:\d{3}:\d{4}$`).
   - Симуляція витягу з Державного земельного кадастру (ДЗК) та Державного реєстру речових прав на нерухоме майно (ДРРП).
   - Автоматичний розрахунок нормативної грошової оцінки (НГО) за категоріями цільового призначення.
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
3. **Ідентифікація користувача**: Контролери використовують ідентифікатор користувача (`userId`), переданий у параметрах запиту або збережений у безпечній Cookie `LandAgencyUserId`.
4. **Обробка логіки (Controller)**:
   - Контролер звертається до `LandAgencyContext` через LINQ-запити.
   - Отримує або змінює дані в моделях (`LandPlot`, `SaleDeal`, `ViewingRequest` тощо).
   - Зберігає зміни через `await _db.SaveChangesAsync()`.
5. **Генерація відповіді (View)**:
   - Контролер передає підготовлені дані (Model або ViewModel) у файл представлення Razor (`.cshtml`).
   - Razor генерує фінальний HTML, який повертається браузеру клієнта.

---

## 3. Структура файлів та каталогів

```
LandAgencyMvc/
├── LandAgencyMvc.sln                 # Файл рішення для Visual Studio / Rider
└── LandAgencyMvc/                    # Основний проєкт застосунку
    ├── Program.cs                    # Точка входу, конфігурація DI, middleware, ініціалізація БД
    ├── LandAgencyMvc.csproj          # Файл конфігурації проєкту .NET та NuGet-пакетів
    ├── appsettings.json              # Загальні налаштування застосунку
    ├── appsettings.Development.json  # Налаштування для середовища розробки
    ├── landagency.db                 # Файл бази даних SQLite
    │
    ├── Models/                       # Доменні сутності, моделі БД та бізнес-сервіси
    │   ├── Enums.cs                  # Переліки: UserRole, PlotStatus, ViewingStatus, DealStatus
    │   ├── AppUser.cs                # Сутність користувача (Клієнт, Рієлтор, Юрист)
    │   ├── LandPlot.cs               # Земельна ділянка (кадастровий номер, площа, ціна, статус)
    │   ├── PlotDocument.cs           # Документи, прикріплені до ділянки
    │   ├── SelectionRequest.cs       # Заявка клієнта на підбір земельної ділянки
    │   ├── ViewingRequest.cs         # Заявка на виїзд та перегляд ділянки
    │   ├── LegalConclusion.cs        # Юридичний висновок / акт ризиків юриста
    │   ├── SaleDeal.cs               # Угода купівлі-продажу, статус, комісія, нотаріус
    │   ├── Payment.cs                # Платіж (вартість ділянки, комісія агентства, IBAN)
    │   ├── DzkRegistrySimulator.cs   # Симулятор перевірки в реєстрах ДЗК та ДРРП
    │   ├── DzkRegistryExtract.cs     # Модель витягу з ДЗК/ДРРП
    │   ├── LandAgencyContext.cs      # Контекст Entity Framework Core (DbContext)
    │   ├── CabinetViewModel.cs       # Модель даних для відображення особистого кабінету клієнта
    │   ├── RealtorRequestsViewModel.cs # Модель даних для сторінки заявок рієлтора
    │   └── ErrorViewModel.cs         # Модель для відображення системних помилок
    │
    ├── Controllers/                  # Контролери обробки HTTP-запитів
    │   ├── AccountController.cs      # Вхід (Login), реєстрація (Register), вихід (Logout)
    │   ├── ClientController.cs       # Каталог, кабінет, додавання ділянки, перегляди, оплата
    │   ├── RealtorController.cs      # Заявки на підбір, перегляди, оформлення угод, нотаріус
    │   ├── LawyerController.cs       # Черга експертизи ділянок, висновки ДЗК, погодження угод
    │   └── HomeController.cs         # Головна сторінка, редиректи, обробка помилок
    │
    ├── Views/                        # Razor-представлення (HTML + C# шаблони)
    │   ├── Account/                  # Сторінки авторизації та реєстрації (Login, Register)
    │   ├── Client/                   # Інтерфейс клієнта (Catalog, Cabinet, CreatePlot, Details, Receipt)
    │   ├── Realtor/                  # Інтерфейс рієлтора (Requests, Viewings, Deals, PrintContract)
    │   ├── Lawyer/                   # Інтерфейс юриста (Queue, Conclusion, Contracts)
    │   ├── Home/                     # Загальні сторінки (Index)
    │   ├── Shared/                   # Спільні шаблони (_Layout.cshtml, Error.cshtml, тощо)
    │   ├── _ViewImports.cshtml       # Глобальні using-директиви для Razor
    │   └── _ViewStart.cshtml         # Налаштування базового шаблону за замовчуванням
    │
    └── wwwroot/                      # Статичні ресурси клієнтської частини
        ├── css/                      # Стилі (site.css, власні стилі системи)
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

### 2. `LandPlot.cs` — Земельна ділянка:
Містить кадастровий номер (`CadastralNumber`), назву, локацію, площу в гектарах (`AreaHectares`), цільове призначення (`Purpose`), ціну в USD (`PriceUsd`), номер документа про право власності, банківський IBAN продавця та прив'язку до продавця (`SellerId`).
- Метод `ValidateCadastralFormat()` перевіряє регулярним виразом формат кадастрового номера: `^\d{10}:\d{2}:\d{3}:\d{4}$`.

### 3. `SaleDeal.cs` та `Payment.cs` — Угода та розрахунки:
- Фіксує ціну (`FinalPriceUsd`), комісію агентства (`AgencyCommissionUsd`), нотаріальну контору, дату посвідчення, номер у реєстрі нотаріуса.
- Метод `CalculateCommission(double ratePercent = 3.0)` автоматично обраховує 3% комісійних.
- Метод `CreateDefaultPayments(...)` створює 2 платіжні записи:
  1. Оплата вартості ділянки безпосередньо продавцю на його IBAN.
  2. Оплата винагороди агентства на офіційний IBAN агентства.

### 4. `DzkRegistrySimulator.cs` — Симулятор державних реєстрів:
Аналізує кадастровий номер та текстовий опис:
- Якщо кадастровий номер закінчується на `:0000`, `:9999` або опис містить слова *«арешт»*, *«іпотек»*, *«спір»* — симулюється виявлення арешту з високим рівнем ризику.
- Якщо містить *«сервітут»* або закінчується на `:0009` — симулюється обмеження у користуванні.
- Розраховує нормативну грошову оцінку (НГО) за ставками залежно від коду призначення (`02.*` — будівництво, `03.*` — комерція тощо).

---

## 5. Контролери та їхня відповідальність (`Controllers/`)

| Контролер | Основні дії (Actions) | Призначення |
| :--- | :--- | :--- |
| **`AccountController`** | `Login`, `Register`, `Logout` | Авторизація за Email та паролем. Автоматично визначає роль: адреси, що містять `realtor@`, стають рієлторами; `lawyer@` — юристами; всі інші — клієнтами. Записує Cookie `LandAgencyUserId`. |
| **`ClientController`** | `Catalog`, `Details`, `CreatePlot`, `Cabinet`, `CreateViewing`, `ConfirmPayment`, `DownloadReceipt` | Перегляд публічного каталогу, пошук та фільтрація (за бюджетом, площею, призначенням), подача ділянки на продаж, створення заявок на підбір, запис на перегляд, проведення оплати за договором. |
| **`RealtorController`** | `Requests`, `MatchPlotsForRequest`, `Viewings`, `UpdateViewingStatus`, `Deals`, `CreateDeal`, `SignDealAtNotary`, `CompleteNotaryDeal`, `PrintContract` | Робота рієлтора: підбір ділянок під запити покупців, узгодження часу виїзних переглядів, створення проєкту угоди, друк договору та передача на підпис. |
| **`LawyerController`** | `Queue`, `Conclusion`, `SubmitLegalConclusion`, `Contracts`, `ApproveDealContract` | Робота юриста: черга ділянок на первинну перевірку ДЗК/ДРРП, затвердження або відхилення ділянки з актом ризиків, юридичне погодження договорів рієлтора перед походом до нотаріуса. |

---

## 6. Як працює база даних (`LandAgencyContext.cs`)

База даних створюється автоматично при старті програми в методі `InitializeDatabase()`:
- `EnsureSchemaUpToDate()` перевіряє цілісність полів (якщо схема змінилася, БД безпечно перестворюється).
- За замовчуванням створюються тестові облікові записи:
  - **Рієлтор**: `realtor@landagency.ua` / пароль: `realtor123`
  - **Юрист**: `lawyer@landagency.ua` / пароль: `lawyer123`
- Файл бази даних зберігається локально за адресою: `LandAgencyMvc/landagency.db`.

---

## 7. Практичний посібник: Як редагувати цей код

### Сценарій А: Зміна бізнес-правила (наприклад, зміна ставки комісії)
1. Відкрийте файл [`SaleDeal.cs`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Models/SaleDeal.cs).
2. Знайдіть метод `CalculateCommission(double ratePercent = 3.0)` та замініть значення за замовчуванням або алгоритм обрахунку.
3. Якщо потрібно змінити ставку за замовчуванням для рієлтора, відкрийте [`AppUser.cs`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Models/AppUser.cs) та змініть значення поля `CommissionRate`.

### Сценарій Б: Додавання нового поля до сутності (наприклад, «Газифікація ділянки» у `LandPlot`)
1. **Зміна моделі**:
   - Відкрийте [`LandPlot.cs`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Models/LandPlot.cs).
   - Додайте нову властивість:
     ```csharp
     public bool HasGasSupply { get; set; } = false;
     ```
2. **Оновлення контролера**:
   - Відкрийте [`ClientController.cs`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Controllers/ClientController.cs).
   - У методі `CreatePlot(...)` додайте вхідний параметр `bool hasGasSupply` і збережіть його:
     ```csharp
     new LandPlot { ... HasGasSupply = hasGasSupply };
     ```
3. **Оновлення форми у View**:
   - Відкрийте форму створення [`Views/Client/CreatePlot.cshtml`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Views/Client/CreatePlot.cshtml).
   - Додайте чекбокс або випадаючий список для нового поля:
     ```html
     <div class="form-check mb-3">
         <input class="form-check-input" type="checkbox" name="hasGasSupply" value="true" id="gasCheck" />
         <label class="form-check-label" for="gasCheck">Підведено газопостачання</label>
     </div>
     ```
4. **Оновлення відображення**:
   - Відкрийте [`Views/Client/Details.cshtml`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Views/Client/Details.cshtml) або картку в каталозі і виведіть статус:
     ```html
     <span>Газ: @(Model.HasGasSupply ? "Підведено ✓" : "Відсутній ✗")</span>
     ```
5. **Оновлення бази даних**:
   - Якщо ви використовуєте `EnsureDeleted()` / `EnsureCreated()` у `EnsureSchemaUpToDate()`, додайте перевірку нового поля або просто видаліть файл `landagency.db`, і при наступному запуску застосунок створить чисту таблицю з новим полем.

### Сценарій В: Додавання нового пункту меню або нової сторінки
1. **Створення Action у Контролері**:
   - Відкрийте потрібний контролер (наприклад, `ClientController.cs`).
   - Додайте метод:
     ```csharp
     [HttpGet]
     public async Task<IActionResult> MyNewPage(int userId)
     {
         await SetCurrentUserAsync(userId);
         return View();
     }
     ```
2. **Створення представлення (Razor View)**:
   - Створіть файл `Views/Client/MyNewPage.cshtml`.
   - Додайте необхідну розмітку та стилі.
3. **Посилання у навігації**:
   - Відкрийте шапку в `Views/Shared/_Layout.cshtml` або внутрішню навігацію у відповідному представленні та додайте посилання:
     ```html
     <a asp-controller="Client" asp-action="MyNewPage" asp-route-userId="@ViewBag.CurrentUserId">Нова сторінка</a>
     ```

### Сценарій Г: Зміна валідації кадастрового номера
- Відкрийте [`LandPlot.cs`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc/Models/LandPlot.cs#L83-L91).
- У методі `ValidateCadastralFormat()` можна налаштувати або пом'якшити регулярний вираз для перевірки формату:
  ```csharp
  return Regex.IsMatch(CadastralNumber.Trim(), @"^\d{10}:\d{2}:\d{3}:\d{4}$");
  ```

---

## 8. Запуск та налагодження проєкту

### Запуск через термінал:
```bash
# Перехід у папку з проєктом
cd /Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc

# Збірка проєкту
dotnet build

# Запуск веб-сервера
dotnet run
```
За замовчуванням застосунок запуститься за адресою: `http://localhost:5000` або `https://localhost:5001` (також адреса виводиться у консолі).

### Запуск у JetBrains Rider:
1. Відкрийте рішення [`LandAgencyMvc.sln`](file:///Users/mac/RiderProjects/LandAgencyMvc/LandAgencyMvc.sln).
2. У верхньому правому куті оберіть конфігурацію запуску **LandAgencyMvc**.
3. Натисніть зелену кнопку **Run** (або **Debug** для покрокового трасування коду).
4. Застосунок автоматично відкриється у браузері.

### Тестові дані для швидкого входу:
- **Рієлтор**: email `realtor@landagency.ua`, пароль `realtor123`
- **Юрист**: email `lawyer@landagency.ua`, пароль `lawyer123`
- **Клієнт**: можна зареєструвати через сторінку реєстрації або ввести довільний email (наприклад `ivan@gmail.com`).
