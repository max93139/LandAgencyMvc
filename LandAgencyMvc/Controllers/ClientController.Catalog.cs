using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class ClientController
{
    /// <summary>
    /// Каталог перевірених земельних ділянок, схвалених юридичним аудитом ДЗК/ДРРП.
    /// Підтримує відбір за довільним поєднанням критеріїв (призначення, діапазон бюджету,
    /// площа, дати публікації, текстовий пошук) та багатоваріантне впорядкування (LINQ to Entities + LINQ to Objects).
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
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
    /// UC-01 «Подати запит на підбір ділянки» (include UC-02 «Автоматичний підбір ділянок за критеріями»).
    /// Створює заявку покупця на індивідуальний підбір ділянок рієлтором
    /// та негайно запускає автоматичний двигун підбору (AutoMatchingService).
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
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
    [Authorize(Roles = AppRoles.AnyClient)]
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
    [Authorize(Roles = AppRoles.AnyClient)]
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
    /// Картка ділянки: тут покупець бачить документи, висновок, фото й замовляє огляд (UC-03).
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
}
