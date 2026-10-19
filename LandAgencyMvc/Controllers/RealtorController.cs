using System.Security.Claims;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Робоче місце сертифікованого рієлтора агентства.
/// Відповідає за автоматизований та індивідуальний підбір ділянок за запитами клієнтів,
/// узгодження та фіксацію виїзних оглядів, формування проектів угод з виставленням рахунків-фактур
/// та перевірку банківських оплат перед нотаріальним посвідченням.
/// </summary>
[Authorize(Roles = nameof(UserRole.Realtor))]
public class RealtorController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IAutoMatchingService _autoMatchingService;

    public RealtorController(LandAgencyContext context, IAutoMatchingService autoMatchingService)
    {
        _db = context;
        _autoMatchingService = autoMatchingService;
    }

    private async Task<AppUser?> GetCurrentRealtorAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var realtorId) && realtorId > 0)
        {
            var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Id == realtorId && u.Role == UserRole.Realtor);
            if (realtor != null)
            {
                ViewBag.CurrentUser = realtor;
                ViewBag.CurrentUserId = realtor.Id;
                return realtor;
            }
        }

        return null;
    }

    /// <summary>
    /// Робоча черга заявок на підбір ділянок з автоматичним двигуном підбору.
    /// </summary>
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

    /// <summary>
    /// Запуск ручного або повторного підбору для конкретної заявки клієнта.
    /// </summary>
    [HttpPost]
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

    [HttpPost]
    public async Task<IActionResult> UpdatePlotDescription(int plotId, string description)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var plot = await _db.LandPlots.FindAsync(plotId);
        if (plot != null)
        {
            plot.Description = (description ?? string.Empty).Trim();
            plot.AssignedRealtorId = currentUser?.Id;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Комерційний опис ділянки {plot.CadastralNumber} оновлено.";
        }

        return RedirectToAction(nameof(Requests));
    }

    /// <summary>
    /// Реєстр запланованих та проведених виїзних оглядів ділянок з покупцями.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Viewings()
    {
        await GetCurrentRealtorAsync();
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
    /// Оновлює стан огляду: узгоджує дату, фіксує успішний огляд або відмову покупця
    /// з поверненням запиту клієнта на повторний підбір.
    /// </summary>
    [HttpPost]
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
    /// Реєстр нотаріальних угод купівлі-продажу з візуальним відображенням стадій правового супроводу.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Deals(int? plotId, int? buyerId)
    {
        await GetCurrentRealtorAsync();
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

    /// <summary>
    /// Створює проєкт угоди: розраховує 3% комісію агентства, формує графік рахунків-фактур (IBAN)
    /// та направляє проект договору юристу для отримання правової візи.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDeal(
        int landPlotId,
        int buyerId,
        decimal finalPriceUsd,
        string draftContractNumber,
        string notaryOffice,
        DateTime notaryDate,
        string? buyerTaxId = null,
        string? sellerBankIban = null)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == landPlotId);
        var buyer = await _db.Users.FirstOrDefaultAsync(u => u.Id == buyerId && u.Role == UserRole.Client);

        if (plot == null || plot.Status != PlotStatus.VerifiedInCatalog)
        {
            TempData["RealtorError"] = "Оберіть перевірену в ДЗК земельну ділянку з каталогу для формування угоди.";
            return RedirectToAction(nameof(Deals));
        }

        if (buyer == null)
        {
            TempData["RealtorError"] = "Оберіть зареєстрованого покупця для формування угоди.";
            return RedirectToAction(nameof(Deals));
        }

        if (plot.SellerId == buyer.Id)
        {
            TempData["RealtorError"] = "Власник ділянки (Продавець) не може бути одночасно Покупцем у договорі купівлі-продажу.";
            return RedirectToAction(nameof(Deals));
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
            return RedirectToAction(nameof(Deals));
        }

        if (!string.IsNullOrWhiteSpace(buyerTaxId) && string.IsNullOrWhiteSpace(buyer.TaxId))
        {
            buyer.TaxId = buyerTaxId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sellerBankIban))
        {
            plot.SellerBankIban = sellerBankIban.Trim();
            if (plot.Seller != null && string.IsNullOrWhiteSpace(plot.Seller.BankDetails))
            {
                plot.Seller.BankDetails = sellerBankIban.Trim();
            }
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
            return RedirectToAction(nameof(Deals));
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

        TempData["RealtorMessage"] = $"Проєкт договору {deal.DraftContractNumber} створено, сформовано рахунки на оплату та передано юристу на погодження.";
        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// Перевірка та підтвердження банківського зарахування коштів за квитанцією клієнта.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> VerifyClientPayment(int paymentId, string? verificationNotes)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && currentUser != null)
        {
            payment.VerifyPayment(currentUser.Id, verificationNotes);
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Банківське зарахування за рахунком {payment.InvoiceNumber} (код операції: {payment.PaymentReference}) успішно підтверджено.";
        }

        return RedirectToAction(nameof(Deals));
    }

    [HttpPost]
    public async Task<IActionResult> SendDealToLawyer(int dealId)
    {
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
            if (deal.Status == DealStatus.CompletedAtNotary || deal.Status == DealStatus.SignedAtNotary)
            {
                TempData["RealtorError"] = $"Угода {deal.DraftContractNumber} вже посвідчена у нотаріуса і не потребує повторного візування.";
                return RedirectToAction(nameof(Deals));
            }

            deal.Status = DealStatus.DraftPreparedByRealtor;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Проєкт договору {deal.DraftContractNumber} передано юристу на візування.";
        }

        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// Нотаріальне підписання договору: перевіряє правову візу юриста та факт зарахування гарантійного внеску/комісії.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SignDealAtNotary(int dealId, string? notarialRegistryNumber)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (!deal.IsAgencyFeePaid)
            {
                var agencyPayment = deal.AgencyPayment;
                TempData["RealtorError"] = $"Неможливо призначити нотаріальне посвідчення до підтвердження зарахування депозиту / комісії агентства (Рахунок {agencyPayment?.InvoiceNumber ?? "INV"}). Очікується банківська виписка.";
                return RedirectToAction(nameof(Deals));
            }

            var regNum = !string.IsNullOrWhiteSpace(notarialRegistryNumber)
                ? notarialRegistryNumber.Trim()
                : $"НР-{DateTime.Now:yyyy}-{deal.Id:D4}";

            if (!deal.SignByParties(regNum))
            {
                TempData["RealtorError"] = $"Неможливо посвідчити договір {deal.DraftContractNumber} у нотаріуса без попереднього погодження (візи) юриста.";
                return RedirectToAction(nameof(Deals));
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Підписання договору {deal.DraftContractNumber} у нотаріуса (реєстровий №{regNum}) зафіксовано.";
        }

        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// Фіналізує угоду: фіксує перехід права власності в ДРРП, змінює статус ділянки на 'Sold'
    /// та автоматично скасовує всі інші активні заявки на виїзний огляд цього об'єкта.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CompleteNotaryDeal(int dealId, string? notarialRegistryNumber)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (!deal.RegisterOwnershipRight())
            {
                TempData["RealtorError"] = $"Неможливо зареєструвати право власності в ДРРП за угодою {deal.DraftContractNumber} до її нотаріального посвідчення (Етап 2).";
                return RedirectToAction(nameof(Deals));
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
                p.Status = PaymentStatus.Confirmed;
            }

            var pendingViewings = await _db.ViewingRequests
                .Where(v => v.LandPlotId == deal.LandPlotId &&
                            v.Status != ViewingStatus.Completed &&
                            v.Status != ViewingStatus.Cancelled)
                .ToListAsync();
            foreach (var v in pendingViewings)
            {
                v.CancelViewing("Ділянку продано (угоду посвідчено у нотаріуса та зареєстровано в ДРРП).");
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Право власності за угодою {deal.DraftContractNumber} зареєстровано в ДРРП. Ділянку переведено у статус «Продано».";
        }

        return RedirectToAction(nameof(Deals));
    }

    [HttpGet]
    public async Task<IActionResult> PrintContract(int dealId)
    {
        await GetCurrentRealtorAsync();
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
            return RedirectToAction(nameof(Deals));
        }

        return View(deal);
    }
}
