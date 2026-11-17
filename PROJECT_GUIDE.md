# Посібник із проєкту «Земельне агентство» (LandAgencyMvc)

Структура проєкту, карта прецедентів із діаграми ЛР1, матриця CRUD, реалізація ACID та підготовка до захисту.
Таблиці з посиланнями на код генеруються скриптом `tools/use_case_map.py`, тому номери рядків завжди актуальні.

## 1. Огляд

**LandAgencyMvc** — інформаційна система земельного агентства: ASP.NET Core MVC на .NET 10, Entity Framework Core (Code First), SQLite.
Ролі збігаються з діаграмою прецедентів ЛР1: чотири актори-люди й три зовнішні системи, які в проєкті емулюються.

| Роль | Що робить |
|---|---|
| **Покупець** (`Buyer`) | Шукає ділянки в каталозі, подає запити на підбір, замовляє й переносить огляди, оплачує рахунки, підписує договір у нотаріуса |
| **Продавець** (`Seller`) | Виставляє ділянки, задає ціну й умови продажу, завантажує правовстановлюючі документи |
| **Рієлтор** (`Realtor`) | Складає опис ділянки (координати, фото, відео), організовує огляди, готує угоди й підтверджує оплати |
| **Юрист** (`Lawyer`) | Перевіряє ділянки в ДЗК/ДРРП, видає висновки й акти ризиків, візує договори, реєструє перехід права власності |

Зовнішні системи з діаграми: **ДЗК/ДРРП** — `DzkRegistrySimulator`; **Банківська система** — IBAN і підтвердження платежу рієлтором;
**Сервіс підбору** — `AutoMatchingService`. Це емуляція в межах лабораторної роботи.

### Тестові акаунти

Адміністратора немає: роль `Admin` у проєкті не існує.

| Роль | Вхід | Email | Пароль |
|---|---|---|---|
| Рієлтор | Єдиний вхід (`/Account/Login`) | `realtor@landagency.ua`, `realtor2@landagency.ua` | `realtor123` |
| Юрист | Єдиний вхід | `lawyer@landagency.ua`, `lawyer2@landagency.ua` | `lawyer123` |
| Продавець | Єдиний вхід (`/Account/Login`) | `seller@landagency.ua`, `seller2@…`, `seller3@…`, `seller4@…` | `client123` |
| Покупець | Клієнтський | `buyer@landagency.ua`, `buyer2@…` … `buyer5@…` | `client123` |

## 2. Архітектура та життєвий цикл запиту

```
Браузер → Program.cs (маршрутизація, автентифікація, фільтри)
        → Контролер (Controllers/…, [Authorize(Roles = AppRoles.…)])
        → LandAgencyContext (EF Core) + сервіси (Services/…) + доменні методи сутностей (Models/…)
        → Razor-вид (Views/…) → HTML
```

1. `Program.cs` налаштовує cookie-автентифікацію (`LandAgency.Auth`), DI, SQLite, фільтр помилок БД та перехоплювач довговічності.
2. Атрибут `[Authorize(Roles = AppRoles.Buyer)]` та подібні розмежовують доступ; імена ролей зібрано в `Models/AppRoles.cs`.
3. Бізнес-правила живуть у сутностях (`SaleDeal.SignByParties`, `ViewingRequest.Reschedule` тощо), а контролер лише викликає їх і зберігає.
4. Неавторизований перехід на `/Realtor/...` або `/Lawyer/...` веде на службовий вхід, решта — на клієнтський (`Program.cs`, `OnRedirectToLogin`).

## 3. Структура файлів

