using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class ClientController
{
    /// <summary>
    /// UC-03 «Замовити виїзний огляд ділянки».
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

    /// <summary>
    /// Скасування покупцем власної заявки з UC-03 «Замовити виїзний огляд ділянки».
    /// </summary>
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
}
