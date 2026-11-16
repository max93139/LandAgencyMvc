using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// Черга запитів на підбір (UC-01): рієлтор бачить результат UC-02 та підібрані ділянки.
    /// Робоча черга заявок на підбір ділянок з автоматичним двигуном підбору.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Requests(int? matchedRequestId)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var requests = await _db.SelectionRequests
            .Include(r => r.Buyer)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        var allPlots = await _db.LandPlots
            .Include(p => p.Seller)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        var matchedPreview = new List<LandPlot>();
        if (matchedRequestId.HasValue)
        {
            var selectedReq = requests.FirstOrDefault(r => r.Id == matchedRequestId.Value);
            if (selectedReq != null)
            {
                var ids = selectedReq.GetMatchedPlotIds();
                matchedPreview = allPlots.Where(p => ids.Contains(p.Id)).ToList();
            }
        }

        var viewModel = new RealtorRequestsViewModel
        {
            CurrentUser = currentUser,
            SelectionRequests = requests,
            AllPlots = allPlots,
            SelectedRequestId = matchedRequestId,
            MatchedPlotsPreview = matchedPreview
        };

        return View(viewModel);
    }

    /// <summary>
    /// UC-02 «Автоматичний підбір ділянок за критеріями»: ручний перерахунок збігів рієлтором.
    /// Запуск ручного або повторного підбору для конкретної заявки клієнта.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MatchPlotsForRequest(int requestId, string? realtorNotes)
    {
        var currentUser = await GetCurrentRealtorAsync();
        int matchedCount = await _autoMatchingService.MatchRequestAsync(requestId);

        if (!string.IsNullOrWhiteSpace(realtorNotes))
        {
            var req = await _db.SelectionRequests.FindAsync(requestId);
            if (req != null)
            {
                req.ProcessedByRealtorId = currentUser?.Id;
                req.RealtorNotes = realtorNotes.Trim();
                await _db.SaveChangesAsync();
            }
        }

        TempData["RealtorMessage"] = $"Запит клієнта #{requestId} опрацьовано автоматичним двигуном. Підібрано ділянок: {matchedCount}.";
        return RedirectToAction(nameof(Requests), new { matchedRequestId = requestId });
    }

    /// <summary>
    /// Створення нової заявки клієнта на підбір ділянки.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSelectionRequest(int buyerId, string desiredDistrict, double minAreaHa, decimal maxBudgetUsd, string purpose)
    {
        var realtor = await GetCurrentRealtorAsync();
        var req = new SelectionRequest
        {
            BuyerId = buyerId,
            DesiredDistrict = (desiredDistrict ?? string.Empty).Trim(),
            MinAreaHa = minAreaHa > 0 ? minAreaHa : 0.10,
            MaxBudgetUsd = maxBudgetUsd > 0 ? maxBudgetUsd : 30000m,
            Purpose = (purpose ?? string.Empty).Trim(),
            ProcessedByRealtorId = realtor?.Id,
            CreatedAt = DateTime.Now
        };

        _db.SelectionRequests.Add(req);
        await _db.SaveChangesAsync();
        await _autoMatchingService.MatchRequestAsync(req.Id);

        TempData["RealtorMessage"] = $"Заявку покупця #{req.Id} створено та автоматично оброблено.";
        return RedirectToAction(nameof(Requests), new { matchedRequestId = req.Id });
    }

    /// <summary>
    /// Коригування параметрів заявки на підбір.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSelectionRequest(int id, string desiredDistrict, double minAreaHa, decimal maxBudgetUsd, string purpose, string? realtorNotes)
    {
        var req = await _db.SelectionRequests.FindAsync(id);
        if (req != null)
        {
            req.DesiredDistrict = (desiredDistrict ?? string.Empty).Trim();
            if (minAreaHa > 0) req.MinAreaHa = minAreaHa;
            if (maxBudgetUsd > 0) req.MaxBudgetUsd = maxBudgetUsd;
            if (!string.IsNullOrWhiteSpace(purpose)) req.Purpose = purpose.Trim();
            if (realtorNotes != null) req.RealtorNotes = realtorNotes.Trim();

            await _db.SaveChangesAsync();
            await _autoMatchingService.MatchRequestAsync(req.Id);

            TempData["RealtorMessage"] = $"Параметри запиту #{req.Id} оновлено та перераховано підбір.";
        }

        return RedirectToAction(nameof(Requests), new { matchedRequestId = id });
    }

    /// <summary>
    /// Вилучення заявки на підбір.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelectionRequest(int id)
    {
        var req = await _db.SelectionRequests.FindAsync(id);
        if (req != null)
        {
            _db.SelectionRequests.Remove(req);
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Заявку на підбір #{id} вилучено з черги.";
        }

        return RedirectToAction(nameof(Requests));
    }
}
