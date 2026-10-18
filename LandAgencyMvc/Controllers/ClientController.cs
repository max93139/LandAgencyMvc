using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public class ClientController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IWebHostEnvironment _env;

    public ClientController(LandAgencyContext context, IWebHostEnvironment env)
    {
        _db = context;
        _env = env;
    }

    private async Task<AppUser?> SetCurrentUserAsync(int userId)
    {
        int resolvedId = userId;
        if (resolvedId <= 0 &&
            Request.Cookies.TryGetValue("LandAgencyUserId", out var cookieVal) &&
            int.TryParse(cookieVal, out var parsedCookieId))
        {
            resolvedId = parsedCookieId;
        }

        AppUser? user = null;
        if (resolvedId > 0)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == resolvedId);
        }

        user ??= await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Client);

        ViewBag.CurrentUser = user;
        ViewBag.CurrentUserId = user?.Id ?? 0;
        return user;
    }

    [HttpGet("/Catalog")]
    [HttpGet("/Client/Catalog")]
    public async Task<IActionResult> Catalog(int userId, string? purpose, decimal? maxBudget, double? minArea, string? search)
    {
        await SetCurrentUserAsync(userId);

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

        var plots = await query.OrderByDescending(p => p.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            var cleanPurpose = purpose.Trim();
            plots = plots.Where(p =>
                p.Purpose.Contains(cleanPurpose, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            plots = plots.Where(p =>
                p.Location.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                p.CadastralNumber.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.FilterPurpose = purpose;
        ViewBag.FilterMaxBudget = maxBudget;
        ViewBag.FilterMinArea = minArea;
        ViewBag.FilterSearch = search;

        return View(plots);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSelectionRequest(int userId, string desiredDistrict, double minAreaHa, decimal maxBudgetUsd, string purpose)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        if (currentUser == null)
        {
            TempData["LoginError"] = "Будь ласка, увійдіть або зареєструйтеся в системі.";
            return RedirectToAction("Login", "Account");
        }

        if (maxBudgetUsd <= 0 || minAreaHa <= 0 || string.IsNullOrWhiteSpace(desiredDistrict))
        {
            TempData["CabinetError"] = "Вкажіть коректний район, мінімальну площу та бюджет для підбору ділянки.";
            return RedirectToAction(nameof(Catalog), new { userId = currentUser.Id });
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

        TempData["CabinetMessage"] = "Запит на індивідуальний підбір ділянки успішно передано рієлтору.";
        return RedirectToAction(nameof(Cabinet), new { userId = currentUser.Id });
    }

    [HttpGet("/Catalog/Details/{id:int}")]
    [HttpGet("/Client/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, int userId)
    {
        await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .ThenInclude(c => c!.Lawyer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return RedirectToAction(nameof(Catalog), new { userId });
        }

        return View(plot);
    }

    [HttpPost]
    public async Task<IActionResult> CreateViewing(int userId, int landPlotId, DateTime preferredDate, string timeSlot, string comment)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        if (currentUser == null)
        {
            TempData["LoginError"] = "Будь ласка, увійдіть або зареєструйтеся для запису на огляд.";
            return RedirectToAction("Login", "Account");
        }

        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == landPlotId);
        if (plot == null || plot.Status != PlotStatus.VerifiedInCatalog)
        {
            TempData["CabinetError"] = "Обрана земельна ділянка не знайдена або недоступна для виїзного огляду.";
            return RedirectToAction(nameof(Catalog), new { userId = currentUser.Id });
        }

        if (plot.SellerId == currentUser.Id)
        {
            TempData["DetailsError"] = "Ви є власником цієї ділянки і не можете подати заявку на її огляд як покупець.";
            return RedirectToAction(nameof(Details), new { id = landPlotId, userId = currentUser.Id });
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
        return RedirectToAction(nameof(Cabinet), new { userId = currentUser.Id });
    }

    [HttpPost]
    public async Task<IActionResult> CancelViewingByClient(int userId, int viewingId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var viewing = await _db.ViewingRequests.FindAsync(viewingId);
        if (viewing != null)
        {
            viewing.CancelViewing("Скасовано покупцем з особистого кабінету");
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Заявку на огляд #{viewingId} скасовано.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> RequestPurchaseFromViewing(int userId, int viewingId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var viewing = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .ThenInclude(p => p!.Seller)
            .FirstOrDefaultAsync(v => v.Id == viewingId);

        if (viewing != null && viewing.LandPlot != null)
        {
            if (viewing.LandPlot.SellerId == viewing.BuyerId)
            {
                TempData["CabinetError"] = "Власник ділянки не може укласти угоду купівлі власного об'єкта.";
                return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
            }

            if (viewing.LandPlot.Status == PlotStatus.Sold)
            {
                TempData["CabinetError"] = "Ця земельна ділянка вже продана.";
                return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
            }

            viewing.BuyerReadyToBuy = true;
            if (viewing.Status == ViewingStatus.Scheduled)
            {
                viewing.Status = ViewingStatus.Completed;
            }

            bool dealExists = await _db.SaleDeals.AnyAsync(d =>
                d.LandPlotId == viewing.LandPlotId && d.Status != DealStatus.ReturnedForRevision);

            if (!dealExists)
            {
                var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);
                var deal = new SaleDeal
                {
                    LandPlotId = viewing.LandPlotId,
                    BuyerId = viewing.BuyerId,
                    RealtorId = realtor?.Id,
                    NotaryOffice = "Перша Київська державна нотаріальна контора",
                    NotaryDate = DateTime.Today.AddDays(5).AddHours(11)
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
                    agencyIban: realtor?.BankDetails));
            }

            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = "Заявку на купівлю подано! Проєкт договору сформовано та передано юристу й рієлтору.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet("/Plots/Create")]
    [HttpGet("/Client/CreatePlot")]
    public async Task<IActionResult> CreatePlot(int userId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var model = new LandPlot
        {
            SellerBankIban = currentUser?.BankDetails ?? string.Empty
        };
        return View(model);
    }

    [HttpPost("/Plots/Create")]
    [HttpPost("/Client/CreatePlot")]
    public async Task<IActionResult> CreatePlot(
        int userId,
        LandPlot model,
        string? docTitle,
        string? docType,
        DateTime? docIssueDate,
        bool signWithKep = false,
        IFormFile? documentFile = null)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        if (currentUser == null)
        {
            TempData["LoginError"] = "Будь ласка, увійдіть або зареєструйтеся в системі, щоб виставити ділянку.";
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

        string savedFileUrl = await SaveUploadedDocumentAsync(documentFile, model.Id, model.OwnershipDocNumber);

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
        plotDoc.AttachFile(savedFileUrl);

        if (signWithKep)
        {
            plotDoc.ApplyKepSignature(currentUser.FullName, currentUser.TaxId);
        }

        _db.PlotDocuments.Add(plotDoc);
        await _db.SaveChangesAsync();

        TempData["CabinetMessage"] = $"Ділянку {model.CadastralNumber} та правовстановлюючий документ додано і відправлено юристу на перевірку в ДЗК/ДРРП.";
        return RedirectToAction(nameof(Cabinet), new { userId = currentUser.Id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePlotPrice(int userId, int plotId, decimal newPriceUsd)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId);
        if (plot != null && newPriceUsd > 0)
        {
            plot.UpdatePrice(newPriceUsd);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Ціну ділянки {plot.CadastralNumber} оновлено до ${newPriceUsd:N0}.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> AddPlotDocument(
        int userId,
        int plotId,
        string title,
        string docType,
        string documentNumber,
        DateTime? issueDate,
        bool signWithKep = false,
        IFormFile? documentFile = null)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId);
        if (plot != null && currentUser != null)
        {
            string fileUrl = await SaveUploadedDocumentAsync(documentFile, plot.Id, documentNumber);
            var doc = new PlotDocument
            {
                LandPlotId = plot.Id,
                Title = !string.IsNullOrWhiteSpace(title) ? title.Trim() : "Документ земельної ділянки",
                DocType = !string.IsNullOrWhiteSpace(docType) ? docType.Trim() : "Витяг з ДЗК",
                DocumentNumber = (documentNumber ?? string.Empty).Trim(),
                IssueDate = issueDate ?? DateTime.Today,
                IsVerified = plot.Status == PlotStatus.VerifiedInCatalog
            };
            doc.AttachFile(fileUrl);

            if (signWithKep)
            {
                doc.ApplyKepSignature(currentUser.FullName, currentUser.TaxId);
            }

            _db.PlotDocuments.Add(doc);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Документ «{doc.Title}» {(signWithKep ? "підписано КЕП та " : "")}прикріплено до ділянки {plot.CadastralNumber}.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmPayment(int userId, int dealId, string? payerIban)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Realtor)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (!deal.Payments.Any())
            {
                _db.Payments.AddRange(deal.CreateDefaultPayments(
                    deal.LandPlot?.SellerBankIban ?? deal.LandPlot?.Seller?.BankDetails,
                    isPaid: true,
                    senderIban: payerIban ?? currentUser?.BankDetails,
                    agencyIban: deal.Realtor?.BankDetails));
            }
            else
            {
                foreach (var payment in deal.Payments)
                {
                    payment.ExecuteTransaction(payerIban ?? currentUser?.BankDetails);
                }
            }

            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Банківську оплату за угодою {deal.DraftContractNumber} успішно підтверджено. Квитанцію сформовано.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(int dealId, int userId)
    {
        await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Cabinet), new { userId });
        }

        return View("Receipt", deal);
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(int userId, string currentPassword, string newPassword, string? bankDetails)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        if (currentUser != null)
        {
            if (!string.IsNullOrWhiteSpace(bankDetails))
            {
                currentUser.BankDetails = bankDetails.Trim();
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (!currentUser.ChangePassword(currentPassword, newPassword))
                {
                    TempData["CabinetError"] = "Невірний поточний пароль. Пароль не змінено.";
                    return RedirectToAction(nameof(Cabinet), new { userId = currentUser.Id });
                }
            }

            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = "Налаштування профілю та реквізити успішно оновлено.";
        }

        return RedirectToAction(nameof(Cabinet), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet("/Cabinet")]
    [HttpGet("/Client/Cabinet")]
    public async Task<IActionResult> Cabinet(int userId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        int activeUserId = currentUser?.Id ?? userId;

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

    private async Task<string> SaveUploadedDocumentAsync(IFormFile? file, int plotId, string? docNum)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        if (file != null && file.Length > 0)
        {
            var safeExt = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(safeExt))
            {
                safeExt = ".pdf";
            }

            var fileName = $"plot_{plotId}_{DateTime.Now:yyyyMMddHHmmss}{safeExt}";
            var fullPath = Path.Combine(uploadsFolder, fileName);
            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/{fileName}";
        }

        var generatedFileName = $"extract_drrp_plot_{plotId}.txt";
        var generatedPath = Path.Combine(uploadsFolder, generatedFileName);
        if (!System.IO.File.Exists(generatedPath))
        {
            await System.IO.File.WriteAllTextAsync(
                generatedPath,
                $"ВИТЯГ З ДЕРЖАВНОГО РЕЄСТРУ РЕЧОВИХ ПРАВ НА НЕРУХОМЕ МАЙНО\nДокумент №: {docNum}\nID ділянки: {plotId}\nДата формування: {DateTime.Now:dd.MM.yyyy HH:mm}\nСтатус КЕП: Дійсний кваліфікований електронний підпис власника.");
        }

        return $"/uploads/{generatedFileName}";
    }
}
