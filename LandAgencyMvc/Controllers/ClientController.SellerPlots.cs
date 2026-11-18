using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class ClientController
{
    [Authorize(Roles = AppRoles.AnyClient)]
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
    [Authorize(Roles = AppRoles.AnyClient)]
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
        model.Status = PlotStatus.Draft;
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

        TempData["CabinetMessage"] = $"Ділянку {model.CadastralNumber} ({koatuu.RegionName}) збережено як чернетку; щоб юрист її перевірив, натисніть «Відправити юристу». " +
                                     $"Попередній запит до ДЗК/ДРРП (витяг {registryExtract.ExtractNumber}): {registryExtract.RecommendedRiskLevel}. " +
                                     "Документ зафіксовано з контрольною сумою SHA-256.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-10 «Встановити ціну та умови продажу»: продавець змінює ціну й умови продажу власної ділянки,
    /// доки за нею немає підписаного договору.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
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
    [Authorize(Roles = AppRoles.AnyClient)]
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
    [Authorize(Roles = AppRoles.AnyClient)]
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
    /// UC-08/UC-09: продавець відправляє свою чернетку (або виправлену після відхилення ділянку) юристу.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOwnPlotForLegalCheck(int plotId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == plotId && p.SellerId == currentUserId);

        if (plot == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (!plot.SendForLegalCheck())
        {
            TempData["CabinetError"] = plot.Documents.Any()
                ? $"Ділянку {plot.CadastralNumber} у статусі «{plot.StatusDisplayName}» відправити юристу не можна."
                : $"Ділянку {plot.CadastralNumber} не можна відправити юристу: додайте хоча б один правовстановлюючий документ.";
            return RedirectToAction(nameof(Cabinet));
        }

        await _db.SaveChangesAsync();
        TempData["CabinetMessage"] = $"Ділянку {plot.CadastralNumber} відправлено юристу на перевірку.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Продавець вилучає неверифікований документ власної чернетки або відхиленої ділянки.
    /// Верифіковані юристом документи не вилучаються.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOwnPlotDocument(int documentId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var doc = await _db.PlotDocuments
            .Include(d => d.LandPlot)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.LandPlot != null && d.LandPlot.SellerId == currentUserId);

        if (doc == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (doc.IsVerified || doc.LandPlot!.Status is not (PlotStatus.Draft or PlotStatus.RejectedByLawyer))
        {
            TempData["CabinetError"] = "Документ вилучити не можна: ділянка на перевірці або документ уже верифіковано юристом.";
            return RedirectToAction(nameof(Cabinet));
        }

        _db.PlotDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        TempData["CabinetMessage"] = $"Документ {doc.DocType} №{doc.DocumentNumber} вилучено.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Вилучення власного оголошення. Вилучити можна лише чернетку без документів; фото й відео
    /// вилучаються разом із нею, а копія з причиною записується в архів.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOwnPlot(int plotId, string? reason)
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

        if (!plot.CanBeDeleted || await _db.SaleDeals.AnyAsync(d => d.LandPlotId == plot.Id))
        {
            TempData["CabinetError"] = $"Ділянку {plot.CadastralNumber} вилучити не можна: вилучається лише чернетка без документів. Ділянка, яку бачив юрист, лишається в історії агентства.";
            return RedirectToAction(nameof(Cabinet));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["CabinetError"] = "Вкажіть причину вилучення оголошення.";
            return RedirectToAction(nameof(Cabinet));
        }

        await _plotRemoval.RemoveAsync(plot, reason, currentUser!);

        TempData["CabinetMessage"] = $"Оголошення {plot.CadastralNumber} вилучено. Копію збережено в архіві агентства.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-11 «Завантажити правовстановлюючі документи»: продавець додає до власної ділянки документ або витяг
    /// з автоматичним підрахунком контрольної суми SHA-256.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
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
            if (plot.AreLegalFactsLocked || !plot.IsEditable)
            {
                TempData["CabinetError"] = $"До ділянки {plot.CadastralNumber} документи зараз не додаються: її перевіряє юрист, вона вже опублікована або продана.";
                return RedirectToAction(nameof(Cabinet));
            }

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
                IsVerified = false
            };
            doc.AttachFile(savedFileUrl, fileBytes, mimeType);

            _db.PlotDocuments.Add(doc);
            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = $"Документ «{doc.Title}» успішно прикріплено до ділянки {plot.CadastralNumber}.";
        }

        return RedirectToAction(nameof(Cabinet));
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
