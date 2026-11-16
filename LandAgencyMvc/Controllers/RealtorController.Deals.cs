using System.Security.Claims;
using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// UC-14 «Підготувати пакет документів для нотаріуса»: реєстр угод і рахунків.
    /// Реєстр нотаріальних угод купівлі-продажу з візуальним відображенням стадій правового супроводу.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Deals(int? plotId, int? buyerId)
    {
        var currentUser = await GetCurrentRealtorAsync();
        var availablePlots = await _db.LandPlots
            .Include(p => p.Seller)
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .ToListAsync();

        var clients = await _db.Users
            .Where(u => u.Role == UserRole.Buyer)
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
    /// UC-14 «Підготувати пакет документів для нотаріуса» (include UC-15 «Автоматичний розрахунок комісії агентства»).
    /// Створює проєкт угоди: розраховує 3% комісію агентства, формує графік рахунків-фактур (IBAN)
    /// та направляє проект договору юристу для отримання правової візи.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
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
        var buyer = await _db.Users.FirstOrDefaultAsync(u => u.Id == buyerId && u.Role == UserRole.Buyer);

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

        await using var transaction = await _db.Database.BeginTransactionAsync();

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
            await transaction.CommitAsync();

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
        await transaction.CommitAsync();

        TempData["RealtorMessage"] = $"Проєкт договору {deal.DraftContractNumber} створено, сформовано рахунки на оплату та передано юристу на погодження.";
        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// UC-06 «Здійснити оплату / завдаток через банк»: рієлтор підтверджує зарахування за банківською випискою.
    /// Перевірка та підтвердження банківського зарахування коштів за квитанцією клієнта.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
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

            if (!payment.VerifyPayment(currentUser.Id, verificationNotes))
            {
                TempData["RealtorError"] = $"Платіж за рахунком {payment.InvoiceNumber} у статусі «{payment.StatusDisplayName}» підтвердити не можна: підтверджується лише платіж, поданий покупцем.";
                return RedirectToAction(nameof(Deals));
            }

            await _db.SaveChangesAsync();
            TempData["RealtorMessage"] = $"Банківське зарахування за рахунком {payment.InvoiceNumber} (код операції: {payment.PaymentReference}) успішно підтверджено.";
        }

        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// UC-06 «Здійснити оплату / завдаток через банк»: рієлтор відхиляє платіж із неправильними реквізитами.
    /// Відхилення поданого клієнтом коду квитанції у разі невідповідності банківській виписці.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
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

            if (!payment.RejectPayment(currentUser.Id, rejectionNotes))
            {
                TempData["RealtorError"] = $"Платіж за рахунком {payment.InvoiceNumber} у статусі «{payment.StatusDisplayName}» відхилити не можна: відхиляється лише платіж, що очікує перевірки.";
                return RedirectToAction(nameof(Deals));
            }

            await _db.SaveChangesAsync();
            TempData["RealtorError"] = $"Квитанцію за рахунком {payment.InvoiceNumber} відхилено. Клієнт може повторно надіслати коректний код транзакції.";
        }

        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// UC-14 «Підготувати пакет документів для нотаріуса»: передача проєкту договору юристу на візу.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
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
    /// Скасовує активну угоду до моменту реєстрації права власності в ДРРП.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
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

    /// <summary>
    /// UC-14 «Підготувати пакет документів для нотаріуса»: друкована форма договору.
    /// </summary>
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
    /// Форма редагування чернетки угоди.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> EditDeal(int id)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deal == null)
        {
            return NotFound();
        }

        if (!deal.IsEditable)
        {
            TempData["RealtorError"] = $"Угоду у стані «{deal.StatusDisplayName}» або з проведеною оплатою змінювати заборонено.";
            return RedirectToAction(nameof(Deals));
        }

        var vm = new DealEditViewModel
        {
            Id = deal.Id,
            Version = deal.Version,
            DraftContractNumber = deal.DraftContractNumber,
            PlotTitle = deal.LandPlot?.Title ?? "—",
            CadastralNumber = deal.LandPlot?.CadastralNumber ?? "—",
            BuyerName = deal.Buyer?.FullName ?? "—",
            FinalPriceUsd = deal.FinalPriceUsd,
            NotaryOffice = deal.NotaryOffice,
            NotaryDate = deal.NotaryDate
        };

        return View(vm);
    }

    /// <summary>
    /// Збереження змінених умов угоди з перерахунком комісії та рахунків.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDeal(int id, DealEditViewModel vm)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deal == null)
        {
            return NotFound();
        }

        if (!deal.IsEditable)
        {
            TempData["RealtorError"] = "Неможливо змінити погоджену або оплачену угоду.";
            return RedirectToAction(nameof(Deals));
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        _db.UseSubmittedVersion(deal, vm.Version);
        deal.UpdateDraftTerms(vm.FinalPriceUsd, vm.NotaryOffice, vm.NotaryDate);
        await _db.SaveChangesAsync();

        TempData["RealtorMessage"] = $"Умови договору №{deal.DraftContractNumber} оновлено: ціна ${deal.FinalPriceUsd:N0}, комісія (3%) ${deal.AgencyCommissionUsd:N2}. Рахунки перераховано.";
        return RedirectToAction(nameof(Deals));
    }

    /// <summary>
    /// Сторінка підтвердження каскадного вилучення чернетки угоди разом з рахунками.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> DeleteDeal(int id)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deal == null)
        {
            return NotFound();
        }

        return View(deal);
    }

    /// <summary>
    /// Каскадне вилучення угоди та її платежів.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost, ActionName("DeleteDealConfirmed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDealConfirmed(int id)
    {
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deal == null)
        {
            return NotFound();
        }

        if (!deal.CanBeDeleted)
        {
            TempData["RealtorError"] = $"Угоду у стані «{deal.StatusDisplayName}» або з підтвердженими оплатами видалити не можна (дозволено лише скасування).";
            return RedirectToAction(nameof(Deals));
        }

        int paymentsCount = deal.Payments.Count;
        string num = deal.DraftContractNumber;

        if (deal.Payments.Any())
        {
            _db.Payments.RemoveRange(deal.Payments);
        }

        _db.SaleDeals.Remove(deal);
        await _db.SaveChangesAsync();

        TempData["RealtorMessage"] = $"Чернетку угоди №{num} разом із рахунками ({paymentsCount}) каскадно вилучено з бази даних.";
        return RedirectToAction(nameof(Deals));
    }
}
