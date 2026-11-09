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
[Authorize]
public class RealtorController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IAutoMatchingService _autoMatchingService;
    private readonly ILandAgencyQueries _queries;

    public RealtorController(
        LandAgencyContext context,
        IAutoMatchingService autoMatchingService,
        ILandAgencyQueries queries)
    {
        _db = context;
        _autoMatchingService = autoMatchingService;
        _queries = queries;
    }

    private async Task<AppUser?> GetCurrentRealtorAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var realtorId) && realtorId > 0)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == realtorId && u.Role == UserRole.Realtor);
        }

        return null;
    }

    /// <summary>
    /// Робоча черга заявок на підбір ділянок з автоматичним двигуном підбору.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
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
    /// Запуск ручного або повторного підбору для конкретної заявки клієнта.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
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

    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlot(
        int plotId,
        string? title,
        string? location,
        double? areaHectares,
        string? purpose,
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

            if (areaHectares.HasValue && areaHectares.Value > 0)
            {
                plot.AreaHectares = areaHectares.Value;
            }

            if (!string.IsNullOrWhiteSpace(purpose))
            {
                plot.Purpose = purpose.Trim();
            }

            if (priceUsd.HasValue && priceUsd.Value > 0)
            {
                plot.PriceUsd = priceUsd.Value;
            }

            if (description != null)
            {
                plot.Description = description.Trim();
            }

            plot.AssignedRealtorId = currentUser?.Id;
            await _db.SaveChangesAsync();
            await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);

            TempData["RealtorMessage"] = $"Дані земельної ділянки {plot.CadastralNumber} успішно оновлено.";
        }

        return RedirectToAction("Details", "Client", new { id = plotId });
    }

    [Authorize(Roles = nameof(UserRole.Realtor))]
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
            plot.AssignedRealtorId = currentUser?.Id;
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Комерційний опис ділянки {plot.CadastralNumber} оновлено.";
        }

        return RedirectToAction(nameof(Requests));
    }

    /// <summary>
    /// Реєстр запланованих та проведених виїзних оглядів ділянок з покупцями.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
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
    /// Оновлює стан огляду: узгоджує дату, фіксує успішний огляд або відмову покупця
    /// з поверненням запиту клієнта на повторний підбір.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
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
    /// Реєстр нотаріальних угод купівлі-продажу з візуальним відображенням стадій правового супроводу.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpGet]
    public async Task<IActionResult> Deals(int? plotId, int? buyerId)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var availablePlots = await _db.LandPlots
            .Include(p => p.Seller)
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .ToListAsync();

        var clients = await _db.Users
            .Where(u => u.Role == UserRole.Client)
            .ToListAsync();

        var deals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.LegalConclusion)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .OrderByDescending(d => d.Id)
            .ToListAsync();

        var viewModel = new RealtorDealsViewModel
        {
            CurrentUser = currentUser,
            Deals = deals,
            AvailablePlots = availablePlots,
            Clients = clients,
            PreselectedPlotId = plotId,
            PreselectedBuyerId = buyerId,
            PreselectedPrice = plotId.HasValue
                ? availablePlots.FirstOrDefault(p => p.Id == plotId.Value)?.PriceUsd
                : null
        };

        return View(viewModel);
    }

    /// <summary>
    /// Створює проєкт угоди: розраховує 3% комісію агентства, формує графік рахунків-фактур (IBAN)
    /// та направляє проект договору юристу для отримання правової візи.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
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
            existingDeal.Status != DealStatus.Cancelled &&
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
            var ibanClean = sellerBankIban.Trim().ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(ibanClean, @"^UA\d{27}$"))
            {
                TempData["RealtorError"] = "Невірний формат IBAN продавця. Вкажіть рахунок у форматі UA та 27 цифр.";
                return RedirectToAction(nameof(Deals));
            }

            plot.SellerBankIban = ibanClean;
            if (plot.Seller != null && string.IsNullOrWhiteSpace(plot.Seller.BankDetails))
            {
                plot.Seller.BankDetails = ibanClean;
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
            existingDeal.BuyerId = buyerId;
            existingDeal.RealtorId = currentUser?.Id;
            existingDeal.NotaryOffice = resolvedNotaryOffice;
            existingDeal.NotaryDate = resolvedNotaryDate;
            existingDeal.LawyerApprovalNotes = string.Empty;
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
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyClientPayment(int paymentId, string? verificationNotes)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && currentUser != null)
        {
            if (payment.SaleDeal?.Status == DealStatus.Cancelled)
            {
                TempData["RealtorError"] = "Неможливо підтвердити платіж за скасованою угодою.";
                return RedirectToAction(nameof(Deals));
            }

            payment.VerifyPayment(currentUser.Id, verificationNotes);
            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Банківське зарахування за рахунком {payment.InvoiceNumber} (код операції: {payment.PaymentReference}) успішно підтверджено.";
        }

        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// Відхилення поданого клієнтом коду квитанції у разі невідповідності банківській виписці.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClientPayment(int paymentId, string? rejectionNotes)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && currentUser != null)
        {
            if (payment.SaleDeal?.Status == DealStatus.CompletedAtNotary)
            {
                TempData["RealtorError"] = $"Неможливо відхилити платіж за угодою {payment.SaleDeal.DraftContractNumber}, за якою вже зареєстровано право власності в ДРРП.";
                return RedirectToAction(nameof(Deals));
            }

            payment.RejectPayment(currentUser.Id, rejectionNotes);
            await _db.SaveChangesAsync();
            TempData["RealtorError"] = $"Квитанцію за рахунком {payment.InvoiceNumber} відхилено. Клієнт може повторно надіслати коректний код транзакції.";
        }

        return RedirectToAction(nameof(Deals));
    }

    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDealToLawyer(int dealId)
    {
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
            if (deal.Status == DealStatus.CompletedAtNotary ||
                deal.Status == DealStatus.SignedAtNotary ||
                deal.Status == DealStatus.Cancelled)
            {
                TempData["RealtorError"] = $"Угода {deal.DraftContractNumber} перебуває у статусі «{deal.StatusDisplayName}» і не підлягає повторному направленню на візу.";
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
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
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
    /// Фіналізує угоду: перевіряє повне підтвердження всіх банківських розрахунків за угодою,
    /// фіксує перехід права власності в ДРРП, змінює статус ділянки на 'Sold'
    /// та автоматично скасовує всі інші активні заявки на виїзний огляд цього об'єкта.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteNotaryDeal(int dealId, string? notarialRegistryNumber)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (deal.Status != DealStatus.SignedAtNotary && deal.Status != DealStatus.CompletedAtNotary)
            {
                TempData["RealtorError"] = $"Неможливо зареєструвати право власності в ДРРП за угодою {deal.DraftContractNumber} до її нотаріального посвідчення (Етап 2).";
                return RedirectToAction(nameof(Deals));
            }

            if (!deal.IsFullyPaid)
            {
                var unpaidInvoices = string.Join(", ", deal.Payments.Where(p => !p.IsPaid).Select(p => p.InvoiceNumber));
                TempData["RealtorError"] = $"Державна реєстрація права власності в ДРРП неможлива до повного підтвердження всіх банківських розрахунків за договором (непідтверджені рахунки: {unpaidInvoices}).";
                return RedirectToAction(nameof(Deals));
            }

            if (!deal.RegisterOwnershipRight())
            {
                TempData["RealtorError"] = $"Помилка реєстрації права власності в ДРРП за угодою {deal.DraftContractNumber}.";
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

    /// <summary>
    /// Скасовує активну угоду до моменту реєстрації права власності в ДРРП.
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Realtor))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDeal(int dealId, string? reason)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal != null)
        {
            if (!deal.CancelDeal(reason ?? "Скасовано за ініціативою сторін"))
            {
                TempData["RealtorError"] = $"Угоду {deal.DraftContractNumber} вже завершено або скасовано.";
                return RedirectToAction(nameof(Deals));
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Угоду {deal.DraftContractNumber} скасовано. Земельна ділянка залишається відкритою у каталозі.";
        }

        return RedirectToAction(nameof(Deals));
    }

    [HttpGet]
    public async Task<IActionResult> PrintContract(int dealId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int currentUserId = int.TryParse(idClaim, out var parsedId) ? parsedId : 0;
        var currentUser = currentUserId > 0 ? await _db.Users.FindAsync(currentUserId) : null;
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

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
            return currentUser.Role == UserRole.Realtor
                ? RedirectToAction(nameof(Deals))
                : RedirectToAction("Cabinet", "Client");
        }

        bool isAuthorized = currentUser.Role == UserRole.Realtor ||
                            currentUser.Role == UserRole.Lawyer ||
                            deal.BuyerId == currentUser.Id ||
                            deal.LandPlot?.SellerId == currentUser.Id;

        if (!isAuthorized)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return View(deal);
    }

    /// <summary>
    /// Фінансова аналітика та результативність агентства за обраний період (день, місяць, півріччя або довільний).
    /// </summary>
    [Authorize(Roles = "Realtor,Admin")]
    [HttpGet]
    public async Task<IActionResult> Analytics(
        PeriodKind kind = PeriodKind.Month,
        DateOnly? date = null,
        DateOnly? from = null,
        DateOnly? to = null,
        TurnoverSort sort = TurnoverSort.Turnover,
        AgentSort? agentSort = null,
        bool? desc = null)
    {
        bool isDescending = desc ?? true;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = date ?? today;
        var periodFrom = from ?? today.AddMonths(-3);
        var periodTo = to ?? today;

        var period = kind == PeriodKind.Custom
            ? Period.Between(periodFrom, periodTo)
            : Period.Containing(kind, day);

        var actualAgentSort = agentSort ?? sort switch
        {
            TurnoverSort.Deals => AgentSort.Deals,
            TurnoverSort.Name => AgentSort.Name,
            _ => AgentSort.Turnover
        };

        var byPurpose = await _queries.TurnoverByPurposeAsync(period, sort, isDescending);
        var byAgent = await _queries.AgentPerformanceAsync(period, actualAgentSort, isDescending);

        var viewModel = new PeriodTurnoverViewModel
        {
            Kind = kind,
            Date = day,
            From = DateOnly.FromDateTime(period.From),
            To = DateOnly.FromDateTime(period.To).AddDays(-1),
            Sort = sort,
            Descending = isDescending,
            Period = period,
            ByPurpose = byPurpose,
            ByAgent = byAgent
        };

        ViewData["ActiveNav"] = "Analytics";
        return View(viewModel);
    }

    /// <summary>
    /// Реєстр клієнтів агентства (CRM).
    /// Забезпечує пошук за email, додавання, редагування та безпечне вилучення клієнтів.
    /// </summary>
    [Authorize(Roles = "Realtor,Admin")]
    [HttpGet]
    public async Task<IActionResult> Clients(string? searchEmail = null)
    {
        var clientsQuery = _db.Users
            .Where(u => u.Role == UserRole.Client)
            .Include(u => u.OwnedPlots)
            .Include(u => u.ViewingRequests)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchEmail))
        {
            var cleanSearch = searchEmail.Trim().ToLower();
            clientsQuery = clientsQuery.Where(u => u.Email.ToLower().Contains(cleanSearch));
        }

        var clients = await clientsQuery
            .OrderBy(u => u.Id)
            .ToListAsync();

        AppUser? foundUser = null;
        if (!string.IsNullOrWhiteSpace(searchEmail))
        {
            foundUser = clients.FirstOrDefault(u => u.Email.Equals(searchEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Client && u.Email == searchEmail.Trim());

            if (foundUser == null)
            {
                TempData["ErrorMessage"] = $"Клієнта з адресою '{searchEmail}' не знайдено.";
            }
        }

        var model = new UsersManagementViewModel
        {
            Users = clients,
            SearchEmail = searchEmail,
            FoundUser = foundUser
        };

        ViewData["ActiveNav"] = "Clients";
        return View(model);
    }

    /// <summary>
    /// Додавання нового клієнта з початковою заявкою в одній транзакції.
    /// </summary>
    [Authorize(Roles = "Realtor,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddClient(AddUserFormModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["ErrorMessage"] = "Заповніть усі обов'язкові поля: ПІБ, Email та пароль.";
            return RedirectToAction(nameof(Clients));
        }

        try
        {
            var user = await _queries.AddUserAsync(
                model.FullName, model.Email, model.Phone, UserRole.Client, model.Password, model.BankDetails);
            TempData["SuccessMessage"] = $"Успішно створено картку клієнта {user.FullName} ({user.Email}).";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Clients));
    }

    /// <summary>
    /// Оновлення персональних даних, контактів, бюджету та статусу активності клієнта.
    /// </summary>
    [Authorize(Roles = "Realtor,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClient(UpdateUserFormModel model)
    {
        try
        {
            await _queries.UpdateUserAsync(
                model.Id,
                model.FullName,
                model.Email,
                model.Phone,
                model.TaxId,
                model.BankDetails,
                model.MaxBudget,
                model.TargetPurpose,
                model.IsActive);
            TempData["SuccessMessage"] = $"Дані клієнта #{model.Id} ({model.FullName}) успішно оновлено.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Clients));
    }

    /// <summary>
    /// Безпечне вилучення користувача з контролем обмеження цілісності Restrict.
    /// </summary>
    [Authorize(Roles = "Realtor,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClient(int id)
    {
        try
        {
            string name = await _queries.DeleteUserAsync(id);
            TempData["SuccessMessage"] = $"Користувача {name} (ID={id}) успішно вилучено з бази даних.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Clients));
    }
}
