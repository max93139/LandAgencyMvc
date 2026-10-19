using System.Security.Claims;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Єдиний клієнтський контролер (модель взаємодії 'Покупець / Продавець' за стандартом платформи OLX).
/// Забезпечує перегляд каталогу ділянок, подачу заявок на огляд, автоматизований підбір,
/// публікацію власних ділянок на продаж з верифікацією КОАТУУ та подання платіжних реквізитів.
/// </summary>
public class ClientController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ICadastralValidationService _cadastralService;
    private readonly IAutoMatchingService _autoMatchingService;

    public ClientController(
        LandAgencyContext context,
        IWebHostEnvironment env,
        ICadastralValidationService cadastralService,
        IAutoMatchingService autoMatchingService)
    {
        _db = context;
        _env = env;
        _cadastralService = cadastralService;
        _autoMatchingService = autoMatchingService;
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var userId) && userId > 0)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                ViewBag.CurrentUser = user;
                ViewBag.CurrentUserId = user.Id;
                return user;
            }
        }

        ViewBag.CurrentUser = null;
        ViewBag.CurrentUserId = 0;
        return null;
    }

    /// <summary>
    /// Каталог перевірених земельних ділянок, схвалених юридичним аудитом ДЗК/ДРРП.
    /// Підтримує фільтрацію за кодом КВЦПЗ, максимальним бюджетом, мінімальною площею та текстом.
    /// </summary>
    [HttpGet("/Catalog")]
    [HttpGet("/Client/Catalog")]
    public async Task<IActionResult> Catalog(string? purpose, decimal? maxBudget, double? minArea, string? search)
    {
        await GetCurrentUserAsync();

        var query = _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .Include(p => p.LegalConclusion)
            .Include(p => p.Documents)
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .AsQueryable();

        if (maxBudget.HasValue && maxBudget.Value > 0)
        {
            query = query.Where(p => p.PriceUsd <= maxBudget.Value);
        }

        if (minArea.HasValue && minArea.Value > 0)
        {
            query = query.Where(p => p.AreaHectares >= minArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            var cleanPurpose = purpose.Trim();
            query = query.Where(p => p.Purpose.Contains(cleanPurpose));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(cleanSearch) ||
                p.Location.ToLower().Contains(cleanSearch) ||
                p.CadastralNumber.ToLower().Contains(cleanSearch) ||
                p.Description.ToLower().Contains(cleanSearch));
        }

        var plots = await query.OrderByDescending(p => p.Id).ToListAsync();

        ViewBag.FilterPurpose = purpose;
        ViewBag.FilterMaxBudget = maxBudget;
        ViewBag.FilterMinArea = minArea;
        ViewBag.FilterSearch = search;
        ViewBag.TotalCount = plots.Count;

        return View(plots);
    }

    /// <summary>
    /// Створює заявку покупця на індивідуальний підбір ділянок рієлтором
    /// та негайно запускає автоматичний двигун підбору (AutoMatchingService).
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitSelectionRequest(
        string desiredDistrict,
        double minAreaHa,
        decimal maxBudgetUsd,
        string? purpose)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (string.IsNullOrWhiteSpace(desiredDistrict) || maxBudgetUsd <= 0)
        {
            TempData["CabinetError"] = "Вкажіть бажаний район та максимальний бюджет для підбору ділянок.";
            return RedirectToAction(nameof(Cabinet));
        }

        currentUser.MaxBudget = (double)maxBudgetUsd;
        currentUser.TargetPurpose = (purpose ?? string.Empty).Trim();

        var request = new SelectionRequest
        {
            BuyerId = currentUser.Id,
            DesiredDistrict = desiredDistrict.Trim(),
            MinAreaHa = minAreaHa,
            MaxBudgetUsd = maxBudgetUsd,
            Purpose = currentUser.TargetPurpose,
            IsProcessedByRealtor = false,
            CreatedAt = DateTime.Now
        };

        _db.SelectionRequests.Add(request);
        await _db.SaveChangesAsync();

        int matchedCount = await _autoMatchingService.MatchRequestAsync(request.Id);

        TempData["CabinetMessage"] = $"Запит на підбір ділянки створено. Автоматично знайдено {matchedCount} відповідних об'єктів у каталозі!";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Сторінка картки земельної ділянки з переліком документів, юридичним висновком та формою огляду.
    /// </summary>
    [HttpGet("/Catalog/Details/{id:int}")]
    [HttpGet("/Client/Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        await GetCurrentUserAsync();
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .ThenInclude(c => c!.Lawyer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return RedirectToAction(nameof(Catalog));
        }

        ViewBag.KadastrLiveUrl = _cadastralService.GetKadastrLiveUrl(plot.CadastralNumber);
        ViewBag.OpenDataBotUrl = _cadastralService.GetOpenDataBotUrl(plot.CadastralNumber);

        return View(plot);
    }

    /// <summary>
    /// Оформлює заявку на виїзний огляд обраної ділянки із зазначенням бажаної дати та таймслоту.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateViewing(int landPlotId, DateTime preferredDate, string timeSlot, string comment)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == landPlotId);
        if (plot == null || plot.Status != PlotStatus.VerifiedInCatalog)
        {
            TempData["CabinetError"] = "Обрана земельна ділянка не знайдена або недоступна для виїзного огляду.";
            return RedirectToAction(nameof(Catalog));
        }

        if (plot.SellerId == currentUser.Id)
        {
            TempData["DetailsError"] = "Ви є власником цієї ділянки і не можете подати заявку на її огляд як покупець.";
            return RedirectToAction(nameof(Details), new { id = landPlotId });
        }

        var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);

        var viewing = new ViewingRequest
        {
            LandPlotId = landPlotId,
            BuyerId = currentUser.Id,
            RealtorId = plot.AssignedRealtorId ?? realtor?.Id,
            PreferredDate = preferredDate == default ? DateTime.Today.AddDays(1) : preferredDate,
            TimeSlot = (timeSlot ?? "12:00").Trim(),
            Comment = (comment ?? string.Empty).Trim(),
            Status = ViewingStatus.NewRequest
        };

        _db.ViewingRequests.Add(viewing);
        await _db.SaveChangesAsync();

        TempData["CabinetMessage"] = "Заявку на виїзний огляд ділянки створено та передано рієлтору.";
        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CancelViewingByClient(int viewingId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var viewing = await _db.ViewingRequests.FirstOrDefaultAsync(v => v.Id == viewingId && v.BuyerId == currentUserId);
        if (viewing != null)
        {
            viewing.CancelViewing("Скасовано покупцем з особистого кабінету");
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Заявку на огляд #{viewingId} скасовано.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RequestPurchaseFromViewing(int viewingId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var viewing = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .ThenInclude(p => p!.Seller)
            .FirstOrDefaultAsync(v => v.Id == viewingId && v.BuyerId == currentUserId);

        if (viewing != null && viewing.LandPlot != null)
        {
            if (viewing.LandPlot.SellerId == viewing.BuyerId)
            {
                TempData["CabinetError"] = "Власник ділянки не може укласти угоду купівлі власного об'єкта.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (viewing.LandPlot.Status == PlotStatus.Sold)
            {
                TempData["CabinetError"] = "Ця земельна ділянка вже продана.";
                return RedirectToAction(nameof(Cabinet));
            }

            var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);

            var existingDeal = await _db.SaleDeals
                .Include(d => d.Payments)
                .FirstOrDefaultAsync(d => d.LandPlotId == viewing.LandPlotId);

            if (existingDeal == null)
            {
                var deal = new SaleDeal
                {
                    LandPlotId = viewing.LandPlotId,
                    BuyerId = viewing.BuyerId,
                    RealtorId = viewing.RealtorId ?? realtor?.Id,
                    NotaryOffice = "Перша Київська державна нотаріальна контора",
                    NotaryDate = DateTime.Today.AddDays(3).AddHours(11)
                };

                deal.PrepareContractDraft(
                    $"ДГ-{DateTime.Now:yyyy}-{viewing.LandPlotId:D3}",
                    viewing.LandPlot.PriceUsd,
                    realtor?.CommissionRate ?? 3.0);

                _db.SaleDeals.Add(deal);
                await _db.SaveChangesAsync();

                _db.Payments.AddRange(deal.CreateDefaultPayments(
                    viewing.LandPlot.SellerBankIban ?? viewing.LandPlot.Seller?.BankDetails,
                    isPaid: false,
                    senderIban: currentUser?.BankDetails,
                    agencyIban: realtor?.BankDetails));
                await _db.SaveChangesAsync();

                TempData["CabinetMessage"] = $"Проєкт нотаріальної угоди {deal.DraftContractNumber} успішно ініційовано на підставі проведеного огляду.";
            }
            else
            {
                TempData["CabinetMessage"] = $"По ділянці {viewing.LandPlot.CadastralNumber} вже існує проект угоди {existingDeal.DraftContractNumber}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpGet("/Plots/Create")]
    [HttpGet("/Client/CreatePlot")]
    public async Task<IActionResult> CreatePlot()
    {
        var currentUser = await GetCurrentUserAsync();
        var model = new LandPlot
        {
            SellerBankIban = currentUser?.BankDetails ?? string.Empty
        };
        return View(model);
    }

    /// <summary>
    /// Реєстрація ділянки продавцем: валідація КОАТУУ, завантаження документа з SHA-256 хешуванням,
    /// накладення електронного підпису та передача в чергу аудиту ДЗК/ДРРП.
    /// </summary>
    [Authorize]
    [HttpPost("/Plots/Create")]
    [HttpPost("/Client/CreatePlot")]
    public async Task<IActionResult> CreatePlot(
        LandPlot model,
        string? docTitle,
        string? docType,
        DateTime? docIssueDate,
        bool signWithKep = false,
        IFormFile? documentFile = null)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (Request.HasFormContentType && Request.Form.TryGetValue("AreaHectares", out var rawArea))
        {
            var normalizedArea = rawArea.ToString().Replace(',', '.').Trim();
            if (double.TryParse(normalizedArea, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedArea) && parsedArea > 0)
            {
                model.AreaHectares = parsedArea;
            }
        }

        model.CadastralNumber = (model.CadastralNumber ?? string.Empty).Trim();
        model.Title = (model.Title ?? string.Empty).Trim();
        model.Location = (model.Location ?? string.Empty).Trim();
        model.Purpose = (model.Purpose ?? string.Empty).Trim();
        model.OwnershipDocNumber = (model.OwnershipDocNumber ?? string.Empty).Trim();
        model.SellerBankIban = (model.SellerBankIban ?? string.Empty).Trim();
        model.Description = (model.Description ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(model.CadastralNumber) ||
            string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Location) ||
            model.AreaHectares <= 0 ||
            model.PriceUsd <= 0)
        {
            TempData["PlotError"] = "Заповніть кадастровий номер, назву, адресу, площу (> 0 га) та ціну (> $0).";
            return View(model);
        }

        var koatuu = _cadastralService.ValidateKoatuuFormat(model.CadastralNumber);
        if (!koatuu.IsValid)
        {
            TempData["PlotError"] = $"Помилка валідації кадастрового номера: {koatuu.ErrorMessage}";
            return View(model);
        }

        var ibanClean = model.SellerBankIban.ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(ibanClean, @"^UA\d{27}$"))
        {
            TempData["PlotError"] = "Невірний номер рахунку IBAN. Формат банківського рахунку України має складати рівно 29 символів (літери UA та 27 цифр).";
            return View(model);
        }
        model.SellerBankIban = ibanClean;

        bool duplicateCadastral = await _db.LandPlots.AnyAsync(p =>
            p.CadastralNumber == model.CadastralNumber &&
            p.Status != PlotStatus.RejectedByLawyer);
        if (duplicateCadastral)
        {
            TempData["PlotError"] = $"Ділянка з кадастровим номером {model.CadastralNumber} вже зареєстрована в системі.";
            return View(model);
        }

        var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);

        model.SellerId = currentUser.Id;
        model.AssignedRealtorId = realtor?.Id;
        model.Status = PlotStatus.PendingLegalCheck;
        model.CreatedAt = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(model.SellerBankIban) && string.IsNullOrWhiteSpace(currentUser.BankDetails))
        {
            currentUser.BankDetails = model.SellerBankIban;
        }
        if (!string.IsNullOrWhiteSpace(model.OwnershipDocNumber))
        {
            currentUser.OwnershipDoc = model.OwnershipDocNumber;
        }

        _db.LandPlots.Add(model);
        await _db.SaveChangesAsync();

        var (savedFileUrl, fileBytes, mimeType) = await SaveUploadedDocumentAsync(documentFile, model.Id, model.OwnershipDocNumber);

        var plotDoc = new PlotDocument
        {
            LandPlotId = model.Id,
            Title = !string.IsNullOrWhiteSpace(docTitle)
                ? docTitle.Trim()
                : $"Правовстановлюючий документ ({model.OwnershipDocNumber})",
            DocType = !string.IsNullOrWhiteSpace(docType)
                ? docType.Trim()
                : "Витяг з ДРРП про реєстрацію права власності",
            DocumentNumber = model.OwnershipDocNumber,
            IssueDate = docIssueDate ?? DateTime.Today,
            IsVerified = false
        };
        plotDoc.AttachFile(savedFileUrl, fileBytes, mimeType);

        if (signWithKep)
        {
            plotDoc.ApplyKepSignature(currentUser.FullName, currentUser.TaxId, fileBytes);
        }

        _db.PlotDocuments.Add(plotDoc);
        await _db.SaveChangesAsync();

        TempData["CabinetMessage"] = $"Ділянку {model.CadastralNumber} ({koatuu.RegionName}) успішно подано на модерацію. Документ зафіксовано з контрольною сумою SHA-256.";
        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UpdatePlotPrice(int plotId, decimal newPriceUsd)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);
        if (plot != null && newPriceUsd > 0)
        {
            plot.UpdatePrice(newPriceUsd);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Ціну ділянки {plot.CadastralNumber} оновлено до ${newPriceUsd:N0}.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddPlotDocument(
        int plotId,
        string title,
        string docType,
        string documentNumber,
        DateTime? issueDate,
        bool signWithKep = false,
        IFormFile? documentFile = null)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);
        if (plot != null && currentUser != null)
        {
            var (savedFileUrl, fileBytes, mimeType) = await SaveUploadedDocumentAsync(documentFile, plot.Id, documentNumber);
            var doc = new PlotDocument
            {
                LandPlotId = plot.Id,
                Title = !string.IsNullOrWhiteSpace(title) ? title.Trim() : "Документ земельної ділянки",
                DocType = !string.IsNullOrWhiteSpace(docType) ? docType.Trim() : "Витяг з ДЗК",
                DocumentNumber = (documentNumber ?? string.Empty).Trim(),
                IssueDate = issueDate ?? DateTime.Today,
                IsVerified = plot.Status == PlotStatus.VerifiedInCatalog
            };
            doc.AttachFile(savedFileUrl, fileBytes, mimeType);

            if (signWithKep)
            {
                doc.ApplyKepSignature(currentUser.FullName, currentUser.TaxId, fileBytes);
            }

            _db.PlotDocuments.Add(doc);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Документ «{doc.Title}» {(signWithKep ? "підписано КЕП та " : "")}прикріплено до ділянки {plot.CadastralNumber}.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Подання клієнтом реквізитів банківської оплати (код квитанції, IBAN) за виставленим рахунком-фактурою.
    /// Переводить платіж у статус очікування підтвердження виписки агентством.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitPayment(int paymentId, string paymentReference, string? senderIban)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && payment.SaleDeal != null && payment.SaleDeal.BuyerId == currentUserId)
        {
            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                TempData["CabinetError"] = "Вкажіть номер платіжної інструкції або код квитанції банку.";
                return RedirectToAction(nameof(Cabinet));
            }

            payment.SubmitPayment(paymentReference, senderIban ?? currentUser?.BankDetails);
            await _db.SaveChangesAsync();

            TempData["CabinetMessage"] = $"Реквізити оплати за рахунком {payment.InvoiceNumber} (транзакція {payment.PaymentReference}) надіслано на верифікацію банківської виписки рієлтору.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Альтернативний обробник для зворотної сумісності форми кабінету.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ConfirmPayment(int dealId, string? payerIban, string? paymentReference)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.BuyerId == currentUserId);

        if (deal != null)
        {
            string reference = !string.IsNullOrWhiteSpace(paymentReference)
                ? paymentReference.Trim()
                : $"TRX-{DateTime.Now:yyyyMMdd}-{deal.Id:D4}";

            var pendingPayment = deal.Payments.FirstOrDefault(p => !p.IsPaid) ?? deal.Payments.FirstOrDefault();
            if (pendingPayment != null)
            {
                pendingPayment.SubmitPayment(reference, payerIban ?? currentUser?.BankDetails);
                await _db.SaveChangesAsync();
                TempData["CabinetMessage"] = $"Повідомлення про оплату за договором {deal.DraftContractNumber} передано рієлтору. Код транзакції: {reference}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(int dealId)
    {
        await GetCurrentUserAsync();
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        return View("Receipt", deal);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string? bankDetails, string? taxId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser != null)
        {
            if (!string.IsNullOrWhiteSpace(bankDetails))
            {
                currentUser.BankDetails = bankDetails.Trim();
            }

            if (!string.IsNullOrWhiteSpace(taxId))
            {
                currentUser.TaxId = taxId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (!currentUser.ChangePassword(currentPassword, newPassword))
                {
                    TempData["CabinetError"] = "Невірний поточний пароль. Пароль не змінено.";
                    return RedirectToAction(nameof(Cabinet));
                }
            }

            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = "Налаштування профілю та реквізити успішно оновлено.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpGet("/Cabinet")]
    [HttpGet("/Client/Cabinet")]
    public async Task<IActionResult> Cabinet()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        int activeUserId = currentUser.Id;

        var mySelectionRequests = await _db.SelectionRequests
            .Where(r => r.BuyerId == activeUserId)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        var matchedIds = mySelectionRequests
            .Where(r => !string.IsNullOrWhiteSpace(r.MatchedPlotIds))
            .SelectMany(r => r.MatchedPlotIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(s => int.TryParse(s, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var matchedPlots = matchedIds.Any()
            ? await _db.LandPlots.Where(p => matchedIds.Contains(p.Id)).ToListAsync()
            : new List<LandPlot>();

        var viewModel = new CabinetViewModel
        {
            CurrentUser = currentUser,
            MyPlots = await _db.LandPlots
                .Include(p => p.Documents)
                .Include(p => p.LegalConclusion)
                .Where(p => p.SellerId == activeUserId)
                .OrderByDescending(p => p.Id)
                .ToListAsync(),
            MySelectionRequests = mySelectionRequests,
            MatchedPlotsForUser = matchedPlots,
            MyViewings = await _db.ViewingRequests
                .Include(v => v.LandPlot)
                .Include(v => v.Realtor)
                .Where(v => v.BuyerId == activeUserId)
                .OrderByDescending(v => v.Id)
                .ToListAsync(),
            MyDeals = await _db.SaleDeals
                .Include(d => d.LandPlot)
                .ThenInclude(p => p!.Seller)
                .Include(d => d.Buyer)
                .Include(d => d.Payments)
                .Where(d => d.BuyerId == activeUserId)
                .OrderByDescending(d => d.Id)
                .ToListAsync(),
            MySellerDeals = await _db.SaleDeals
                .Include(d => d.LandPlot)
                .ThenInclude(p => p!.Seller)
                .Include(d => d.Buyer)
                .Include(d => d.Payments)
                .Where(d => d.LandPlot != null && d.LandPlot.SellerId == activeUserId && d.BuyerId != activeUserId)
                .OrderByDescending(d => d.Id)
                .ToListAsync()
        };

        return View(viewModel);
    }

    private async Task<(string fileUrl, byte[] fileBytes, string mimeType)> SaveUploadedDocumentAsync(
        IFormFile? file,
        int plotId,
        string? docNum)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        if (file != null && file.Length > 0)
        {
            var rawExt = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".txt", ".p7s" };
            var safeExt = !string.IsNullOrWhiteSpace(rawExt) && allowedExts.Contains(rawExt) ? rawExt : ".pdf";

            var fileName = $"plot_{plotId}_{DateTime.Now:yyyyMMddHHmmss}{safeExt}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            await System.IO.File.WriteAllBytesAsync(fullPath, bytes);
            string mime = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType;

            return ($"/uploads/{fileName}", bytes, mime);
        }

        var generatedFileName = $"extract_drrp_plot_{plotId}.txt";
        var generatedPath = Path.Combine(uploadsFolder, generatedFileName);
        string textContent = $"ВИТЯГ З ДЕРЖАВНОГО РЕЄСТРУ РЕЧОВИХ ПРАВ НА НЕРУХОМЕ МАЙНО\nДокумент №: {docNum}\nID ділянки: {plotId}\nДата формування: {DateTime.Now:dd.MM.yyyy HH:mm}\nСтатус КЕП: Дійсний кваліфікований електронний підпис власника.";
        var defaultBytes = System.Text.Encoding.UTF8.GetBytes(textContent);

        if (!System.IO.File.Exists(generatedPath))
        {
            await System.IO.File.WriteAllBytesAsync(generatedPath, defaultBytes);
        }

        return ($"/uploads/{generatedFileName}", defaultBytes, "text/plain");
    }
}
