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
            Status = PlotStatus.PendingLegalCheck,
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

        TempData["RealtorMessage"] = $"Земельну ділянку {plot.CadastralNumber} успішно зареєстровано та передано в чергу юридичного аудиту.";
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
    /// Сторінка підтвердження видалення ділянки з демонстрацією некаскадного та каскадного режимів.
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
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            return NotFound();
        }

        int dealsCount = await _db.SaleDeals.CountAsync(d => d.LandPlotId == id);

        var vm = new DeletePlotConfirmationViewModel
        {
            Plot = plot,
            DocumentsCount = plot.Documents.Count,
            ViewingsCount = plot.ViewingRequests.Count,
            HasLegalConclusion = plot.LegalConclusion != null,
            DealsCount = dealsCount
        };

        return View(vm);
    }

    /// <summary>
    /// Виконання вилучення ділянки: некаскадного (з контролем Restrict) або каскадного.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost, ActionName("DeletePlotConfirmed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlotConfirmed(int id, bool cascade = false)
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

        int dealsCount = await _db.SaleDeals.CountAsync(d => d.LandPlotId == id);
        if (dealsCount > 0 || !plot.CanBeDeleted)
        {
            TempData["RealtorError"] = dealsCount > 0 || plot.Status == PlotStatus.Sold
                ? $"Неможливо вилучити ділянку {plot.CadastralNumber}: за об'єктом зареєстровано {dealsCount} угод купівлі-продажу. Правило цілісності (Restrict) блокує видалення."
                : $"Неможливо вилучити ділянку {plot.CadastralNumber}: є юридичний висновок №{plot.LegalConclusion?.ConclusionNumber}, який зберігається для аудиту. Щоб вилучити ділянку, юрист має відкликати висновок.";
            return RedirectToAction(nameof(DeletePlot), new { id });
        }

        if (!cascade && plot.HasDependentRecords)
        {
            TempData["RealtorError"] = $"Некаскадне вилучення неможливе (ON DELETE RESTRICT): ділянка містить {plot.Documents.Count} документів та {plot.ViewingRequests.Count} оглядів. Для видалення очистіть їх окремо або оберіть «Каскадне вилучення».";
            return RedirectToAction(nameof(DeletePlot), new { id });
        }

        string cadNum = plot.CadastralNumber;
        int docsRemoved = plot.Documents.Count;
        int viewingsRemoved = plot.ViewingRequests.Count;

        await _plotRemoval.RemoveWithDependentsAsync(plot);

        if (cascade)
        {
            TempData["RealtorMessage"] = $"Ділянку {cadNum} успішно вилучено каскадно разом із документами ({docsRemoved}) та оглядами ({viewingsRemoved}).";
        }
        else
        {
            TempData["RealtorMessage"] = $"Ділянку {cadNum} (без дочірніх записів) успішно вилучено з бази даних.";
        }

        return RedirectToAction(nameof(Plots));
    }
}
