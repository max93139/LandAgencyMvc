using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// Реєстр клієнтів агентства (CRM).
    /// Забезпечує пошук за email, додавання, редагування та безпечне вилучення клієнтів.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Clients(string? searchEmail = null)
    {
        var clientsQuery = _db.Users
            .Where(u => AppRoles.ClientRoles.Contains(u.Role))
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
                ?? await _db.Users.FirstOrDefaultAsync(u => AppRoles.ClientRoles.Contains(u.Role) && u.Email == searchEmail.Trim());

            if (foundUser == null)
            {
                TempData["ErrorMessage"] = $"Клієнта з адресою '{searchEmail}' не знайдено.";
            }
        }

        var clientIds = clients.Select(u => u.Id).ToList();
        var plotCounts = await _db.LandPlots
            .Where(p => clientIds.Contains(p.SellerId))
            .GroupBy(p => p.SellerId)
            .Select(g => new { SellerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SellerId, x => x.Count);

        var model = new UsersManagementViewModel
        {
            Users = clients,
            PlotCounts = plotCounts,
            SearchEmail = searchEmail,
            FoundUser = foundUser
        };

        ViewData["ActiveNav"] = "Clients";
        return View(model);
    }

    /// <summary>
    /// Додавання нового клієнта з початковою заявкою в одній транзакції.
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddClient(AddUserFormModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["ErrorMessage"] = "Заповніть усі обов'язкові поля: ПІБ, Email та пароль.";
            return RedirectToAction(nameof(Clients));
        }

        if (!AppRoles.ClientRoles.Contains(model.Role))
        {
            TempData["ErrorMessage"] = "Через CRM можна створити лише клієнта: Покупця або Продавця.";
            return RedirectToAction(nameof(Clients));
        }

        try
        {
            var user = await _queries.AddUserAsync(
                model.FullName, model.Email, model.Phone, model.Role, model.Password, model.BankDetails);
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
    [Authorize(Roles = AppRoles.Realtor)]
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
    [Authorize(Roles = AppRoles.Realtor)]
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
