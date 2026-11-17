using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class ClientController
{
    /// <summary>
    /// Окрема сторінка налаштувань клієнта: ПІБ, телефон, РНОКПП та IBAN для угод, зміна пароля.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        return View(new CabinetViewModel { CurrentUser = currentUser });
    }

    /// <summary>
    /// Зберігає налаштування профілю клієнта; пароль змінюється лише за правильного поточного пароля.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string? fullName, string? phone, string currentPassword, string newPassword, string? bankDetails, string? taxId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser != null)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                currentUser.FullName = fullName.Trim();
            }

            if (phone != null)
            {
                currentUser.Phone = phone.Trim();
            }

            if (!string.IsNullOrWhiteSpace(bankDetails))
            {
                var ibanClean = bankDetails.Trim().ToUpperInvariant();
                if (!System.Text.RegularExpressions.Regex.IsMatch(ibanClean, @"^UA\d{27}$"))
                {
                    TempData["CabinetError"] = "Невірний формат IBAN. Банківський рахунок України має містити літери UA та 27 цифр (29 символів).";
                    return RedirectToAction(nameof(Settings));
                }
                currentUser.BankDetails = ibanClean;
            }

            if (!string.IsNullOrWhiteSpace(taxId))
            {
                currentUser.TaxId = taxId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (!currentUser.ChangePassword(currentPassword, newPassword))
                {
                    TempData["CabinetError"] = "Невірний поточний пароль. Пароль не змінено.";
                    return RedirectToAction(nameof(Settings));
                }
            }

            await _db.SaveChangesAsync();
            TempData["CabinetMessage"] = "Налаштування профілю та реквізити успішно оновлено.";
        }

        return RedirectToAction(nameof(Settings));
    }

    /// <summary>
    /// Особистий кабінет клієнта. Продавець бачить власні ділянки, їхні документи та угоди з продажу;
    /// покупець бачить запити на підбір, заявки на огляд, угоди купівлі та рахунки на оплату.
    /// </summary>
    [Authorize(Roles = AppRoles.AnyClient)]
    [HttpGet("/Cabinet")]
    [HttpGet("/Client/Cabinet")]
    public async Task<IActionResult> Cabinet()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var viewModel = new CabinetViewModel { CurrentUser = currentUser };

        if (currentUser.Role == UserRole.Seller)
        {
            await LoadSellerCabinetAsync(viewModel);
        }
        else
        {
            await LoadBuyerCabinetAsync(viewModel);
        }

        return View(viewModel);
    }

    private async Task LoadSellerCabinetAsync(CabinetViewModel viewModel)
    {
        int sellerId = viewModel.CurrentUser!.Id;

        viewModel.MyPlots = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .Where(p => p.SellerId == sellerId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        viewModel.MySellerDeals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .Where(d => d.LandPlot != null && d.LandPlot.SellerId == sellerId)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    private async Task LoadBuyerCabinetAsync(CabinetViewModel viewModel)
    {
        int buyerId = viewModel.CurrentUser!.Id;

        viewModel.MySelectionRequests = await _db.SelectionRequests
            .Where(r => r.BuyerId == buyerId)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        var matchedIds = viewModel.MySelectionRequests
            .SelectMany(r => r.GetMatchedPlotIds())
            .Distinct()
            .ToList();

        if (matchedIds.Any())
        {
            viewModel.MatchedPlotsForUser = await _db.LandPlots
                .Where(p => matchedIds.Contains(p.Id) && p.Status == PlotStatus.VerifiedInCatalog)
                .ToListAsync();
        }

        viewModel.MyViewings = await _db.ViewingRequests
            .Include(v => v.LandPlot)
            .Include(v => v.Realtor)
            .Where(v => v.BuyerId == buyerId)
            .OrderByDescending(v => v.Id)
            .ToListAsync();

        viewModel.MyDeals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.Buyer)
            .Include(d => d.Payments)
            .Where(d => d.BuyerId == buyerId)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }
}
