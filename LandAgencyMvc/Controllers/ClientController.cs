using System.Security.Claims;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Єдиний клієнтський контролер (модель взаємодії 'Покупець / Продавець' за стандартом платформи OLX).
/// Забезпечує перегляд каталогу ділянок, подачу заявок на огляд, автоматизований підбір,
/// публікацію власних ділянок на продаж з верифікацією КОАТУУ та подання платіжних реквізитів.
/// </summary>
[Authorize]
public partial class ClientController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ICadastralValidationService _cadastralService;
    private readonly IAutoMatchingService _autoMatchingService;
    private readonly IPlotRemovalService _plotRemoval;

    public ClientController(
        LandAgencyContext context,
        IWebHostEnvironment env,
        ICadastralValidationService cadastralService,
        IAutoMatchingService autoMatchingService,
        IPlotRemovalService plotRemoval)
    {
        _db = context;
        _env = env;
        _cadastralService = cadastralService;
        _autoMatchingService = autoMatchingService;
        _plotRemoval = plotRemoval;
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var userId) && userId > 0)
        {
            return await _db.Users.FindAsync(userId);
        }

        return null;
    }
}
