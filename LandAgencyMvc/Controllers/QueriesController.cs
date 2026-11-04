using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public class QueriesController : Controller
{
    private readonly ILandAgencyQueries _queries;
    private readonly LandAgencyContext _db;

    public QueriesController(ILandAgencyQueries queries, LandAgencyContext db)
    {
        _queries = queries;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stats = await _queries.GetTableStatsAsync();
        var model = new QueriesDashboardViewModel
        {
            TableStats = stats
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> FilterPlots(PlotFilter filter)
    {
        var result = await _queries.FilterPlotsAsync(filter);
        var model = new PlotsFilterViewModel
        {
            Filter = filter,
            Result = result
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PeriodTurnover(
        PeriodKind kind = PeriodKind.Month,
        DateOnly? date = null,
        DateOnly? from = null,
        DateOnly? to = null,
        TurnoverSort sort = TurnoverSort.Turnover,
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

        var byPurpose = await _queries.TurnoverByPurposeAsync(period, sort, isDescending);
        var byAgent = await _queries.AgentPerformanceAsync(period, AgentSort.Turnover, isDescending);

        var model = new PeriodTurnoverViewModel
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

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LinqShowcase()
    {
        var showcase = await _queries.GetLinqShowcaseAsync();
        var model = new LinqShowcaseViewModel
        {
            Showcase = showcase
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LandBank(LandBankFilter filter)
    {
        var result = await _queries.LandBankAuditAsync(filter);
        var model = new LandBankViewModel
        {
            Filter = filter,
            Result = result
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? searchEmail = null, string? checkEmail = null, string? password = null)
    {
        var allUsers = await _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync();
        AppUser? foundUser = null;
        bool? checkResult = null;

        if (!string.IsNullOrWhiteSpace(searchEmail))
        {
            foundUser = await _queries.FindUserAsync(searchEmail);
        }

        if (!string.IsNullOrWhiteSpace(checkEmail) && !string.IsNullOrWhiteSpace(password))
        {
            var user = await _queries.FindUserAsync(checkEmail);
            if (user != null)
            {
                checkResult = _queries.CheckPassword(user, password);
                if (checkResult == true)
                {
                    TempData["SuccessMessage"] = $"Пароль для користувача {user.FullName} ({user.Email}) успішно підтверджено (PBKDF2).";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Невірний пароль для облікового запису {user.Email}.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = $"Користувача з адресою {checkEmail} не знайдено.";
            }
        }

        var model = new UsersManagementViewModel
        {
            Users = allUsers,
            SearchEmail = searchEmail,
            FoundUser = foundUser,
            CheckPasswordEmail = checkEmail,
            PasswordCheckResult = checkResult
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(AddUserFormModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["ErrorMessage"] = "Заповніть усі обов'язкові поля: ПІБ, Email та пароль.";
            return RedirectToAction(nameof(Users));
        }

        try
        {
            var user = await _queries.AddUserAsync(
                model.FullName, model.Email, model.Phone, model.Role, model.Password, model.BankDetails);
            TempData["SuccessMessage"] = $"Успішно створено користувача {user.FullName} ({user.Email}) з роллю {user.Role}.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(UpdateUserFormModel model)
    {
        try
        {
            await _queries.UpdateUserAsync(model.Id, model.FullName, model.Phone, model.IsActive, model.NewPassword);
            TempData["SuccessMessage"] = $"Дані користувача #{model.Id} ({model.FullName}) успішно оновлено.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
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

        return RedirectToAction(nameof(Users));
    }
}
