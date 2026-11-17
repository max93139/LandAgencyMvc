"""Генерує таблиці PROJECT_GUIDE.md: карту прецедентів, CRUD-матрицю та місця реалізації ACID.

Номери рядків шукаються у коді, тому посилання лишаються точними після змін.
Запуск із кореня репозиторію:  python3 tools/use_case_map.py --write PROJECT_GUIDE.md
Без аргументів таблиці друкуються в консоль.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / 'LandAgencyMvc'

CTRL = 'Controllers/'
CLIENT_CAT = CTRL + 'ClientController.Catalog.cs'
CLIENT_VIEW = CTRL + 'ClientController.Viewings.cs'
CLIENT_DEALS = CTRL + 'ClientController.Deals.cs'
CLIENT_PLOTS = CTRL + 'ClientController.SellerPlots.cs'
CLIENT_CAB = CTRL + 'ClientController.Cabinet.cs'
RT_REQ = CTRL + 'RealtorController.Requests.cs'
RT_VIEW = CTRL + 'RealtorController.Viewings.cs'
RT_PLOTS = CTRL + 'RealtorController.Plots.cs'
RT_DEALS = CTRL + 'RealtorController.Deals.cs'
RT_CLIENTS = CTRL + 'RealtorController.Clients.cs'
LAWYER = CTRL + 'LawyerController.cs'
ACCOUNT = CTRL + 'AccountController.cs'

USE_CASES = [
    ('UC-01', 'Подати запит на підбір ділянки', 'Покупець', 'include UC-02',
     [(CLIENT_CAT, 'SubmitSelectionRequest')], ['Views/Client/Catalog.cshtml'], [('Models/SelectionRequest.cs', 'MatchPlots')]),
    ('UC-02', 'Автоматичний підбір ділянок за критеріями', 'Система автоматизації', 'включається в UC-01',
     [('Services/AutoMatchingService.cs', 'MatchRequestAsync'), ('Services/AutoMatchingService.cs', 'MatchNewOrUpdatedPlotAsync')], [], [('Models/SelectionRequest.cs', 'MatchPlots')]),
    ('UC-03', 'Замовити виїзний огляд ділянки', 'Покупець', '',
     [(CLIENT_VIEW, 'CreateViewing')], ['Views/Client/Details.cshtml'], [('Models/ViewingRequest.cs', 'ConfirmViewing')]),
    ('UC-04', 'Перенести дату огляду', 'Покупець', 'extend UC-03',
     [(CLIENT_VIEW, 'RescheduleViewingByClient')], ['Views/Client/_BuyerCabinet.cshtml'], [('Models/ViewingRequest.cs', 'Reschedule')]),
    ('UC-05', 'Підписати договір купівлі-продажу у нотаріуса', 'Покупець', 'include UC-06',
     [(CLIENT_DEALS, 'SignContractAtNotary')], ['Views/Client/_BuyerCabinet.cshtml'], [('Models/SaleDeal.cs', 'SignByParties')]),
    ('UC-06', 'Здійснити оплату / завдаток через банк', 'Покупець, Банківська система', 'включається в UC-05',
     [(CLIENT_DEALS, 'SubmitPayment'), (RT_DEALS, 'VerifyClientPayment')], ['Views/Client/_BuyerCabinet.cshtml', 'Views/Realtor/Deals.cshtml'], [('Models/Payment.cs', 'SubmitPayment'), ('Models/Payment.cs', 'VerifyPayment')]),
    ('UC-07', 'Реєстрація та авторизація клієнта', 'Покупець, Продавець', '',
     [(ACCOUNT, 'Register'), (ACCOUNT, 'Login')], ['Views/Account/Register.cshtml', 'Views/Account/Login.cshtml'], [('Models/AppUser.cs', 'Authenticate')]),
    ('UC-08', 'Зареєструвати ділянку на продаж', 'Продавець', 'include UC-09',
     [(CLIENT_PLOTS, 'CreatePlot')], ['Views/Client/CreatePlot.cshtml'], [('Models/LandPlot.cs', 'ValidateCadastralFormat')]),
    ('UC-09', 'Перевірити кадастровий номер та реєстрацію в ДЗК', 'Державний земельний кадастр', 'включається в UC-08',
     [('Services/CadastralValidationService.cs', 'ValidateKoatuuFormat')], [], [('Models/DzkRegistrySimulator.cs', 'QueryRegistriesAsync')]),
    ('UC-10', 'Встановити ціну та умови продажу', 'Продавець', '',
     [(CLIENT_PLOTS, 'UpdatePlotTerms'), (CLIENT_PLOTS, 'EditOwnPlot')], ['Views/Client/_SellerCabinet.cshtml', 'Views/Client/EditOwnPlot.cshtml'], [('Models/LandPlot.cs', 'UpdateSaleConditions')]),
    ('UC-11', 'Завантажити правовстановлюючі документи', 'Продавець', '',
     [(CLIENT_PLOTS, 'AddPlotDocument')], ['Views/Client/_SellerCabinet.cshtml'], [('Models/PlotDocument.cs', 'AttachFile')]),
    ('UC-12', 'Скласти опис ділянки з фото, відео та координатами', 'Рієлтор', '',
     [(RT_PLOTS, 'EditPlot'), (RT_PLOTS, 'AddPlotMedia')], ['Views/Realtor/EditPlot.cshtml', 'Views/Shared/_PlotMediaManager.cshtml'], [('Models/PlotMedia.cs', 'PlotMedia'), ('Services/PlotMediaStorage.cs', 'SaveAsync')]),
    ('UC-13', 'Організувати виїзд та показ ділянки на місці', 'Рієлтор', '',
     [(RT_VIEW, 'Viewings'), (RT_VIEW, 'UpdateViewingStatus')], ['Views/Realtor/Viewings.cshtml'], [('Models/ViewingRequest.cs', 'CompleteViewing')]),
    ('UC-14', 'Підготувати пакет документів для нотаріуса', 'Рієлтор', 'include UC-15',
     [(RT_DEALS, 'CreateDeal'), (RT_DEALS, 'PrintContract')], ['Views/Realtor/Deals.cshtml', 'Views/Realtor/PrintContract.cshtml'], [('Models/SaleDeal.cs', 'PrepareContractDraft')]),
    ('UC-15', 'Автоматичний розрахунок комісії агентства', 'Система', 'включається в UC-14',
     [], [], [('Models/SaleDeal.cs', 'CalculateCommission')]),
    ('UC-16', 'Службова авторизація співробітника', 'Рієлтор, Юрист', 'той самий вхід, що UC-07',
     [(ACCOUNT, 'Login')], ['Views/Account/Login.cshtml'], [('Controllers/AccountController.cs', 'RedirectToWorkspace')]),
    ('UC-17', 'Перевірити обмеження використання ділянки', 'Юрист', 'include UC-18',
     [(LAWYER, 'Queue'), (LAWYER, 'Conclusion')], ['Views/Lawyer/Queue.cshtml', 'Views/Lawyer/Conclusion.cshtml'], [('Services/CadastralValidationService.cs', 'AnalyzePricePerSotkaAsync')]),
    ('UC-18', 'Онлайн-запит витягу з бази ДЗК', 'Державний земельний кадастр', 'включається в UC-17',
     [], [], [('Models/DzkRegistrySimulator.cs', 'QueryRegistriesAsync')]),
    ('UC-19', 'Оформити юридичний звіт щодо ділянки', 'Юрист', 'extend UC-20',
     [(LAWYER, 'SubmitLegalConclusion'), (LAWYER, 'RevokeLegalConclusion')], ['Views/Lawyer/Conclusion.cshtml'], [('Models/LegalConclusion.cs', 'Approve')]),
    ('UC-20', 'Скласти акт про виявлені ризики', 'Юрист', 'розширює UC-19',
     [(LAWYER, 'SubmitLegalConclusion')], ['Views/Lawyer/Conclusion.cshtml'], [('Models/LegalConclusion.cs', 'Reject')]),
    ('UC-21', 'Зареєструвати перехід права власності в ДЗК', 'Юрист', '',
     [(LAWYER, 'RegisterOwnershipTransfer')], ['Views/Lawyer/Contracts.cshtml'], [('Models/SaleDeal.cs', 'RegisterOwnershipRight')]),
]

CRUD = [
    ('Users', 'Покупець/Продавець: Register; рієлтор: AddClient',
     'Рієлтор: Clients; клієнт: Settings', 'Клієнт: ChangePassword (сторінка Settings); рієлтор: UpdateClient', 'Рієлтор: DeleteClient (лише клієнти)',
     [(ACCOUNT, 'Register'), (RT_CLIENTS, 'AddClient'), (CLIENT_CAB, 'Settings'), (CLIENT_CAB, 'ChangePassword'), (RT_CLIENTS, 'UpdateClient'), (RT_CLIENTS, 'DeleteClient')]),
    ('LandPlots', 'Продавець: CreatePlot; рієлтор: CreatePlot',
     'Покупець: Catalog, Details; продавець: Cabinet; рієлтор: Plots; юрист: Queue', 'Продавець: UpdatePlotTerms, EditOwnPlot; рієлтор: EditPlot (юридичні факти закриті під час і після перевірки); юрист: SubmitLegalConclusion', 'Продавець: DeleteOwnPlot; рієлтор: DeletePlotConfirmed (лише ділянки без юридичного висновку)',
     [(CLIENT_PLOTS, 'CreatePlot'), (RT_PLOTS, 'CreatePlot'), (CLIENT_CAT, 'Catalog'), (RT_PLOTS, 'Plots'), (CLIENT_PLOTS, 'EditOwnPlot'), (RT_PLOTS, 'EditPlot'), (CLIENT_PLOTS, 'DeleteOwnPlot'), (RT_PLOTS, 'DeletePlotConfirmed')]),
    ('PlotDocuments', 'Продавець: AddPlotDocument, CreatePlot',
     'Details, Cabinet', 'Рієлтор: EditPlot (неверифіковані); юрист: SubmitLegalConclusion (верифікація)', 'Рієлтор: DeletePlotDocument (неверифіковані)',
     [(CLIENT_PLOTS, 'AddPlotDocument'), (RT_PLOTS, 'EditPlot'), (RT_PLOTS, 'DeletePlotDocument')]),
    ('ArchivedPlots', 'Система: копія створюється під час вилучення ділянки (DeleteOwnPlot, DeletePlotConfirmed)', 'Рієлтор: PlotArchive',
     'не змінюється: тригер бази забороняє UPDATE', 'не видаляється: тригер бази забороняє DELETE',
     [(CLIENT_PLOTS, 'DeleteOwnPlot'), (RT_PLOTS, 'DeletePlotConfirmed'), (RT_PLOTS, 'PlotArchive')]),
    ('PlotMedia', 'Рієлтор: AddPlotMedia', 'Покупець: Details', 'не редагується: вилучити й додати знову', 'Рієлтор: DeletePlotMedia',
     [(RT_PLOTS, 'AddPlotMedia'), (RT_PLOTS, 'DeletePlotMedia')]),
    ('LegalConclusions', 'Юрист: SubmitLegalConclusion', 'Юрист: Conclusion, Queue', 'Юрист: SubmitLegalConclusion (повторно)', 'Юрист: RevokeLegalConclusion',
     [(LAWYER, 'SubmitLegalConclusion'), (LAWYER, 'Conclusion'), (LAWYER, 'RevokeLegalConclusion')]),
    ('ViewingRequests', 'Покупець: CreateViewing; рієлтор: CreateViewingByRealtor', 'Покупець: Cabinet; рієлтор: Viewings',
     'Покупець: RescheduleViewingByClient, CancelViewingByClient; рієлтор: UpdateViewingStatus, EditViewing (лише допустимі переходи)', 'Рієлтор: DeleteViewing (лише нові, скасовані, відхилені)',
     [(CLIENT_VIEW, 'CreateViewing'), (RT_VIEW, 'CreateViewingByRealtor'), (RT_VIEW, 'Viewings'), (CLIENT_VIEW, 'RescheduleViewingByClient'), (RT_VIEW, 'EditViewing'), (RT_VIEW, 'DeleteViewing')]),
    ('SelectionRequests', 'Покупець: SubmitSelectionRequest; рієлтор: CreateSelectionRequest', 'Покупець: Cabinet; рієлтор: Requests',
     'Покупець: EditSelectionRequestByClient; рієлтор: EditSelectionRequest', 'Покупець: DeleteSelectionRequestByClient; рієлтор: DeleteSelectionRequest',
     [(CLIENT_CAT, 'SubmitSelectionRequest'), (RT_REQ, 'CreateSelectionRequest'), (RT_REQ, 'Requests'), (CLIENT_CAT, 'EditSelectionRequestByClient'), (RT_REQ, 'EditSelectionRequest'), (CLIENT_CAT, 'DeleteSelectionRequestByClient'), (RT_REQ, 'DeleteSelectionRequest')]),
    ('SaleDeals', 'Рієлтор: CreateDeal; покупець: RequestPurchaseFromViewing', 'Рієлтор: Deals; юрист: Contracts; покупець: Cabinet',
     'Рієлтор: EditDeal, SendDealToLawyer; юрист: ApproveDealContract, RegisterOwnershipTransfer; покупець: SignContractAtNotary', 'Рієлтор: DeleteDealConfirmed, CancelDeal',
     [(RT_DEALS, 'CreateDeal'), (CLIENT_DEALS, 'RequestPurchaseFromViewing'), (RT_DEALS, 'Deals'), (RT_DEALS, 'EditDeal'), (LAWYER, 'RegisterOwnershipTransfer'), (CLIENT_DEALS, 'SignContractAtNotary'), (RT_DEALS, 'DeleteDealConfirmed'), (RT_DEALS, 'CancelDeal')]),
    ('Payments', 'Система: створюються разом з угодою (CreateDeal)', 'Покупець: Cabinet, DownloadReceipt; рієлтор: Deals',
     'Покупець: SubmitPayment; рієлтор: VerifyClientPayment, RejectClientPayment (лише з «на перевірці»)', 'Разом з угодою (DeleteDealConfirmed)',
     [(RT_DEALS, 'CreateDeal'), (CLIENT_DEALS, 'DownloadReceipt'), (CLIENT_DEALS, 'SubmitPayment'), (RT_DEALS, 'VerifyClientPayment'), (RT_DEALS, 'DeleteDealConfirmed')]),
]

ACID = [
    ('Atomicity', 'Транзакція навколо кількох збережень', 'BeginTransactionAsync', None),
    ('Consistency', 'CHECK-обмеження', 'HasCheckConstraint', 'Models/LandAgencyContext.cs'),
    ('Consistency', 'Одна активна угода на ділянку (частковий унікальний індекс)', 'UX_SaleDeals_LandPlotId_Active', 'Models/LandAgencyContext.cs'),
    ('Consistency', 'Зовнішні ключі та Restrict/Cascade', 'DeleteBehavior.', 'Models/LandAgencyContext.cs'),
    ('Isolation', 'Токен версії рядка', 'IsConcurrencyToken', 'Models/LandAgencyContext.cs'),
    ('Isolation', 'Оновлення версії при кожному збереженні', 'private void StampVersions', 'Models/LandAgencyContext.cs'),
    ('Isolation', 'Версія з форми редагування', 'public void UseSubmittedVersion', 'Models/LandAgencyContext.cs'),
    ('Isolation', 'Повідомлення про конфлікт замість помилки', 'DbUpdateConcurrencyException =>', 'Filters/DatabaseExceptionFilter.cs'),
    ('Durability', 'synchronous = FULL на кожному з\'єднанні', 'PRAGMA synchronous', 'Data/SqliteDurabilityInterceptor.cs'),
]


def find_line(rel, pattern):
    path = SRC / rel
    text = path.read_text(encoding='utf-8').split('\n')
    sig = re.compile(r'(public|private|protected|internal)\b[^;=]*\b' + re.escape(pattern) + r'\b\s*[\(\{<]|\b' + re.escape(pattern) + r'\b')
    for i, line in enumerate(text, 1):
        if re.search(r'(public|private|protected)\s+[^=;]*\b' + re.escape(pattern) + r'\s*\(', line) or re.search(r'class\s+' + re.escape(pattern) + r'\b', line):
            return i
    for i, line in enumerate(text, 1):
        if pattern in line:
            return i
    raise SystemExit(f'не знайдено {pattern} у {rel}')


def find_all_lines(rel, pattern):
    text = (SRC / rel).read_text(encoding='utf-8').split('\n')
    return [i for i, line in enumerate(text, 1) if re.search(r'(public|private)\s+[^=;]*\b' + re.escape(pattern) + r'\s*\(', line)]


def link(rel, pattern, label=None):
    lines = find_all_lines(rel, pattern) or [find_line(rel, pattern)]
    name = Path(rel).name
    shown = label or pattern
    return ' / '.join(f'[`{shown}`](LandAgencyMvc/{rel}#L{n})' for n in lines) if len(lines) > 1 else f'[`{shown}`](LandAgencyMvc/{rel}#L{lines[0]})'


def view_link(rel):
    if not (SRC / rel).exists():
        raise SystemExit(f'немає виду {rel}')
    return f'[{Path(rel).name}](LandAgencyMvc/{rel})'


def use_case_table():
    rows = ['| № | Прецедент | Актор | Зв\'язок | Дії (контролер, сервіс) | Види | Модель, домен |', '|---|---|---|---|---|---|---|']
    for uc, title, actor, rel, actions, views, models in USE_CASES:
        a = '<br>'.join(link(f, m) for f, m in actions) or '—'
        v = '<br>'.join(view_link(x) for x in views) or '—'
        m = '<br>'.join(link(f, p) for f, p in models) or '—'
        rows.append(f'| **{uc}** | {title} | {actor} | {rel or "—"} | {a} | {v} | {m} |')
    return '\n'.join(rows)


def crud_table():
    rows = ['| Таблиця | Create | Read | Update | Delete | Код |', '|---|---|---|---|---|---|']
    for table, c, r, u, d, refs in CRUD:
        code = '<br>'.join(link(f, m) for f, m in refs)
        rows.append(f'| `{table}` | {c} | {r} | {u} | {d} | {code} |')
    return '\n'.join(rows)


def acid_table():
    rows = ['| Властивість | Механізм | Де в коді |', '|---|---|---|']
    for prop, what, pattern, rel in ACID:
        if rel is None:
            hits = []
            for p in sorted(SRC.rglob('*.cs')):
                if any(x in p.parts for x in ('obj', 'bin')):
                    continue
                for i, line in enumerate(p.read_text(encoding='utf-8').split('\n'), 1):
                    if pattern in line:
                        hits.append(f'[{p.name}:{i}](LandAgencyMvc/{p.relative_to(SRC).as_posix()}#L{i})')
            where = ', '.join(hits)
        else:
            n = find_line(rel, pattern)
            where = f'[{Path(rel).name}:{n}](LandAgencyMvc/{rel}#L{n})'
        rows.append(f'| {prop} | {what} | {where} |')
    return '\n'.join(rows)


BLOCKS = {'use-case-map': use_case_table, 'crud-matrix': crud_table, 'acid-map': acid_table}


def write_into(guide_path):
    text = Path(guide_path).read_text(encoding='utf-8')
    for name, builder in BLOCKS.items():
        start, end = f'<!-- {name}:start -->', f'<!-- {name}:end -->'
        pattern = re.compile(re.escape(start) + '.*?' + re.escape(end), re.S)
        if not pattern.search(text):
            raise SystemExit(f'у {guide_path} немає маркерів {name}')
        text = pattern.sub(lambda m: f'{start}\n{builder()}\n{end}', text)
    Path(guide_path).write_text(text, encoding='utf-8')


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--write':
        write_into(sys.argv[2])
        print('оновлено', sys.argv[2])
    else:
        for name, builder in BLOCKS.items():
            print(f'### {name}\n{builder()}\n')
