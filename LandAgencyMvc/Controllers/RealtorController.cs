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
public partial class RealtorController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly IAutoMatchingService _autoMatchingService;
    private readonly ILandAgencyQueries _queries;
    private readonly ICadastralValidationService _cadastralService;
    private readonly IPlotMediaStorage _mediaStorage;
    private readonly IPlotRemovalService _plotRemoval;

    public RealtorController(
        LandAgencyContext context,
        IAutoMatchingService autoMatchingService,
        ILandAgencyQueries queries,
        ICadastralValidationService cadastralService,
        IPlotMediaStorage mediaStorage,
        IPlotRemovalService plotRemoval)
    {
        _db = context;
        _autoMatchingService = autoMatchingService;
        _queries = queries;
        _cadastralService = cadastralService;
        _mediaStorage = mediaStorage;
        _plotRemoval = plotRemoval;
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
}
