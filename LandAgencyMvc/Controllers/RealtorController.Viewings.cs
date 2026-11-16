using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// UC-13 «Організувати виїзд та показ ділянки на місці»: перелік заявок на огляд.
    /// Реєстр запланованих та проведених виїзних оглядів ділянок з покупцями.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Viewings()
    {
        var viewings = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(v => v.Buyer)
            .Include(v => v.Realtor)
            .OrderByDescending(v => v.Id)
            .ToListAsync();
        return View(viewings);
    }

    /// <summary>
    /// UC-13 «Організувати виїзд та показ ділянки на місці»: підтвердження, проведення або скасування огляду.
    /// Оновлює стан огляду: узгоджує дату, фіксує успішний огляд або відмову покупця
    /// з поверненням запиту клієнта на повторний підбір.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateViewingStatus(int viewingId, ViewingStatus status, string? resultNotes)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var viewing = await _db.ViewingRequests.FindAsync(viewingId);
        if (viewing != null)
        {
            switch (status)
            {
                case ViewingStatus.Scheduled:
                    viewing.ConfirmViewing(currentUser?.Id);
                    break;
                case ViewingStatus.Completed:
                    viewing.CompleteViewing(true, resultNotes ?? "Огляд меж та оригіналів документів проведено, покупець підтвердив намір купівлі.");
                    break;
                case ViewingStatus.PlotRejectedByBuyer:
                    var cleanNotes = !string.IsNullOrWhiteSpace(resultNotes)
                        ? resultNotes.Trim()
                        : "Ділянка не підійшла покупцю за підсумками виїзного огляду. Уточнено критерії пошуку.";
                    viewing.CompleteViewing(false, cleanNotes);

                    var buyerReq = await _db.SelectionRequests
                        .Where(r => r.BuyerId == viewing.BuyerId)
                        .OrderByDescending(r => r.Id)
                        .FirstOrDefaultAsync();
                    if (buyerReq != null)
                    {
                        buyerReq.IsProcessedByRealtor = false;
                        buyerReq.RealtorNotes = $"Потребує повторного підбору (зауваження після огляду: {cleanNotes})";
                    }
                    break;
                case ViewingStatus.Cancelled:
                    viewing.CancelViewing(resultNotes ?? "Скасовано рієлтором за погодженням сторін.");
                    break;
                default:
                    viewing.Status = status;
                    break;
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Статус заявки на огляд #{viewing.Id} оновлено.";
        }

        return RedirectToAction(nameof(Viewings));
    }

    /// <summary>
    /// UC-13 «Організувати виїзд та показ ділянки на місці»: рієлтор призначає огляд сам.
    /// Призначення виїзного огляду ділянки з клієнтом.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateViewingByRealtor(int landPlotId, int buyerId, DateTime preferredDate, string timeSlot, string? comment)
    {
        var realtor = await GetCurrentRealtorAsync();
        var viewing = new ViewingRequest
        {
            LandPlotId = landPlotId,
            BuyerId = buyerId,
            RealtorId = realtor?.Id,
            PreferredDate = preferredDate,
            TimeSlot = (timeSlot ?? "12:00").Trim(),
            Comment = (comment ?? string.Empty).Trim(),
            Status = ViewingStatus.Scheduled,
            CreatedAt = DateTime.Now
        };

        _db.ViewingRequests.Add(viewing);
        await _db.SaveChangesAsync();

        TempData["RealtorMessage"] = $"Огляд ділянки #{landPlotId} на {viewing.PreferredDate:dd.MM.yyyy} о {viewing.TimeSlot} заплановано.";
        return RedirectToAction(nameof(Viewings));
    }

    /// <summary>
    /// UC-13 «Організувати виїзд та показ ділянки на місці»: зміна дати, часу й статусу огляду.
    /// Коригування параметрів виїзного огляду.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditViewing(int viewingId, DateTime preferredDate, string timeSlot, ViewingStatus status, string? comment)
    {
        var viewing = await _db.ViewingRequests.FindAsync(viewingId);
        if (viewing != null)
        {
            viewing.PreferredDate = preferredDate;
            viewing.TimeSlot = (timeSlot ?? "12:00").Trim();
            viewing.Status = status;
            if (comment != null) viewing.Comment = comment.Trim();

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Дані виїзного огляду #{viewingId} успішно оновлено.";
        }

        return RedirectToAction(nameof(Viewings));
    }

    /// <summary>
    /// UC-13 «Організувати виїзд та показ ділянки на місці»: вилучення заявки.
    /// Фізичне вилучення запису про огляд.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteViewing(int viewingId)
    {
        var viewing = await _db.ViewingRequests.FindAsync(viewingId);
        if (viewing != null)
        {
            _db.ViewingRequests.Remove(viewing);
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Запис про огляд #{viewingId} вилучено з графіка.";
        }

        return RedirectToAction(nameof(Viewings));
    }
}