```
LandAgencyMvc/
├── Program.cs                      DI, автентифікація, фільтр помилок БД, SQLite із синхронізацією FULL
├── landagency_seed.sql             Схема й початкові дані (13 користувачів, 20 ділянок (5 опублікованих), 19 висновків, 14 угод)
├── Controllers/
│   ├── AccountController.cs        UC-07 і UC-16: єдиний вхід для всіх ролей, реєстрація
│   ├── ClientController*.cs        Покупець і продавець (partial-файли за темами)
│   │   ├── .Catalog.cs             Каталог, запити на підбір, картка ділянки
│   │   ├── .Viewings.cs            Огляди: замовити, скасувати, перенести
│   │   ├── .Deals.cs               Заявка на купівлю, оплата, підпис у нотаріуса, квитанція
│   │   ├── .SellerPlots.cs         Ділянки продавця: створити, змінити, вилучити, документи
│   │   └── .Cabinet.cs             Особистий кабінет і профіль
│   ├── RealtorController*.cs       Рієлтор (partial-файли за темами)
│   │   ├── .Requests.cs            Запити на підбір
│   │   ├── .Viewings.cs            Огляди
│   │   ├── .Plots.cs               Ділянки: координати, фото, відео, документи, вилучення
│   │   ├── .Deals.cs               Угоди, рахунки, оплати, договір
│   │   ├── .Clients.cs             CRM клієнтів
│   │   └── .Analytics.cs           Фінансова аналітика
│   ├── LawyerController.cs         Перевірка ділянок, висновки, договори, реєстрація права
│   └── QueriesController.cs        Переспрямування старих посилань
├── Models/                         Сутності, перелічення, форми, симулятор ДЗК
├── Services/                       AutoMatchingService, CadastralValidationService, LandAgencyQueries,
│                                   PlotMediaStorage, PlotRemovalService
├── Filters/DatabaseExceptionFilter.cs   Зрозумілі повідомлення про конфлікти й порушення цілісності
├── Data/SqliteDurabilityInterceptor.cs  PRAGMA synchronous = FULL на кожному з'єднанні
├── Views/                          Razor-види (Account, Client, Realtor, Lawyer, Shared)
└── wwwroot/                        Стилі (site.css), скрипти, завантажені файли (uploads)
```

## 4. Карта прецедентів

Нумерація йде по діаграмі ЛР1 зверху вниз і зліва направо. Кожна дія в коді має в XML-документації рядок `UC-nn`,
тому прецедент знаходиться пошуком по `UC-05` у всьому проєкті.

