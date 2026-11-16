using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandAgencyMvc.Controllers;

public partial class RealtorController
{
    /// <summary>
    /// Особиста фінансова аналітика та результативність поточного рієлтора за обраний період (день, місяць, півріччя або довільний).
    /// </summary>
    [Authorize(Roles = AppRoles.Realtor)]
    [HttpGet]
    public async Task<IActionResult> Analytics(
        PeriodKind kind = PeriodKind.Month,
        DateOnly? date = null,
        DateOnly? from = null,
        DateOnly? to = null,
        TurnoverSort sort = TurnoverSort.Turnover,
        bool? desc = null)
    {
        var currentUser = await GetCurrentRealtorAsync();
        bool isDescending = desc ?? true;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = date ?? today;
        var periodFrom = from ?? today.AddMonths(-3);
        var periodTo = to ?? today;

        var defaultMonthPeriod = Period.Containing(kind, day);
        bool customRangeChanged = from.HasValue && to.HasValue &&
            (from.Value != DateOnly.FromDateTime(defaultMonthPeriod.From) ||
             to.Value != DateOnly.FromDateTime(defaultMonthPeriod.To).AddDays(-1));

        if (customRangeChanged && kind != PeriodKind.Custom)
        {
            kind = PeriodKind.Custom;
        }

        var period = kind == PeriodKind.Custom
            ? Period.Between(periodFrom, periodTo)
            : defaultMonthPeriod;

        var byPurpose = await _queries.TurnoverByPurposeAsync(period, sort, isDescending, currentUser?.Id);
        var closedDeals = await _queries.RealtorClosedDealsAsync(period, currentUser?.Id, isDescending);

        var viewModel = new PeriodTurnoverViewModel
        {
            CurrentUser = currentUser,
            Kind = kind,
            Date = day,
            From = DateOnly.FromDateTime(period.From),
            To = DateOnly.FromDateTime(period.To).AddDays(-1),
            Sort = sort,
            Descending = isDescending,
            Period = period,
            ByPurpose = byPurpose,
            ClosedDeals = closedDeals
        };

        ViewData["ActiveNav"] = "Analytics";
        return View(viewModel);
    }
}
