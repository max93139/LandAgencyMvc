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
[Authorize]
public class ClientController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ICadastralValidationService _cadastralService;
    private readonly IAutoMatchingService _autoMatchingService;
    private readonly IPlotRemovalService _plotRemoval;

    public ClientController(
        LandAgencyContext context,
        IWebHostEnvironment env,
        ICadastralValidationService cadastralService,
        IAutoMatchingService autoMatchingService,
        IPlotRemovalService plotRemoval)
    {
        _db = context;
        _env = env;
        _cadastralService = cadastralService;
        _autoMatchingService = autoMatchingService;
        _plotRemoval = plotRemoval;
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var userId) && userId > 0)
        {
            return await _db.Users.FindAsync(userId);
        }

        return null;
    }

    /// <summary>
    /// Каталог перевірених земельних ділянок, схвалених юридичним аудитом ДЗК/ДРРП.
    /// Підтримує відбір за довільним поєднанням критеріїв (призначення, діапазон бюджету,
    /// площа, дати публікації, текстовий пошук) та багатоваріантне впорядкування (LINQ to Entities + LINQ to Objects).
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpGet("/Catalog")]
    [HttpGet("/Client/Catalog")]
    public async Task<IActionResult> Catalog(
        string? purpose,
        decimal? minBudget,
        decimal? maxBudget,
        double? minArea,
        double? maxArea,
        string? search,
        DateOnly? from,
        DateOnly? to,
        PlotSort sort = PlotSort.Date,
        bool desc = true)
    {
        var query = _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .Include(p => p.LegalConclusion)
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .AsNoTracking();

        if (minBudget.HasValue && minBudget.Value > 0)
        {
            query = query.Where(p => p.PriceUsd >= minBudget.Value);
        }

        if (maxBudget.HasValue && maxBudget.Value > 0)
        {
            query = query.Where(p => p.PriceUsd <= maxBudget.Value);
        }

        if (minArea.HasValue && minArea.Value > 0)
        {
            query = query.Where(p => p.AreaHectares >= minArea.Value);
        }

        if (maxArea.HasValue && maxArea.Value > 0)
        {
            query = query.Where(p => p.AreaHectares <= maxArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            var cleanPurpose = purpose.Trim();
            query = query.Where(p => p.Purpose.Contains(cleanPurpose));
        }

        if (from.HasValue)
        {
            var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt >= fromDt);
        }

        if (to.HasValue)
        {
            var toDt = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt < toDt);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var textCap = char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
            var textLower = char.ToLower(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
            query = query.Where(p => p.Title.Contains(text) || p.Title.Contains(textCap) || p.Title.Contains(textLower)
                                  || p.Location.Contains(text) || p.Location.Contains(textCap) || p.Location.Contains(textLower)
                                  || p.CadastralNumber.Contains(text)
                                  || (p.Seller != null && (p.Seller.FullName.Contains(text) || p.Seller.FullName.Contains(textCap) || p.Seller.FullName.Contains(textLower))));
        }

        query = sort switch
        {
            PlotSort.Area => desc ? query.OrderByDescending(p => p.AreaHectares).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.AreaHectares).ThenBy(p => p.CreatedAt),
            PlotSort.Title => desc ? query.OrderByDescending(p => p.Title).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Title).ThenBy(p => p.CreatedAt),
            PlotSort.Location => desc ? query.OrderByDescending(p => p.Location).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Location).ThenBy(p => p.CreatedAt),
            PlotSort.Price or PlotSort.PricePerSotka => query,
            _ => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var plots = await query.ToListAsync();

        if (sort == PlotSort.Price)
        {
            plots = desc ? plots.OrderByDescending(p => p.PriceUsd).ThenByDescending(p => p.CreatedAt).ToList()
                         : plots.OrderBy(p => p.PriceUsd).ThenBy(p => p.CreatedAt).ToList();
        }
        else if (sort == PlotSort.PricePerSotka)
        {
            plots = desc ? plots.OrderByDescending(p => p.AreaHectares > 0 ? p.PriceUsd / (decimal)(p.AreaHectares * 100.0) : 0m).ThenByDescending(p => p.CreatedAt).ToList()
                         : plots.OrderBy(p => p.AreaHectares > 0 ? p.PriceUsd / (decimal)(p.AreaHectares * 100.0) : 0m).ThenBy(p => p.CreatedAt).ToList();
        }

        var viewModel = new CatalogViewModel
        {
            Plots = plots,
            FilterPurpose = purpose,
            FilterMinBudget = minBudget,
            FilterMaxBudget = maxBudget,
            FilterMinArea = minArea,
            FilterMaxArea = maxArea,
            FilterSearch = search,
            FilterFrom = from,
            FilterTo = to,
            Sort = sort,
            Descending = desc
        };

        return View(viewModel);
    }

    /// <summary>
    /// Створює заявку покупця на індивідуальний підбір ділянок рієлтором
    /// та негайно запускає автоматичний двигун підбору (AutoMatchingService).
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
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

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.SelectionRequests.Add(request);
        await _db.SaveChangesAsync();

        int matchedCount = await _autoMatchingService.MatchRequestAsync(request.Id);
        await transaction.CommitAsync();

        TempData["CabinetMessage"] = $"Запит на підбір ділянки створено. Автоматично знайдено {matchedCount} відповідних об'єктів у каталозі!";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Змінює параметри власного запиту на підбір і одразу перераховує збіги.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSelectionRequestByClient(
        int requestId,
        string desiredDistrict,
        double minAreaHa,
        decimal maxBudgetUsd,
        string? purpose)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var request = await _db.SelectionRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.BuyerId == currentUserId);
        if (request == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (string.IsNullOrWhiteSpace(desiredDistrict) || maxBudgetUsd <= 0)
        {
            TempData["CabinetError"] = "Вкажіть бажаний район та максимальний бюджет для підбору ділянок.";
            return RedirectToAction(nameof(Cabinet));
        }

        request.DesiredDistrict = desiredDistrict.Trim();
        request.MinAreaHa = Math.Max(minAreaHa, 0);
        request.MaxBudgetUsd = maxBudgetUsd;
        request.Purpose = (purpose ?? string.Empty).Trim();

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        int matchedCount = await _autoMatchingService.MatchRequestAsync(request.Id);
        await transaction.CommitAsync();

        TempData["CabinetMessage"] = $"Запит на підбір оновлено. Знайдено {matchedCount} відповідних об'єктів.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Вилучення власного запиту на підбір.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelectionRequestByClient(int requestId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var request = await _db.SelectionRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.BuyerId == currentUserId);
        if (request != null)
        {
            _db.SelectionRequests.Remove(request);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = "Запит на підбір вилучено.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Сторінка картки земельної ділянки з переліком документів, юридичним висновком та формою огляду.
    /// </summary>
    [HttpGet("/Catalog/Details/{id:int}")]
    [HttpGet("/Client/Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .Include(p => p.LegalConclusion)
            .ThenInclude(c => c!.Lawyer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return RedirectToAction(nameof(Catalog));
        }

        bool isOwnerOrStaff = currentUser != null &&
                              (currentUser.Id == plot.SellerId ||
                               currentUser.Role == UserRole.Realtor ||
                               currentUser.Role == UserRole.Lawyer);

        if (plot.Status != PlotStatus.VerifiedInCatalog && !isOwnerOrStaff)
        {
            TempData["CabinetError"] = "Обрана земельна ділянка ще проходить юридичну перевірку або вже знята з публікації.";
            return RedirectToAction(nameof(Catalog));
        }

        return View(plot);
    }

    /// <summary>
    /// Оформлює заявку на виїзний огляд обраної ділянки із зазначенням бажаної дати та таймслоту.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
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

    /// <summary>
    /// UC-04 «Перенести дату огляду» (extend до UC-03 «Замовити виїзний огляд ділянки»).
    /// Покупець змінює дату й час власного огляду; заявка повертається рієлтору на повторне підтвердження.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleViewingByClient(int viewingId, DateTime newDate, string newTimeSlot)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var viewing = await _db.ViewingRequests.FirstOrDefaultAsync(v => v.Id == viewingId && v.BuyerId == currentUserId);
        if (viewing == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (newDate.Date < DateTime.Today || string.IsNullOrWhiteSpace(newTimeSlot))
        {
            TempData["CabinetError"] = "Вкажіть дату огляду не раніше сьогоднішньої та бажаний час.";
            return RedirectToAction(nameof(Cabinet));
        }

        if (!viewing.Reschedule(newDate, newTimeSlot))
        {
            TempData["CabinetError"] = $"Огляд у статусі «{viewing.StatusDisplayName}» перенести неможливо.";
            return RedirectToAction(nameof(Cabinet));
        }

        await _db.SaveChangesAsync();
        TempData["CabinetMessage"] = $"Огляд #{viewingId} перенесено на {viewing.PreferredDate:dd.MM.yyyy} о {viewing.TimeSlot}. Рієлтор підтвердить нову дату.";
        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
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
                .FirstOrDefaultAsync(d => d.LandPlotId == viewing.LandPlotId && d.Status != DealStatus.Cancelled);

            if (existingDeal == null)
            {
                int previousDeals = await _db.SaleDeals.CountAsync(d => d.LandPlotId == viewing.LandPlotId);
                string contractNumber = $"ДГ-{DateTime.Now:yyyy}-{viewing.LandPlotId:D3}" + (previousDeals > 0 ? $"-{previousDeals + 1}" : string.Empty);

                await using var transaction = await _db.Database.BeginTransactionAsync();

                var deal = new SaleDeal
                {
                    LandPlotId = viewing.LandPlotId,
                    BuyerId = viewing.BuyerId,
                    RealtorId = viewing.RealtorId ?? realtor?.Id,
                    NotaryOffice = "Перша Київська державна нотаріальна контора",
                    NotaryDate = DateTime.Today.AddDays(3).AddHours(11)
                };

                deal.PrepareContractDraft(
                    contractNumber,
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
                await transaction.CommitAsync();

                TempData["CabinetMessage"] = $"Проєкт нотаріальної угоди {deal.DraftContractNumber} успішно ініційовано на підставі проведеного огляду.";
            }
            else
            {
                TempData["CabinetMessage"] = $"По ділянці {viewing.LandPlot.CadastralNumber} вже існує проект угоди {existingDeal.DraftContractNumber}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize(Roles = AppRoles.Seller)]
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
    /// <summary>
    /// UC-08 «Зареєструвати ділянку на продаж» (include UC-09 «Перевірити кадастровий номер та реєстрацію в ДЗК»).
    /// Продавець подає ділянку з правовстановлюючим документом. Кадастровий номер перевіряється за форматом і кодом області,
    /// після збереження виконується попередній запит до ДЗК/ДРРП, а остаточне рішення про публікацію ухвалює юрист.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpPost("/Plots/Create")]
    [HttpPost("/Client/CreatePlot")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlot(
        LandPlot model,
        string? docTitle,
        string? docType,
        DateTime? docIssueDate,
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
        model.UpdateSaleConditions(model.SaleConditions);

        // Координати вносить рієлтор, тому значення з форми продавця ігноруються.
        model.Latitude = null;
        model.Longitude = null;

        if (!string.IsNullOrWhiteSpace(model.SellerBankIban) && string.IsNullOrWhiteSpace(currentUser.BankDetails))
        {
            currentUser.BankDetails = model.SellerBankIban;
        }
        if (!string.IsNullOrWhiteSpace(model.OwnershipDocNumber))
        {
            currentUser.OwnershipDoc = model.OwnershipDocNumber;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

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

        try
        {
            _db.PlotDocuments.Add(plotDoc);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            DeleteUploadedFile(savedFileUrl);
            throw;
        }

        var registryExtract = await DzkRegistrySimulator.QueryRegistriesAsync(model, _cadastralService);

        TempData["CabinetMessage"] = $"Ділянку {model.CadastralNumber} ({koatuu.RegionName}) подано на модерацію. " +
                                     $"Попередній запит до ДЗК/ДРРП (витяг {registryExtract.ExtractNumber}): {registryExtract.RecommendedRiskLevel}. " +
                                     "Документ зафіксовано з контрольною сумою SHA-256.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-10 «Встановити ціну та умови продажу»: продавець змінює ціну й умови продажу власної ділянки,
    /// доки за нею немає підписаного договору.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlotTerms(int plotId, decimal newPriceUsd, string? saleConditions)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);
        if (plot != null)
        {
            if (newPriceUsd <= 0)
            {
                TempData["CabinetError"] = "Вкажіть коректну ціну ділянки (> $0).";
                return RedirectToAction(nameof(Cabinet));
            }

            var editBlock = await GetEditBlockReasonAsync(plot);
            if (editBlock != null)
            {
                TempData["CabinetError"] = editBlock;
                return RedirectToAction(nameof(Cabinet));
            }

            plot.UpdatePrice(newPriceUsd);
            plot.UpdateSaleConditions(saleConditions);
            await _db.SaveChangesAsync();

            await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);

            TempData["CabinetMessage"] = $"Ціну й умови продажу ділянки {plot.CadastralNumber} оновлено: ${newPriceUsd:N0}.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Причина, з якої продавець не може змінювати чи вилучати ділянку, або null, якщо може: ділянку продано
    /// або за нею вже підписано договір у нотаріуса.
    /// </summary>
    private async Task<string?> GetEditBlockReasonAsync(LandPlot plot)
    {
        if (plot.Status == PlotStatus.Sold)
        {
            return $"Ділянку {plot.CadastralNumber} вже продано, зміни неможливі.";
        }

        bool hasSignedDeal = await _db.SaleDeals.AnyAsync(d =>
            d.LandPlotId == plot.Id &&
            (d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary));

        return hasSignedDeal
            ? $"По ділянці {plot.CadastralNumber} договір вже підписано у нотаріуса, зміни неможливі."
            : null;
    }

    /// <summary>
    /// Форма редагування власного оголошення продавцем: назва, адреса, ціна, IBAN, опис і умови продажу.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpGet]
    public async Task<IActionResult> EditOwnPlot(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == currentUserId);
        if (plot == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        var editBlock = await GetEditBlockReasonAsync(plot);
        if (editBlock != null)
        {
            TempData["CabinetError"] = editBlock;
            return RedirectToAction(nameof(Cabinet));
        }

        return View(SellerPlotEditViewModel.From(plot));
    }

    /// <summary>
    /// Зберігає зміни власного оголошення. Якщо ділянку змінили в проміжку (інший користувач), збереження відхиляється.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditOwnPlot(int id, SellerPlotEditViewModel vm)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == currentUserId);
        if (plot == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        var editBlock = await GetEditBlockReasonAsync(plot);
        if (editBlock != null)
        {
            TempData["CabinetError"] = editBlock;
            return RedirectToAction(nameof(Cabinet));
        }

        if (!ModelState.IsValid)
        {
            vm.ShowReadOnlyFieldsOf(plot);
            return View(vm);
        }

        _db.UseSubmittedVersion(plot, vm.Version);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        vm.ApplyTo(plot);
        await _db.SaveChangesAsync();
        await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);
        await transaction.CommitAsync();

        TempData["CabinetMessage"] = $"Оголошення {plot.CadastralNumber} оновлено.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Вилучення власного оголошення разом із документами, оглядами та юридичним висновком.
    /// Неможливе, якщо за ділянкою є угода (навіть скасована) або активні заявки на огляд.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOwnPlot(int plotId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .Include(p => p.ViewingRequests)
            .Include(p => p.LegalConclusion)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);

        if (plot == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (await _db.SaleDeals.AnyAsync(d => d.LandPlotId == plot.Id))
        {
            TempData["CabinetError"] = $"Ділянку {plot.CadastralNumber} не можна вилучити: за нею є угода, яку агентство зберігає в історії.";
            return RedirectToAction(nameof(Cabinet));
        }

        if (plot.ViewingRequests.Any(v => v.Status is ViewingStatus.NewRequest or ViewingStatus.Scheduled))
        {
            TempData["CabinetError"] = $"Ділянку {plot.CadastralNumber} не можна вилучити: є активні заявки покупців на огляд.";
            return RedirectToAction(nameof(Cabinet));
        }

        await _plotRemoval.RemoveWithDependentsAsync(plot);

        TempData["CabinetMessage"] = $"Оголошення {plot.CadastralNumber} вилучено разом із документами.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-11 «Завантажити правовстановлюючі документи»: продавець додає до власної ділянки документ або витяг
    /// з автоматичним підрахунком контрольної суми SHA-256.
    /// </summary>
    [Authorize(Roles = AppRoles.Seller)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPlotDocument(
        int plotId,
        string? title,
        string? docType,
        string? documentNumber,
        DateTime? issueDate,
        IFormFile? documentFile = null)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);
        if (plot != null && currentUser != null)
        {
            string cleanDocType = !string.IsNullOrWhiteSpace(docType) ? docType.Trim() : "Витяг з ДЗК";
            string cleanDocNum = (documentNumber ?? string.Empty).Trim();
            string cleanTitle = !string.IsNullOrWhiteSpace(title)
                ? title.Trim()
                : (!string.IsNullOrWhiteSpace(cleanDocNum) ? $"{cleanDocType} ({cleanDocNum})" : cleanDocType);

            var (savedFileUrl, fileBytes, mimeType) = await SaveUploadedDocumentAsync(documentFile, plot.Id, cleanDocNum);
            var doc = new PlotDocument
            {
                LandPlotId = plot.Id,
                Title = cleanTitle,
                DocType = cleanDocType,
                DocumentNumber = cleanDocNum,
                IssueDate = issueDate ?? DateTime.Today,
                IsVerified = plot.Status == PlotStatus.VerifiedInCatalog
            };
            doc.AttachFile(savedFileUrl, fileBytes, mimeType);

            _db.PlotDocuments.Add(doc);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Документ «{doc.Title}» успішно прикріплено до ділянки {plot.CadastralNumber}.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Подання клієнтом реквізитів банківської оплати (код квитанції, IBAN) за виставленим рахунком-фактурою.
    /// Переводить платіж у статус очікування підтвердження виписки агентством.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitPayment(int paymentId, string paymentReference, string? senderIban)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && payment.SaleDeal != null && payment.SaleDeal.BuyerId == currentUserId)
        {
            if (payment.SaleDeal.Status == DealStatus.Cancelled)
            {
                TempData["CabinetError"] = "Угоду скасовано. Подання реквізитів оплати за скасованою угодою неможливе.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (payment.IsPaid)
            {
                TempData["CabinetError"] = $"Рахунок {payment.InvoiceNumber} вже підтверджено агентством.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                TempData["CabinetError"] = "Вкажіть номер платіжної інструкції або код квитанції банку.";
                return RedirectToAction(nameof(Cabinet));
            }

            string resolvedSenderIban = (senderIban ?? currentUser?.BankDetails ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(resolvedSenderIban) && !System.Text.RegularExpressions.Regex.IsMatch(resolvedSenderIban, @"^UA\d{27}$"))
            {
                TempData["CabinetError"] = "Невірний формат IBAN платника. Вкажіть рахунок у форматі UA та 27 цифр.";
                return RedirectToAction(nameof(Cabinet));
            }

            payment.SubmitPayment(paymentReference, resolvedSenderIban);
            await _db.SaveChangesAsync();

            TempData["CabinetMessage"] = $"Реквізити оплати за рахунком {payment.InvoiceNumber} (транзакція {payment.PaymentReference}) надіслано на верифікацію банківської виписки рієлтору.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Альтернативний обробник для зворотної сумісності форми кабінету.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int dealId, string? payerIban, string? paymentReference)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.BuyerId == currentUserId);

        if (deal != null && deal.Status != DealStatus.Cancelled)
        {
            string reference = !string.IsNullOrWhiteSpace(paymentReference)
                ? paymentReference.Trim()
                : $"TRX-{DateTime.Now:yyyyMMdd}-{deal.Id:D4}";

            var pendingPayment = deal.Payments.FirstOrDefault(p => !p.IsPaid);
            if (pendingPayment != null)
            {
                pendingPayment.SubmitPayment(reference, payerIban ?? currentUser?.BankDetails);
                await _db.SaveChangesAsync();
                TempData["CabinetMessage"] = $"Повідомлення про оплату за договором {deal.DraftContractNumber} передано рієлтору. Код транзакції: {reference}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-05 «Підписати договір купівлі-продажу у нотаріуса» (include UC-06 «Здійснити оплату / завдаток через банк»).
    /// Покупець підписує договір, коли юрист погодив його, а банківська виписка про оплату рахунку агентства підтверджена.
    /// Реєстровий номер нотаріальної дії присвоюється автоматично.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignContractAtNotary(int dealId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.BuyerId == currentUserId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (!deal.CanSignAtNotary)
        {
            TempData["CabinetError"] = $"Договір {deal.DraftContractNumber} ще не погоджено юристом або вже підписаний.";
            return RedirectToAction(nameof(Cabinet));
        }

        if (!deal.IsAgencyFeePaid)
        {
            TempData["CabinetError"] = $"Підписання можливе після оплати рахунку агентства {deal.AgencyPayment?.InvoiceNumber}: очікується підтвердження банківської виписки.";
            return RedirectToAction(nameof(Cabinet));
        }

        deal.SignByParties($"НР-{DateTime.Now:yyyy}-{deal.Id:D4}");
        await _db.SaveChangesAsync();

        TempData["CabinetMessage"] = $"Договір {deal.DraftContractNumber} підписано у нотаріуса (реєстровий №{deal.NotarialRegistryNumber}). Далі юрист зареєструє перехід права власності.";
        return RedirectToAction(nameof(Cabinet));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(int dealId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

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

        bool isAuthorized = currentUser.Role == UserRole.Realtor ||
                            currentUser.Role == UserRole.Lawyer ||
                            deal.BuyerId == currentUser.Id ||
                            deal.LandPlot?.SellerId == currentUser.Id;

        if (!isAuthorized)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return View("Receipt", deal);
    }

    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string? fullName, string? phone, string currentPassword, string newPassword, string? bankDetails, string? taxId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser != null)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                currentUser.FullName = fullName.Trim();
            }

            if (phone != null)
            {
                currentUser.Phone = phone.Trim();
            }

            if (!string.IsNullOrWhiteSpace(bankDetails))
            {
                var ibanClean = bankDetails.Trim().ToUpperInvariant();
                if (!System.Text.RegularExpressions.Regex.IsMatch(ibanClean, @"^UA\d{27}$"))
                {
                    TempData["CabinetError"] = "Невірний формат IBAN. Банківський рахунок України має містити літери UA та 27 цифр (29 символів).";
                    return RedirectToAction(nameof(Cabinet));
                }
                currentUser.BankDetails = ibanClean;
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

    /// <summary>
    /// Особистий кабінет клієнта. Продавець бачить власні ділянки, їхні документи та угоди з продажу;
    /// покупець бачить запити на підбір, заявки на огляд, угоди купівлі та рахунки на оплату.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpGet("/Cabinet")]
    [HttpGet("/Client/Cabinet")]
    public async Task<IActionResult> Cabinet()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var viewModel = new CabinetViewModel { CurrentUser = currentUser };

        if (currentUser.Role == UserRole.Seller)
        {
            await LoadSellerCabinetAsync(viewModel);
        }
        else
        {
            await LoadBuyerCabinetAsync(viewModel);
        }

        return View(viewModel);
    }

    private async Task LoadSellerCabinetAsync(CabinetViewModel viewModel)
    {
        int sellerId = viewModel.CurrentUser!.Id;

        viewModel.MyPlots = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .Where(p => p.SellerId == sellerId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        viewModel.MySellerDeals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .Where(d => d.LandPlot != null && d.LandPlot.SellerId == sellerId)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    private async Task LoadBuyerCabinetAsync(CabinetViewModel viewModel)
    {
        int buyerId = viewModel.CurrentUser!.Id;

        viewModel.MySelectionRequests = await _db.SelectionRequests
            .Where(r => r.BuyerId == buyerId)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        var matchedIds = viewModel.MySelectionRequests
            .SelectMany(r => r.GetMatchedPlotIds())
            .Distinct()
            .ToList();

        if (matchedIds.Any())
        {
            viewModel.MatchedPlotsForUser = await _db.LandPlots
                .Where(p => matchedIds.Contains(p.Id) && p.Status == PlotStatus.VerifiedInCatalog)
                .ToListAsync();
        }

        viewModel.MyViewings = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .Include(v => v.Realtor)
            .Where(v => v.BuyerId == buyerId)
            .OrderByDescending(v => v.Id)
            .ToListAsync();

        viewModel.MyDeals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .Where(d => d.BuyerId == buyerId)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Файлова система не входить у транзакцію бази даних, тому після відкоту завантажений файл вилучається вручну.
    /// </summary>
    private void DeleteUploadedFile(string fileUrl)
    {
        var path = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", Path.GetFileName(fileUrl));
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
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