<!-- use-case-map:start -->
| № | Прецедент | Актор | Зв'язок | Дії (контролер, сервіс) | Види | Модель, домен |
|---|---|---|---|---|---|---|
| **UC-01** | Подати запит на підбір ділянки | Покупець | include UC-02 | [`SubmitSelectionRequest`](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L137) | [Catalog.cshtml](LandAgencyMvc/Views/Client/Catalog.cshtml) | [`MatchPlots`](LandAgencyMvc/Models/SelectionRequest.cs#L70) |
| **UC-02** | Автоматичний підбір ділянок за критеріями | Система автоматизації | включається в UC-01 | [`MatchRequestAsync`](LandAgencyMvc/Services/AutoMatchingService.cs#L22)<br>[`MatchNewOrUpdatedPlotAsync`](LandAgencyMvc/Services/AutoMatchingService.cs#L46) | — | [`MatchPlots`](LandAgencyMvc/Models/SelectionRequest.cs#L70) |
| **UC-03** | Замовити виїзний огляд ділянки | Покупець | — | [`CreateViewing`](LandAgencyMvc/Controllers/ClientController.Viewings.cs#L17) | [Details.cshtml](LandAgencyMvc/Views/Client/Details.cshtml) | [`ConfirmViewing`](LandAgencyMvc/Models/ViewingRequest.cs#L112) |
| **UC-04** | Перенести дату огляду | Покупець | extend UC-03 | [`RescheduleViewingByClient`](LandAgencyMvc/Controllers/ClientController.Viewings.cs#L92) | [_BuyerCabinet.cshtml](LandAgencyMvc/Views/Client/_BuyerCabinet.cshtml) | [`Reschedule`](LandAgencyMvc/Models/ViewingRequest.cs#L97) |
| **UC-05** | Підписати договір купівлі-продажу у нотаріуса | Покупець | include UC-06 | [`SignContractAtNotary`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L181) | [_BuyerCabinet.cshtml](LandAgencyMvc/Views/Client/_BuyerCabinet.cshtml) | [`SignByParties`](LandAgencyMvc/Models/SaleDeal.cs#L228) |
| **UC-06** | Здійснити оплату / завдаток через банк | Покупець, Банківська система | включається в UC-05 | [`SubmitPayment`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L96)<br>[`VerifyClientPayment`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L200) | [_BuyerCabinet.cshtml](LandAgencyMvc/Views/Client/_BuyerCabinet.cshtml)<br>[Deals.cshtml](LandAgencyMvc/Views/Realtor/Deals.cshtml) | [`SubmitPayment`](LandAgencyMvc/Models/Payment.cs#L103)<br>[`VerifyPayment`](LandAgencyMvc/Models/Payment.cs#L125) |
| **UC-07** | Реєстрація та авторизація клієнта | Покупець, Продавець | — | [`Register`](LandAgencyMvc/Controllers/AccountController.cs#L82) / [`Register`](LandAgencyMvc/Controllers/AccountController.cs#L97)<br>[`Login`](LandAgencyMvc/Controllers/AccountController.cs#L25) / [`Login`](LandAgencyMvc/Controllers/AccountController.cs#L47) | [Register.cshtml](LandAgencyMvc/Views/Account/Register.cshtml)<br>[Login.cshtml](LandAgencyMvc/Views/Account/Login.cshtml) | [`Authenticate`](LandAgencyMvc/Models/AppUser.cs#L84) |
| **UC-08** | Зареєструвати ділянку на продаж | Продавець | include UC-09 | [`CreatePlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L14) / [`CreatePlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L37) | [CreatePlot.cshtml](LandAgencyMvc/Views/Client/CreatePlot.cshtml) | [`ValidateCadastralFormat`](LandAgencyMvc/Models/LandPlot.cs#L230) |
| **UC-09** | Перевірити кадастровий номер та реєстрацію в ДЗК | Державний земельний кадастр | включається в UC-08 | [`ValidateKoatuuFormat`](LandAgencyMvc/Services/CadastralValidationService.cs#L54) | — | [`QueryRegistriesAsync`](LandAgencyMvc/Models/DzkRegistrySimulator.cs#L17) |
| **UC-10** | Встановити ціну та умови продажу | Продавець | — | [`UpdatePlotTerms`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L171)<br>[`EditOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L228) / [`EditOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L254) | [_SellerCabinet.cshtml](LandAgencyMvc/Views/Client/_SellerCabinet.cshtml)<br>[EditOwnPlot.cshtml](LandAgencyMvc/Views/Client/EditOwnPlot.cshtml) | [`UpdateSaleConditions`](LandAgencyMvc/Models/LandPlot.cs#L206) |
| **UC-11** | Завантажити правовстановлюючі документи | Продавець | — | [`AddPlotDocument`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L349) | [_SellerCabinet.cshtml](LandAgencyMvc/Views/Client/_SellerCabinet.cshtml) | [`AttachFile`](LandAgencyMvc/Models/PlotDocument.cs#L85) |
| **UC-12** | Скласти опис ділянки з фото, відео та координатами | Рієлтор | — | [`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L232) / [`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L266)<br>[`AddPlotMedia`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L331) | [EditPlot.cshtml](LandAgencyMvc/Views/Realtor/EditPlot.cshtml)<br>[_PlotMediaManager.cshtml](LandAgencyMvc/Views/Shared/_PlotMediaManager.cshtml) | [`PlotMedia`](LandAgencyMvc/Models/PlotMedia.cs#L14)<br>[`SaveAsync`](LandAgencyMvc/Services/PlotMediaStorage.cs#L36) |
| **UC-13** | Організувати виїзд та показ ділянки на місці | Рієлтор | — | [`Viewings`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L16)<br>[`UpdateViewingStatus`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L36) | [Viewings.cshtml](LandAgencyMvc/Views/Realtor/Viewings.cshtml) | [`CompleteViewing`](LandAgencyMvc/Models/ViewingRequest.cs#L127) |
| **UC-14** | Підготувати пакет документів для нотаріуса | Рієлтор | include UC-15 | [`CreateDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L63)<br>[`PrintContract`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L321) | [Deals.cshtml](LandAgencyMvc/Views/Realtor/Deals.cshtml)<br>[PrintContract.cshtml](LandAgencyMvc/Views/Realtor/PrintContract.cshtml) | [`PrepareContractDraft`](LandAgencyMvc/Models/SaleDeal.cs#L161) |
| **UC-15** | Автоматичний розрахунок комісії агентства | Система | включається в UC-14 | — | — | [`CalculateCommission`](LandAgencyMvc/Models/SaleDeal.cs#L173) |
| **UC-16** | Службова авторизація співробітника | Рієлтор, Юрист | той самий вхід, що UC-07 | [`Login`](LandAgencyMvc/Controllers/AccountController.cs#L25) / [`Login`](LandAgencyMvc/Controllers/AccountController.cs#L47) | [Login.cshtml](LandAgencyMvc/Views/Account/Login.cshtml) | [`RedirectToWorkspace`](LandAgencyMvc/Controllers/AccountController.cs#L152) |
| **UC-17** | Перевірити обмеження використання ділянки | Юрист | include UC-18 | [`Queue`](LandAgencyMvc/Controllers/LawyerController.cs#L51)<br>[`Conclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L79) | [Queue.cshtml](LandAgencyMvc/Views/Lawyer/Queue.cshtml)<br>[Conclusion.cshtml](LandAgencyMvc/Views/Lawyer/Conclusion.cshtml) | [`AnalyzePricePerSotkaAsync`](LandAgencyMvc/Services/CadastralValidationService.cs#L103) |
| **UC-18** | Онлайн-запит витягу з бази ДЗК | Державний земельний кадастр | включається в UC-17 | — | — | [`QueryRegistriesAsync`](LandAgencyMvc/Models/DzkRegistrySimulator.cs#L17) |
| **UC-19** | Оформити юридичний звіт щодо ділянки | Юрист | extend UC-20 | [`SubmitLegalConclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L125)<br>[`RevokeLegalConclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L323) | [Conclusion.cshtml](LandAgencyMvc/Views/Lawyer/Conclusion.cshtml) | [`Approve`](LandAgencyMvc/Models/LegalConclusion.cs#L41) |
| **UC-20** | Скласти акт про виявлені ризики | Юрист | розширює UC-19 | [`SubmitLegalConclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L125) | [Conclusion.cshtml](LandAgencyMvc/Views/Lawyer/Conclusion.cshtml) | [`Reject`](LandAgencyMvc/Models/LegalConclusion.cs#L55) |
| **UC-21** | Зареєструвати перехід права власності в ДЗК | Юрист | — | [`RegisterOwnershipTransfer`](LandAgencyMvc/Controllers/LawyerController.cs#L377) | [Contracts.cshtml](LandAgencyMvc/Views/Lawyer/Contracts.cshtml) | [`RegisterOwnershipRight`](LandAgencyMvc/Models/SaleDeal.cs#L244) |
<!-- use-case-map:end -->

## 5. Модель даних

Дев'ять таблиць (`Models/LandAgencyContext.cs`):
`Users`, `LandPlots`, `PlotDocuments`, `PlotMedia`, `LegalConclusions`, `ViewingRequests`, `SelectionRequests`, `SaleDeals`, `Payments`.

- Один клас `AppUser` із полем `Role` замість окремих класів-нащадків: специфічні поля (ліцензія, свідоцтво) необов'язкові. Це спрощена реалізація узагальнення «Користувач → Покупець, Продавець, Рієлтор, Юрист» з діаграми класів.
- Композиція з діаграми класів: документи й медіа каскадно видаляються з ділянкою; фінансова історія захищена `Restrict`.
- Колонка `Version` у `LandPlots` і `SaleDeals` — маркер для оптимістичної паралельності.
- `decimal` у SQLite зберігається як `TEXT`, тому суми сортуються й групуються в пам'яті (LINQ to Objects), а умови відбору йдуть у SQL.

## 6. CRUD для всіх даних

Платежі не редагуються довільно: сума виводиться з ціни угоди, статус змінюється лише через подання й підтвердження, а вилучаються вони разом з угодою.
Облікові записи співробітників створює агенція (сід), тому CRUD акаунтів у застосунку є для клієнтів.

<!-- crud-matrix:start -->
| Таблиця | Create | Read | Update | Delete | Код |
|---|---|---|---|---|---|
| `Users` | Покупець/Продавець: Register; рієлтор: AddClient | Рієлтор: Clients; клієнт: Cabinet | Клієнт: ChangePassword; рієлтор: UpdateClient | Рієлтор: DeleteClient (лише клієнти) | [`Register`](LandAgencyMvc/Controllers/AccountController.cs#L82) / [`Register`](LandAgencyMvc/Controllers/AccountController.cs#L97)<br>[`AddClient`](LandAgencyMvc/Controllers/RealtorController.Clients.cs#L63)<br>[`Cabinet`](LandAgencyMvc/Controllers/ClientController.Cabinet.cs#L67)<br>[`ChangePassword`](LandAgencyMvc/Controllers/ClientController.Cabinet.cs#L13)<br>[`UpdateClient`](LandAgencyMvc/Controllers/RealtorController.Clients.cs#L97)<br>[`DeleteClient`](LandAgencyMvc/Controllers/RealtorController.Clients.cs#L127) |
| `LandPlots` | Продавець: CreatePlot; рієлтор: CreatePlot | Покупець: Catalog, Details; продавець: Cabinet; рієлтор: Plots; юрист: Queue | Продавець: UpdatePlotTerms, EditOwnPlot; рієлтор: EditPlot (юридичні факти закриті під час і після перевірки); юрист: SubmitLegalConclusion | Продавець: DeleteOwnPlot; рієлтор: DeletePlotConfirmed (лише ділянки без юридичного висновку) | [`CreatePlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L14) / [`CreatePlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L37)<br>[`CreatePlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L131) / [`CreatePlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L147)<br>[`Catalog`](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L19)<br>[`Plots`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L95)<br>[`EditOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L228) / [`EditOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L254)<br>[`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L232) / [`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L266)<br>[`DeleteOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L296)<br>[`DeletePlotConfirmed`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L450) |
| `PlotDocuments` | Продавець: AddPlotDocument, CreatePlot | Details, Cabinet | Рієлтор: EditPlot (неверифіковані); юрист: SubmitLegalConclusion (верифікація) | Рієлтор: DeletePlotDocument (неверифіковані) | [`AddPlotDocument`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L349)<br>[`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L232) / [`EditPlot`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L266)<br>[`DeletePlotDocument`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L385) |
| `ArchivedPlots` | Система: копія створюється під час вилучення ділянки (DeleteOwnPlot, DeletePlotConfirmed) | Рієлтор: PlotArchive | не змінюється: тригер бази забороняє UPDATE | не видаляється: тригер бази забороняє DELETE | [`DeleteOwnPlot`](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L296)<br>[`DeletePlotConfirmed`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L450)<br>[`PlotArchive`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L514) |
| `PlotMedia` | Рієлтор: AddPlotMedia | Покупець: Details | не редагується: вилучити й додати знову | Рієлтор: DeletePlotMedia | [`AddPlotMedia`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L331)<br>[`DeletePlotMedia`](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L365) |
| `LegalConclusions` | Юрист: SubmitLegalConclusion | Юрист: Conclusion, Queue | Юрист: SubmitLegalConclusion (повторно) | Юрист: RevokeLegalConclusion | [`SubmitLegalConclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L125)<br>[`Conclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L79)<br>[`RevokeLegalConclusion`](LandAgencyMvc/Controllers/LawyerController.cs#L323) |
| `ViewingRequests` | Покупець: CreateViewing; рієлтор: CreateViewingByRealtor | Покупець: Cabinet; рієлтор: Viewings | Покупець: RescheduleViewingByClient, CancelViewingByClient; рієлтор: UpdateViewingStatus, EditViewing (лише допустимі переходи) | Рієлтор: DeleteViewing (лише нові, скасовані, відхилені) | [`CreateViewing`](LandAgencyMvc/Controllers/ClientController.Viewings.cs#L17)<br>[`CreateViewingByRealtor`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L106)<br>[`Viewings`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L16)<br>[`RescheduleViewingByClient`](LandAgencyMvc/Controllers/ClientController.Viewings.cs#L92)<br>[`EditViewing`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L135)<br>[`DeleteViewing`](LandAgencyMvc/Controllers/RealtorController.Viewings.cs#L175) |
| `SelectionRequests` | Покупець: SubmitSelectionRequest; рієлтор: CreateSelectionRequest | Покупець: Cabinet; рієлтор: Requests | Покупець: EditSelectionRequestByClient; рієлтор: EditSelectionRequest | Покупець: DeleteSelectionRequestByClient; рієлтор: DeleteSelectionRequest | [`SubmitSelectionRequest`](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L137)<br>[`CreateSelectionRequest`](LandAgencyMvc/Controllers/RealtorController.Requests.cs#L85)<br>[`Requests`](LandAgencyMvc/Controllers/RealtorController.Requests.cs#L16)<br>[`EditSelectionRequestByClient`](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L186)<br>[`EditSelectionRequest`](LandAgencyMvc/Controllers/RealtorController.Requests.cs#L113)<br>[`DeleteSelectionRequestByClient`](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L227)<br>[`DeleteSelectionRequest`](LandAgencyMvc/Controllers/RealtorController.Requests.cs#L139) |
| `SaleDeals` | Рієлтор: CreateDeal; покупець: RequestPurchaseFromViewing | Рієлтор: Deals; юрист: Contracts; покупець: Cabinet | Рієлтор: EditDeal, SendDealToLawyer; юрист: ApproveDealContract, RegisterOwnershipTransfer; покупець: SignContractAtNotary | Рієлтор: DeleteDealConfirmed, CancelDeal | [`CreateDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L63)<br>[`RequestPurchaseFromViewing`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L16)<br>[`Deals`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L17)<br>[`EditDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L367) / [`EditDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L408)<br>[`RegisterOwnershipTransfer`](LandAgencyMvc/Controllers/LawyerController.cs#L377)<br>[`SignContractAtNotary`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L181)<br>[`DeleteDealConfirmed`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L465)<br>[`CancelDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L296) |
| `Payments` | Система: створюються разом з угодою (CreateDeal) | Покупець: Cabinet, DownloadReceipt; рієлтор: Deals | Покупець: SubmitPayment; рієлтор: VerifyClientPayment, RejectClientPayment (лише з «на перевірці») | Разом з угодою (DeleteDealConfirmed) | [`CreateDeal`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L63)<br>[`DownloadReceipt`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L218)<br>[`SubmitPayment`](LandAgencyMvc/Controllers/ClientController.Deals.cs#L96)<br>[`VerifyClientPayment`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L200)<br>[`DeleteDealConfirmed`](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L465) |
<!-- crud-matrix:end -->

### Правила логічності та збереження історії

Принцип роботи: роль виконує лише природні для неї операції, а відомості, на яких тримаються звітність і аудит, ніколи не знищуються.
Правила закладено в доменну модель та на сервер (приховування кнопок лише дублює їх).

| Операція | Правило | Чому |
|---|---|---|
| Зміна юридичних фактів ділянки | кадастровий номер, площа, призначення, власник і номер документа закриті, поки ділянку перевіряє юрист, і після схвалення (`LandPlot.AreLegalFactsLocked`); виправити їх можна лише після відхилення, і після збереження ділянка повертається юристу (`ResubmitForLegalCheck`) | юрист перевіряє незмінні дані, а схвалена ділянка не лишається «перевіреною» з іншими фактами |
| Верифіковані документи | рієлтор їх не змінює й не вилучає; новий документ рієлтора не верифікований | верифікацію надає лише юрист |
| Вилучення ділянки | лише ділянка без юридичного висновку (`LandPlot.CanBeDeleted`); решту спершу повертає на перевірку юрист | висновки й акти про ризики потрібні для аудиту |
| Архів вилучених ділянок | вилучення потребує причини; копія ділянки (кадастровий номер, власник, статус, хто й коли вилучив) пишеться в `ArchivedPlots` в одній транзакції з видаленням (`PlotRemovalService`); тригери бази забороняють змінювати й видаляти архів | вилучене не зникає безслідно, звітність і аудит бачать усе минуле |
| Підтвердження й відхилення платежу | лише платіж, поданий покупцем (`Payment.VerifyPayment`, `RejectPayment`) | оборот у звітності не змінюється заднім числом |
| Вилучення огляду | лише нова, скасована або відхилена покупцем заявка (`ViewingRequest.CanBeDeleted`) | проведений огляд є підставою для угоди |
| Статус огляду | лише допустимі переходи (`ViewingRequest.CanChangeStatusTo`) | послідовність процесу |
| Платежі, акаунти співробітників | немає довільного створення, зміни чи вилучення в інтерфейсі | фінансова історія недоторканна; ролі за діаграмою, адміністратора немає |

## 7. ACID

SQLite підтримує транзакції; поверх них налаштовано кожну властивість окремо.

<!-- acid-map:start -->
| Властивість | Механізм | Де в коді |
|---|---|---|
| Atomicity | Транзакція навколо кількох збережень | [ClientController.Catalog.cs:169](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L169), [ClientController.Catalog.cs:212](LandAgencyMvc/Controllers/ClientController.Catalog.cs#L212), [ClientController.Deals.cs:50](LandAgencyMvc/Controllers/ClientController.Deals.cs#L50), [ClientController.SellerPlots.cs:122](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L122), [ClientController.SellerPlots.cs:279](LandAgencyMvc/Controllers/ClientController.SellerPlots.cs#L279), [LawyerController.cs:151](LandAgencyMvc/Controllers/LawyerController.cs#L151), [LawyerController.cs:343](LandAgencyMvc/Controllers/LawyerController.cs#L343), [RealtorController.Deals.cs:141](LandAgencyMvc/Controllers/RealtorController.Deals.cs#L141), [RealtorController.Plots.cs:193](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L193), [RealtorController.Plots.cs:311](LandAgencyMvc/Controllers/RealtorController.Plots.cs#L311), [LandAgencyQueries.cs:365](LandAgencyMvc/Services/LandAgencyQueries.cs#L365), [LandAgencyQueries.cs:467](LandAgencyMvc/Services/LandAgencyQueries.cs#L467) |
| Consistency | CHECK-обмеження | [LandAgencyContext.cs:41](LandAgencyMvc/Models/LandAgencyContext.cs#L41) |
| Consistency | Одна активна угода на ділянку (частковий унікальний індекс) | [LandAgencyContext.cs:38](LandAgencyMvc/Models/LandAgencyContext.cs#L38) |
| Consistency | Зовнішні ключі та Restrict/Cascade | [LandAgencyContext.cs:68](LandAgencyMvc/Models/LandAgencyContext.cs#L68) |
| Isolation | Токен версії рядка | [LandAgencyContext.cs:26](LandAgencyMvc/Models/LandAgencyContext.cs#L26) |
| Isolation | Оновлення версії при кожному збереженні | [LandAgencyContext.cs:177](LandAgencyMvc/Models/LandAgencyContext.cs#L177) |
| Isolation | Версія з форми редагування | [LandAgencyContext.cs:165](LandAgencyMvc/Models/LandAgencyContext.cs#L165) |
| Isolation | Повідомлення про конфлікт замість помилки | [DatabaseExceptionFilter.cs:28](LandAgencyMvc/Filters/DatabaseExceptionFilter.cs#L28) |
| Durability | synchronous = FULL на кожному з'єднанні | [SqliteDurabilityInterceptor.cs:13](LandAgencyMvc/Data/SqliteDurabilityInterceptor.cs#L13) |
<!-- acid-map:end -->

**Атомарність.** Дії з кількома збереженнями (створення угоди, реєстрація ділянки, заявка на купівлю, висновок юриста тощо) виконуються в
`BeginTransactionAsync`: якщо другий крок падає, перший скасовується. Файли не входять у транзакцію БД, тому при помилці реєстрації ділянки завантажений файл вилучається вручну (`DeleteUploadedFile`).
Вилучення ділянки теж атомарне: архівна копія в `ArchivedPlots` і видалення ділянки з залежними записами виконує одне `SaveChangesAsync` у `PlotRemovalService`, тож не буває видалення без архіву.

**Узгодженість.** Правила діють у самій базі, а не лише в коді: `CHECK` (площа й ціна додатні, координати в межах України, роль 0–3, суми угод і платежів),
частковий унікальний індекс «одна активна угода на ділянку», зовнішні ключі `Restrict`/`Cascade`.

**Ізоляція.** SQLite виконує запис по одному (serializable). Для втрачених оновлень між запитами є токен `Version`: форма редагування передає версію,
з якою її відкрили, і збереження зі застарілою версією відхиляється. Це типовий приклад оптимістичної паралельності.

**Довговічність.** `PRAGMA synchronous = FULL` на кожному з'єднанні: підтверджена транзакція записана на диск до відповіді.

### Сценарій демонстрації для захисту

1. **Атомарність.** Підмінити в БД номер рахунку так, щоб при створенні угоди другий крок (рахунки) порушив унікальність. Угода не лишається в базі, а користувач бачить повідомлення «Усі зміни скасовано».
2. **Узгодженість.** У консолі SQLite вставити ділянку з площею 0 або другу активну угоду на ту саму ділянку: база відхиляє запис (`CHECK constraint failed`, `UNIQUE constraint failed`).
3. **Ізоляція.** Відкрити форму редагування ділянки рієлтором, змінити ту саму ділянку з іншого вікна й зберегти першу форму: з'являється повідомлення, що дані змінив інший користувач, а чужі зміни не затерті.
4. **Довговічність.** Створити запис, зупинити застосунок командою `kill -9`, запустити знову: запис на місці.

## 8. Запуск

```bash
cd LandAgencyMvc
rm -f landagency.db landagency.db-shm landagency.db-wal   # після зміни схеми базу потрібно створити заново
dotnet run
```

Базу створює `EnsureCreated()`, а `landagency_seed.sql` наповнює її при першому запуску. Якщо змінюєте модель, видаліть `landagency.db` і оновіть сід (колонки в `INSERT` позиційні).

### Як змінити код

- **Ставка комісії:** `SaleDeal.CalculateCommission` (UC-15), значення за замовчуванням у `AppUser.CommissionRate`.
- **Нове поле ділянки:** додайте властивість у `LandPlot` (в кінець класу, щоб колонка стала останньою), форму й дію контролера, видаліть `landagency.db` і додайте значення до `INSERT INTO LandPlots` у сіді.
- **Нова дія:** додайте її в потрібний partial-файл контролера, роль у `[Authorize(Roles = AppRoles.…)]` і рядок `UC-nn` у документацію.

## 9. Питання на захисті

| Питання | Відповідь і де в коді |
|---|---|
| Де обмежено права ролі? | Атрибути `[Authorize(Roles = AppRoles.…)]` на діях; імена ролей у `Models/AppRoles.cs`; видимість кнопок у `_AuthNavbar` та кабінетах (це лише зручність, захист на сервері). |
| Чому ролі саме такі? | Вони взяті з діаграми прецедентів ЛР1: Покупець, Продавець, Рієлтор, Юрист. Вхід для всіх ролей єдиний (UC-07 і UC-16): роль облікового запису визначає робоче місце. |
| Де реалізовано CRUD? | Таблиця розділу 6; у кожної сутності є Create/Read/Update/Delete для відповідальних ролей. |
| Як гарантується атомарність? | `BeginTransactionAsync` у діях із кількома збереженнями (розділ 7). |
| Що буде при одночасному редагуванні? | Токен `Version`: другий користувач отримує повідомлення замість мовчазного перезапису. |
| Чому не можна вилучити ділянку з угодою чи висновком? | Зовнішній ключ `Restrict` і `LandPlot.CanBeDeleted`: фінансова історія й юридичний висновок зберігаються для звітності та аудиту. |
| Чому рієлтор не змінює кадастровий номер ділянки на перевірці чи після неї? | `LandPlot.AreLegalFactsLocked`: поки юрист перевіряє ділянку і після схвалення ці факти закриті. Помилку виправляють після відхилення юристом, і ділянка знову йде на перевірку. |
| Чому не можна довільно змінити чи вилучити платіж? | Сума виводиться з ціни угоди, статус змінюється лише через подання й підтвердження (`Payment.VerifyPayment`); інакше змінився б оборот, уже врахований у звітах. |
| Де обчислюється комісія? | `SaleDeal.CalculateCommission` (UC-15), викликається з `PrepareContractDraft` у `CreateDeal` (UC-14). |
| Чому рієлтор не підписує договір? | За діаграмою договір підписує покупець (UC-05), а перехід права реєструє юрист (UC-21). |
| Чи справжні ДЗК і банк? | Ні, це емуляція: `DzkRegistrySimulator`, IBAN і підтвердження платежу рієлтором. |
| Чому SQLite? | Розробка на macOS, де SQL Server LocalDB недоступний; EF Core однаково працює з обома. |
| Чому суми рахуються в пам'яті? | У SQLite `decimal` зберігається як текст, тож агрегація й сортування в SQL були б лексикографічними. |
| Де успадкування `Користувач → …`? | Один `AppUser` із полем `Role`; на рівні форм є `PlotEditViewModel : PlotCreateViewModel`. |
