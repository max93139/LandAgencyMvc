using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// UC-12 «Скласти опис ділянки з фото, відео та координатами»: швидка зміна параметрів ділянки.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlot(
        int plotId,
        string? title,
        string? location,
        decimal? priceUsd,
        string? description)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var plot = await _db.LandPlots.FindAsync(plotId);
        if (plot != null)
        {
            if (plot.Status == PlotStatus.Sold)
            {
                TempData["RealtorError"] = $"Ділянку {plot.CadastralNumber} вже продано. Редагування параметрів об'єкта заблоковано.";
                return RedirectToAction("Details", "Client", new { id = plotId });
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                plot.Title = title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                plot.Location = location.Trim();
            }

            if (priceUsd.HasValue && priceUsd.Value > 0)
            {
                plot.PriceUsd = priceUsd.Value;
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                plot.Description = description.Trim();
            }

            plot.AssignedRealtorId ??= currentUser?.Id;
            await _db.SaveChangesAsync();
            await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);

            TempData["RealtorMessage"] = $"Дані земельної ділянки {plot.CadastralNumber} успішно оновлено.";
        }

        return RedirectToAction(nameof(Requests));
    }

    /// <summary>
    /// UC-12 «Скласти опис ділянки з фото, відео та координатами»: комерційний опис.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlotDescription(int plotId, string description)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var plot = await _db.LandPlots.FindAsync(plotId);
        if (plot != null)
        {
            if (plot.Status == PlotStatus.Sold)
            {
                TempData["RealtorError"] = $"Ділянку {plot.CadastralNumber} вже продано. Редагування комерційного опису заблоковано.";
                return RedirectToAction(nameof(Requests));
            }

            plot.Description = (description ?? string.Empty).Trim();
            plot.AssignedRealtorId ??= currentUser?.Id;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Комерційний опис ділянки {plot.CadastralNumber} оновлено.";
        }

        return RedirectToAction(nameof(Requests));
    }

    /// <summary>
    /// Реєстр земельних ділянок агентства з повним набором операцій CRUD.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Plots(string? search, PlotStatus? status)
    {
        var query = _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .Include(p => p.ViewingRequests)
            .Include(p => p.LegalConclusion)
            .Where(p => p.Status != PlotStatus.Sold)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.CadastralNumber.ToLower().Contains(s) ||
                p.Title.ToLower().Contains(s) ||
                p.Location.ToLower().Contains(s) ||
                (p.Seller != null && p.Seller.FullName.ToLower().Contains(s)));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var plots = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(plots);
    }

    /// <summary>
    /// Форма створення нової земельної ділянки (базова модель PlotCreateViewModel).
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> CreatePlot()
    {
        ViewBag.Sellers = await _db.Users
            .Where(u => u.Role == UserRole.Seller && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return View(new PlotCreateViewModel());
    }

    /// <summary>
    /// Збереження нової ділянки з валідацією КОАТУУ та первинним документом.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlot(PlotCreateViewModel vm)
    {
        var koatuu = _cadastralService.ValidateKoatuuFormat(vm.CadastralNumber);
        if (!koatuu.IsValid)
        {
            ModelState.AddModelError(nameof(vm.CadastralNumber), $"Помилка структури кадастрового номера: {koatuu.ErrorMessage}");
        }

        bool duplicate = await _db.LandPlots.AnyAsync(p =>
            p.CadastralNumber == vm.CadastralNumber.Trim() &&
            p.Status != PlotStatus.RejectedByLawyer);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.CadastralNumber), $"Ділянка з номером {vm.CadastralNumber} вже існує в базі даних.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Sellers = await _db.Users
                .Where(u => u.Role == UserRole.Seller && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();
            return View(vm);
        }

        var realtor = await GetCurrentRealtorAsync();
        var plot = new LandPlot
        {
            SellerId = vm.SellerId,
            AssignedRealtorId = realtor?.Id,
            CadastralNumber = vm.CadastralNumber.Trim(),
            Title = vm.Title.Trim(),
            Location = vm.Location.Trim(),
            AreaHectares = vm.AreaHectares,
            Purpose = vm.Purpose.Trim(),
            PriceUsd = vm.PriceUsd,
            OwnershipDocNumber = vm.OwnershipDocNumber.Trim(),
            SellerBankIban = vm.SellerBankIban.Trim().ToUpperInvariant(),
            Description = (vm.Description ?? string.Empty).Trim(),
            Latitude = vm.Latitude,
            Longitude = vm.Longitude,
            Status = PlotStatus.Draft,
            CreatedAt = DateTime.Now
        };
        plot.UpdateSaleConditions(vm.SaleConditions);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.LandPlots.Add(plot);
        await _db.SaveChangesAsync();

        var docTitle = !string.IsNullOrWhiteSpace(vm.DocTitle)
            ? vm.DocTitle.Trim()
            : $"Правовстановлюючий документ ({plot.OwnershipDocNumber})";
        var docType = !string.IsNullOrWhiteSpace(vm.DocType)
            ? vm.DocType.Trim()
            : "Витяг з ДРРП про право власності";

        var plotDoc = new PlotDocument
        {
            LandPlotId = plot.Id,
            Title = docTitle,
            DocType = docType,
            DocumentNumber = plot.OwnershipDocNumber,
            IssueDate = vm.DocIssueDate ?? DateTime.Today,
            IsVerified = false
        };
        var dummyBytes = System.Text.Encoding.UTF8.GetBytes($"{docType} №{plot.OwnershipDocNumber} для {plot.CadastralNumber}");
        plotDoc.AttachFile(string.Empty, dummyBytes, "application/pdf");

        _db.PlotDocuments.Add(plotDoc);
        await _db.SaveChangesAsync();
        await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);
        await transaction.CommitAsync();

        TempData["RealtorMessage"] = $"Ділянку {plot.CadastralNumber} збережено як чернетку. Коли дані й документи будуть готові, натисніть «Відправити юристу».";
        return RedirectToAction(nameof(Plots));
    }

    /// <summary>
    /// UC-12 «Скласти опис ділянки з фото, відео та координатами»: форма ділянки з координатами та умовами продажу.
    /// Форма редагування земельної ділянки та її документів (похідна модель PlotEditViewModel : PlotCreateViewModel).
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> EditPlot(int id)
    {
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        if (!plot.IsEditable)
        {
            TempData["RealtorError"] = $"Ділянку {plot.CadastralNumber} вже продано. Редагування заборонено.";
            return RedirectToAction(nameof(Plots));
        }

        ViewBag.Sellers = await _db.Users
            .Where(u => u.Role == UserRole.Seller && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return View(PlotEditViewModel.From(plot));
    }

    /// <summary>
    /// UC-12 «Скласти опис ділянки з фото, відео та координатами»: форма ділянки з координатами та умовами продажу.
    /// Збереження змін у ділянці та синхронізація її документів.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPlot(int id, PlotEditViewModel vm)
    {
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        if (!plot.IsEditable)
        {
            TempData["RealtorError"] = $"Ділянку {plot.CadastralNumber} вже відчужено за договором. Зміни заборонені.";
            return RedirectToAction(nameof(Plots));
        }

        var koatuu = _cadastralService.ValidateKoatuuFormat(vm.CadastralNumber);
        if (!koatuu.IsValid)
        {
            ModelState.AddModelError(nameof(vm.CadastralNumber), $"Помилка кадастрового номера: {koatuu.ErrorMessage}");
        }

        bool duplicate = await _db.LandPlots.AnyAsync(p =>
            p.Id != id &&
            p.CadastralNumber == vm.CadastralNumber.Trim() &&
            p.Status != PlotStatus.RejectedByLawyer);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.CadastralNumber), $"Інша ділянка вже зареєстрована з номером {vm.CadastralNumber}.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Sellers = await _db.Users
                .Where(u => u.Role == UserRole.Seller && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();
            vm.Media = plot.Media.OrderBy(m => m.Id).ToList();
            return View(vm);
        }

        _db.UseSubmittedVersion(plot, vm.Version);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        vm.ApplyTo(plot);
        await _db.SaveChangesAsync();
        await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);
        await transaction.CommitAsync();

        TempData["RealtorMessage"] = $"Дані земельної ділянки {plot.CadastralNumber} та її документів збережено.";
        return RedirectToAction(nameof(Plots));
    }

    /// <summary>
    /// UC-12 «Скласти опис ділянки з фото, відео та координатами»: додає фото або відео до ділянки.
    /// Відео можна завантажити файлом або вказати https-посиланням. Координати вносяться у формі редагування.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPlotMedia(int plotId, MediaKind kind, IFormFile? mediaFile, string? videoUrl, string? caption)
    {
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId);
        if (plot == null)
        {
            return NotFound();
        }

        if (!plot.IsEditable)
        {
            TempData["RealtorError"] = "Неможливо додати матеріали до проданої ділянки.";
            return RedirectToAction(nameof(Plots));
        }

        var result = await _mediaStorage.SaveAsync(plotId, kind, mediaFile, videoUrl, caption);
        if (result.Media == null)
        {
            TempData["RealtorError"] = result.Error;
            return RedirectToAction(nameof(EditPlot), new { id = plotId });
        }

        _db.PlotMedia.Add(result.Media);
        await _db.SaveChangesAsync();

        TempData["RealtorMessage"] = kind == MediaKind.Photo ? "Фото додано до ділянки." : "Відео додано до ділянки.";
        return RedirectToAction(nameof(EditPlot), new { id = plotId });
    }

    /// <summary>
    /// Вилучення фото або відео ділянки разом із завантаженим файлом.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlotMedia(int mediaId, int plotId)
    {
        var media = await _db.PlotMedia.FirstOrDefaultAsync(m => m.Id == mediaId && m.LandPlotId == plotId);
        if (media != null)
        {
            _db.PlotMedia.Remove(media);
            await _db.SaveChangesAsync();
            _mediaStorage.DeleteFile(media);
            TempData["RealtorMessage"] = "Матеріал вилучено.";
        }

        return RedirectToAction(nameof(EditPlot), new { id = plotId });
    }

    /// <summary>
    /// Вилучення окремого документа ділянки.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlotDocument(int documentId, int plotId)
    {
        var doc = await _db.PlotDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.LandPlotId == plotId);
        if (doc != null)
        {
            var plot = await _db.LandPlots.FindAsync(plotId);
            if (plot != null && !plot.IsEditable)
            {
                TempData["RealtorError"] = "Неможливо вилучити документ проданої ділянки.";
                return RedirectToAction(nameof(EditPlot), new { id = plotId });
            }

            if (plot != null && plot.AreLegalFactsLocked)
            {
                TempData["RealtorError"] = "Документи ділянки, яку перевіряє юрист або вже опубліковано, не змінюються.";
                return RedirectToAction(nameof(EditPlot), new { id = plotId });
            }

            if (doc.IsVerified)
            {
                TempData["RealtorError"] = $"Документ «{doc.Title}» верифіковано юристом, тому рієлтор його не вилучає. Щоб змінити документи, юрист відкликає висновок.";
                return RedirectToAction(nameof(EditPlot), new { id = plotId });
            }

            _db.PlotDocuments.Remove(doc);
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Документ «{doc.Title}» вилучено.";
        }

        return RedirectToAction(nameof(EditPlot), new { id = plotId });
    }

    /// <summary>
    /// UC-08/UC-09: рієлтор відправляє чернетку або виправлену після відхилення ділянку юристу.
    /// Відправка — окрема свідома дія, а не наслідок збереження форми.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPlotForLegalCheck(int id)
    {
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        if (!plot.SendForLegalCheck())
        {
            TempData["RealtorError"] = plot.Documents.Any()
                ? $"Ділянку {plot.CadastralNumber} у статусі «{plot.StatusDisplayName}» відправити юристу не можна."
                : $"Ділянку {plot.CadastralNumber} не можна відправити юристу: додайте хоча б один правовстановлюючий документ.";
            return RedirectToAction(nameof(Plots));
        }

        await _db.SaveChangesAsync();
        TempData["RealtorMessage"] = $"Ділянку {plot.CadastralNumber} відправлено юристу на перевірку.";
        return RedirectToAction(nameof(Plots));
    }

    /// <summary>
    /// Сторінка вилучення ділянки: показує, що вилучиться разом із ділянкою (фото й відео) і що блокує вилучення.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> DeletePlot(int id)
    {
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .Include(p => p.ViewingRequests)
            .Include(p => p.LegalConclusion)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        var vm = new DeletePlotConfirmationViewModel
        {
            Plot = plot,
            DocumentsCount = plot.Documents.Count,
            ViewingsCount = plot.ViewingRequests.Count,
            MediaCount = plot.Media.Count,
            HasLegalConclusion = plot.LegalConclusion != null,
            DealsCount = await _db.SaleDeals.CountAsync(d => d.LandPlotId == id)
        };

        return View(vm);
    }

    /// <summary>
    /// Вилучає чернетку ділянки разом із її фото й відео (CASCADE). Документи, висновок, огляди й угоди
    /// блокують вилучення (RESTRICT): вони потрібні для звітності.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost, ActionName("DeletePlotConfirmed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlotConfirmed(int id, string? reason)
    {
        var plot = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.Media)
            .Include(p => p.ViewingRequests)
            .Include(p => p.LegalConclusion)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        var check = new DeletePlotConfirmationViewModel
        {
            Plot = plot,
            DocumentsCount = plot.Documents.Count,
            ViewingsCount = plot.ViewingRequests.Count,
            MediaCount = plot.Media.Count,
            HasLegalConclusion = plot.LegalConclusion != null,
            DealsCount = await _db.SaleDeals.CountAsync(d => d.LandPlotId == id)
        };
        if (!check.CanBeDeleted)
        {
            TempData["RealtorError"] = $"Ділянку {plot.CadastralNumber} вилучити не можна: {string.Join("; ", check.Blockers)}.";
            return RedirectToAction(nameof(DeletePlot), new { id });
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["RealtorError"] = "Вкажіть причину вилучення: вона зберігається в архіві.";
            return RedirectToAction(nameof(DeletePlot), new { id });
        }

        var realtor = await GetCurrentRealtorAsync();
        if (realtor == null)
        {
            return Forbid();
        }

        string cadNum = plot.CadastralNumber;
        int mediaRemoved = plot.Media.Count;
        await _plotRemoval.RemoveAsync(plot, reason, realtor);

        TempData["RealtorMessage"] = mediaRemoved > 0
            ? $"Ділянку {cadNum} вилучено разом із фото й відео ({mediaRemoved}). Копію збережено в архіві."
            : $"Ділянку {cadNum} вилучено. Копію збережено в архіві.";
        return RedirectToAction(nameof(Plots));
    }

    /// <summary>
    /// Архів ділянок: продані ділянки з даними завершеної угоди та вилучені ділянки з причиною вилучення.
    /// Продані ділянки не показуються в робочому реєстрі, бо з ними вже нічого не роблять. Лише перегляд.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> PlotArchive()
    {
        var model = new PlotArchiveViewModel
        {
            SoldDeals = await _db.SaleDeals
                .Include(d => d.LandPlot).ThenInclude(p => p!.Seller)
                .Include(d => d.Buyer)
                .Include(d => d.Realtor)
                .Include(d => d.Lawyer)
                .Where(d => d.Status == DealStatus.CompletedAtNotary)
                .OrderByDescending(d => d.NotaryDate)
                .AsNoTracking()
                .ToListAsync(),
            RemovedPlots = await _db.ArchivedPlots
                .AsNoTracking()
                .OrderByDescending(a => a.RemovedAtUtc)
                .ToListAsync()
        };

        return View(model);
    }
}
