using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class ClientController
{
    /// <summary>
    /// Покупець після проведеного огляду ініціює проєкт угоди; далі UC-14 веде рієлтор, а UC-05 завершує покупець.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPurchaseFromViewing(int viewingId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var viewing = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .ThenInclude(p => p!.Seller)
            .FirstOrDefaultAsync(v => v.Id == viewingId && v.BuyerId == currentUserId);

        if (viewing != null && viewing.LandPlot != null)
        {
            if (viewing.LandPlot.SellerId == viewing.BuyerId)
            {
                TempData["CabinetError"] = "Власник ділянки не може укласти угоду купівлі власного об'єкта.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (viewing.LandPlot.Status == PlotStatus.Sold)
            {
                TempData["CabinetError"] = "Ця земельна ділянка вже продана.";
                return RedirectToAction(nameof(Cabinet));
            }

            var realtor = await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Realtor);

            var existingDeal = await _db.SaleDeals
                .Include(d => d.Payments)
                .FirstOrDefaultAsync(d => d.LandPlotId == viewing.LandPlotId && d.Status != DealStatus.Cancelled);

            if (existingDeal == null)
            {
                int previousDeals = await _db.SaleDeals.CountAsync(d => d.LandPlotId == viewing.LandPlotId);
                string contractNumber = $"ДГ-{DateTime.Now:yyyy}-{viewing.LandPlotId:D3}" + (previousDeals > 0 ? $"-{previousDeals + 1}" : string.Empty);

                await using var transaction = await _db.Database.BeginTransactionAsync();

                var deal = new SaleDeal
                {
                    LandPlotId = viewing.LandPlotId,
                    BuyerId = viewing.BuyerId,
                    RealtorId = viewing.RealtorId ?? realtor?.Id,
                    NotaryOffice = "Перша Київська державна нотаріальна контора",
                    NotaryDate = DateTime.Today.AddDays(3).AddHours(11)
                };

                deal.PrepareContractDraft(
                    contractNumber,
                    viewing.LandPlot.PriceUsd,
                    realtor?.CommissionRate ?? 3.0);

                _db.SaleDeals.Add(deal);
                await _db.SaveChangesAsync();

                _db.Payments.AddRange(deal.CreateDefaultPayments(
                    viewing.LandPlot.SellerBankIban ?? viewing.LandPlot.Seller?.BankDetails,
                    isPaid: false,
                    senderIban: currentUser?.BankDetails,
                    agencyIban: realtor?.BankDetails));
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["CabinetMessage"] = $"Проєкт нотаріальної угоди {deal.DraftContractNumber} успішно ініційовано на підставі проведеного огляду.";
            }
            else
            {
                TempData["CabinetMessage"] = $"По ділянці {viewing.LandPlot.CadastralNumber} вже існує проект угоди {existingDeal.DraftContractNumber}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-06 «Здійснити оплату / завдаток через банк»: покупець повідомляє реквізити платежу за рахунком.
    /// Подання клієнтом реквізитів банківської оплати (код квитанції, IBAN) за виставленим рахунком-фактурою.
    /// Переводить платіж у статус очікування підтвердження виписки агентством.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitPayment(int paymentId, string paymentReference, string? senderIban)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var payment = await _db.Payments
            .Include(p => p.SaleDeal)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment != null && payment.SaleDeal != null && payment.SaleDeal.BuyerId == currentUserId)
        {
            if (payment.SaleDeal.Status == DealStatus.Cancelled)
            {
                TempData["CabinetError"] = "Угоду скасовано. Подання реквізитів оплати за скасованою угодою неможливе.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (payment.IsPaid)
            {
                TempData["CabinetError"] = $"Рахунок {payment.InvoiceNumber} вже підтверджено агентством.";
                return RedirectToAction(nameof(Cabinet));
            }

            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                TempData["CabinetError"] = "Вкажіть номер платіжної інструкції або код квитанції банку.";
                return RedirectToAction(nameof(Cabinet));
            }

            string resolvedSenderIban = (senderIban ?? currentUser?.BankDetails ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(resolvedSenderIban) && !System.Text.RegularExpressions.Regex.IsMatch(resolvedSenderIban, @"^UA\d{27}$"))
            {
                TempData["CabinetError"] = "Невірний формат IBAN платника. Вкажіть рахунок у форматі UA та 27 цифр.";
                return RedirectToAction(nameof(Cabinet));
            }

            payment.SubmitPayment(paymentReference, resolvedSenderIban);
            await _db.SaveChangesAsync();

            TempData["CabinetMessage"] = $"Реквізити оплати за рахунком {payment.InvoiceNumber} (транзакція {payment.PaymentReference}) надіслано на верифікацію банківської виписки рієлтору.";
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-06 «Здійснити оплату / завдаток через банк» (спрощена форма для кабінету).
    /// Альтернативний обробник для зворотної сумісності форми кабінету.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int dealId, string? payerIban, string? paymentReference)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.BuyerId == currentUserId);

        if (deal != null && deal.Status != DealStatus.Cancelled)
        {
            string reference = !string.IsNullOrWhiteSpace(paymentReference)
                ? paymentReference.Trim()
                : $"TRX-{DateTime.Now:yyyyMMdd}-{deal.Id:D4}";

            var pendingPayment = deal.Payments.FirstOrDefault(p => !p.IsPaid);
            if (pendingPayment != null)
            {
                pendingPayment.SubmitPayment(reference, payerIban ?? currentUser?.BankDetails);
                await _db.SaveChangesAsync();
                TempData["CabinetMessage"] = $"Повідомлення про оплату за договором {deal.DraftContractNumber} передано рієлтору. Код транзакції: {reference}.";
            }
        }

        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// UC-05 «Підписати договір купівлі-продажу у нотаріуса» (include UC-06 «Здійснити оплату / завдаток через банк»).
    /// Покупець підписує договір, коли юрист погодив його, а банківська виписка про оплату рахунку агентства підтверджена.
    /// Реєстровий номер нотаріальної дії присвоюється автоматично.
    /// </summary>
    [Authorize(Roles = AppRoles.Buyer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignContractAtNotary(int dealId)
    {
        var currentUser = await GetCurrentUserAsync();
        int currentUserId = currentUser?.Id ?? 0;
        var deal = await _db.SaleDeals
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId && d.BuyerId == currentUserId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        if (!deal.CanSignAtNotary)
        {
            TempData["CabinetError"] = $"Договір {deal.DraftContractNumber} ще не погоджено юристом або вже підписаний.";
            return RedirectToAction(nameof(Cabinet));
        }

        if (!deal.IsAgencyFeePaid)
        {
            TempData["CabinetError"] = $"Підписання можливе після оплати рахунку агентства {deal.AgencyPayment?.InvoiceNumber}: очікується підтвердження банківської виписки.";
            return RedirectToAction(nameof(Cabinet));
        }

        deal.SignByParties($"НР-{DateTime.Now:yyyy}-{deal.Id:D4}");
        await _db.SaveChangesAsync();

        TempData["CabinetMessage"] = $"Договір {deal.DraftContractNumber} підписано у нотаріуса (реєстровий №{deal.NotarialRegistryNumber}). Далі юрист зареєструє перехід права власності.";
        return RedirectToAction(nameof(Cabinet));
    }

    /// <summary>
    /// Квитанція та рахунки за угодою (результат UC-06) для покупця, продавця, рієлтора й юриста.
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(int dealId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var deal = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null)
        {
            return RedirectToAction(nameof(Cabinet));
        }

        bool isAuthorized = currentUser.Role == UserRole.Realtor ||
                            currentUser.Role == UserRole.Lawyer ||
                            deal.BuyerId == currentUser.Id ||
                            deal.LandPlot?.SellerId == currentUser.Id;

        if (!isAuthorized)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return View("Receipt", deal);
    }
}
