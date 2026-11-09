using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Маршрутизатор сумісності для попередніх посилань.
/// Перенаправляє запити безпосередньо у бізнес-модулі програми:
/// - Каталог ділянок (Client/Catalog)
/// - Фінансова аналітика (Realtor/Analytics)
/// - База клієнтів та персонал (Realtor/Clients)
/// - Реєстр земельного банку (Lawyer/LandBank)
/// </summary>
[Authorize]
public class QueriesController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole(nameof(UserRole.Lawyer)))
        {
            return RedirectToAction("LandBank", "Lawyer");
        }

        return RedirectToAction("Analytics", "Realtor");
    }

    [HttpGet]
    public IActionResult FilterPlots() => RedirectToAction("Catalog", "Client");

    [HttpGet]
    public IActionResult PeriodTurnover(
        Services.PeriodKind kind = Services.PeriodKind.Month,
        DateOnly? date = null,
        DateOnly? from = null,
        DateOnly? to = null,
        Services.TurnoverSort sort = Services.TurnoverSort.Turnover,
        bool? desc = null)
    {
        return RedirectToAction("Analytics", "Realtor", new { kind, date, from, to, sort, desc });
    }

    [HttpGet]
    public IActionResult LandBank(Services.LandBankFilter filter)
    {
        return RedirectToAction("LandBank", "Lawyer", filter);
    }

    [HttpGet]
    public IActionResult Users(string? searchEmail = null)
    {
        return RedirectToAction("Clients", "Realtor", new { searchEmail });
    }

    [HttpGet]
    public IActionResult LinqShowcase()
    {
        return RedirectToAction("Analytics", "Realtor");
    }
}
