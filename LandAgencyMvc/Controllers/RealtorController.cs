using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public class RealtorController : Controller
{
    private readonly LandAgencyContext _db;

    public RealtorController(LandAgencyContext context)
    {
        _db = context;
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
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == resolvedId && u.Role == UserRole.Realtor);
        }

        user ??= await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);

        ViewBag.CurrentUser = user;
        ViewBag.CurrentUserId = user?.Id ?? 0;
        return user;
    }

    [HttpGet]
    public async Task<IActionResult> Requests(int userId, int? matchedRequestId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
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
            if (selectedReq != null && !string.IsNullOrWhiteSpace(selectedReq.MatchedPlotIds))
            {
                var ids = selectedReq.MatchedPlotIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
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

    [HttpPost]
    public async Task<IActionResult> MatchPlotsForRequest(int userId, int requestId, string? realtorNotes)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var req = await _db.SelectionRequests.FindAsync(requestId);
        if (req != null)
        {
            var verifiedPlots = await _db.LandPlots
                .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
                .ToListAsync();

            var finalMatches = req.MatchPlots(verifiedPlots, currentUser?.Id, realtorNotes);

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Запит клієнта #{req.Id} опрацьовано. Підібрано ділянок: {finalMatches.Count}.";
        }

        return RedirectToAction(nameof(Requests), new { userId = currentUser?.Id ?? userId, matchedRequestId = requestId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePlotDescription(int userId, int plotId, string description)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots.FindAsync(plotId);
        if (plot != null)
        {
            plot.Description = (description ?? string.Empty).Trim();
            plot.AssignedRealtorId = currentUser?.Id;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Комерційний опис ділянки {plot.CadastralNumber} оновлено.";
        }

        return RedirectToAction(nameof(Requests), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet]
    public async Task<IActionResult> Viewings(int userId)
    {
        await SetCurrentUserAsync(userId);
        var viewings = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(v => v.Buyer)
            .Include(v => v.Realtor)
            .OrderByDescending(v => v.Id)
            .ToListAsync();
        return View(viewings);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateViewingStatus(int userId, int viewingId, ViewingStatus status, string? resultNotes)
    {
        var currentUser = await SetCurrentUserAsync(userId);
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

        return RedirectToAction(nameof(Viewings), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet]
    public async Task<IActionResult> Deals(int userId, int? plotId, int? buyerId)
    {
        await SetCurrentUserAsync(userId);
        var availablePlots = await _db.LandPlots
            .Include(p => p.Seller)
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .ToListAsync();

        ViewBag.AvailablePlots = availablePlots;
        ViewBag.Clients = await _db.Users
            .Where(u => u.Role == UserRole.Client)
            .ToListAsync();
        ViewBag.PreselectedPlotId = plotId;
        ViewBag.PreselectedBuyerId = buyerId;
        ViewBag.PreselectedPrice = plotId.HasValue
            ? availablePlots.FirstOrDefault(p => p.Id == plotId.Value)?.PriceUsd
            : null;

        var deals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.LegalConclusion)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
        return View(deals);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeal(
        int userId,
        int landPlotId,
        int buyerId,
        decimal finalPriceUsd,
        string draftContractNumber,
        string notaryOffice,
        DateTime notaryDate)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == landPlotId);
        var buyer = await _db.Users.FirstOrDefaultAsync(u => u.Id == buyerId && u.Role == UserRole.Client);

        if (plot == null || plot.Status != PlotStatus.VerifiedInCatalog)
        {
            TempData["RealtorError"] = "Оберіть перевірену в ДЗК земельну ділянку з каталогу для формування угоди.";
            return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
        }

        if (buyer == null)
        {
            TempData["RealtorError"] = "Оберіть зареєстрованого покупця для формування угоди.";
            return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
        }

        if (plot.SellerId == buyer.Id)
        {
            TempData["RealtorError"] = "Власник ділянки (Продавець) не може бути одночасно Покупцем у договорі купівлі-продажу.";
            return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
        }

        var existingDeal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.LandPlotId == landPlotId);

        if (existingDeal != null &&
            (existingDeal.Status == DealStatus.SignedAtNotary ||
             existingDeal.Status == DealStatus.CompletedAtNotary ||
             existingDeal.BuyerId != buyerId))
        {
            TempData["RealtorError"] = $"По ділянці {plot.CadastralNumber} вже відкрито інший активний або завершений договір купівлі-продажу.";
            return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
        }

        string contractNum = !string.IsNullOrWhiteSpace(draftContractNumber)
            ? draftContractNumber.Trim()
            : $"ДГ-{DateTime.Now:yyyy}-{landPlotId:D3}";
        decimal agreedPrice = finalPriceUsd > 0 ? finalPriceUsd : plot.PriceUsd;
        string resolvedNotaryOffice = !string.IsNullOrWhiteSpace(notaryOffice)
            ? notaryOffice.Trim()
            : "Перша Київська державна нотаріальна контора";
        DateTime resolvedNotaryDate = notaryDate == default ? DateTime.Today.AddDays(3).AddHours(11) : notaryDate;

        if (existingDeal != null)
        {
            existingDeal.RealtorId = currentUser?.Id;
            existingDeal.NotaryOffice = resolvedNotaryOffice;
            existingDeal.NotaryDate = resolvedNotaryDate;
            existingDeal.PrepareContractDraft(contractNum, agreedPrice, currentUser?.CommissionRate ?? 3.0);

            if (existingDeal.Payments.Any())
            {
                _db.Payments.RemoveRange(existingDeal.Payments);
            }

            _db.Payments.AddRange(existingDeal.CreateDefaultPayments(
                plot.SellerBankIban ?? plot.Seller?.BankDetails,
                isPaid: false,
                agencyIban: currentUser?.BankDetails));
            await _db.SaveChangesAsync();

            TempData["RealtorMessage"] = $"Проєкт договору {existingDeal.DraftContractNumber} оновлено та передано юристу на погодження.";
            return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
        }

        var deal = new SaleDeal
        {
            LandPlotId = landPlotId,
            BuyerId = buyerId,
            RealtorId = currentUser?.Id,
            NotaryOffice = resolvedNotaryOffice,
            NotaryDate = resolvedNotaryDate
        };

        deal.PrepareContractDraft(contractNum, agreedPrice, currentUser?.CommissionRate ?? 3.0);

        _db.SaleDeals.Add(deal);
        await _db.SaveChangesAsync();

        _db.Payments.AddRange(deal.CreateDefaultPayments(
            plot.SellerBankIban ?? plot.Seller?.BankDetails,
            isPaid: false,
            agencyIban: currentUser?.BankDetails));
        await _db.SaveChangesAsync();

        TempData["RealtorMessage"] = $"Проєкт договору {deal.DraftContractNumber} створено та передано юристу на погодження.";
        return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> SendDealToLawyer(int userId, int dealId)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
            if (deal.Status == DealStatus.CompletedAtNotary || deal.Status == DealStatus.SignedAtNotary)
            {
                TempData["RealtorError"] = $"Угода {deal.DraftContractNumber} вже посвідчена у нотаріуса і не потребує повторного візування.";
                return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
            }

            deal.Status = DealStatus.DraftPreparedByRealtor;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Проєкт договору {deal.DraftContractNumber} передано юристу на візування.";
        }

        return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> SignDealAtNotary(int userId, int dealId, string? notarialRegistryNumber)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
            var regNum = !string.IsNullOrWhiteSpace(notarialRegistryNumber)
                ? notarialRegistryNumber.Trim()
                : $"НР-{DateTime.Now:yyyy}-{deal.Id:D4}";
            if (!deal.SignByParties(regNum))
            {
                TempData["RealtorError"] = $"Неможливо посвідчити договір {deal.DraftContractNumber} у нотаріуса без попереднього погодження (візи) юриста.";
                return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Підписання договору {deal.DraftContractNumber} у нотаріуса (реєстровий №{regNum}) зафіксовано.";
        }

        return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
    }

    [HttpPost]
    public async Task<IActionResult> CompleteNotaryDeal(int userId, int dealId, string? notarialRegistryNumber)
    {
        var currentUser = await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (!deal.RegisterOwnershipRight())
            {
                TempData["RealtorError"] = $"Неможливо зареєструвати право власності в ДРРП за угодою {deal.DraftContractNumber} до її нотаріального посвідчення (Етап 2).";
                return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
            }

            if (!string.IsNullOrWhiteSpace(notarialRegistryNumber))
            {
                deal.NotarialRegistryNumber = notarialRegistryNumber.Trim();
            }
            else if (string.IsNullOrWhiteSpace(deal.NotarialRegistryNumber))
            {
                deal.NotarialRegistryNumber = $"ДРРП-{DateTime.Now:yyyy}-{deal.Id:D4}";
            }

            foreach (var p in deal.Payments)
            {
                p.IsPaid = true;
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Право власності за угодою {deal.DraftContractNumber} зареєстровано в ДРРП. Ділянку переведено у статус «Продано».";
        }

        return RedirectToAction(nameof(Deals), new { userId = currentUser?.Id ?? userId });
    }

    [HttpGet]
    public async Task<IActionResult> PrintContract(int dealId, int userId)
    {
        await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.LegalConclusion)
            .Include(d => d.Buyer)
            .Include(d => d.Realtor)
            .Include(d => d.Lawyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Deals), new { userId });
        }

        return View(deal);
    }
}
