using System.Security.Claims;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Робоче місце штатного юриста агентства.
/// Відповідає за правовий аудит земельних ділянок (КОАТУУ, ДЗК/ДРРП, цінові аномалії),
/// формування юридичних висновків та правову експертизу проектів договорів для нотаріуса.
/// </summary>
[Authorize(Roles = nameof(UserRole.Lawyer))]
public class LawyerController : Controller
{
    private readonly LandAgencyContext _db;
    private readonly ICadastralValidationService _cadastralService;
    private readonly IAutoMatchingService _autoMatchingService;

    public LawyerController(
        LandAgencyContext context,
        ICadastralValidationService cadastralService,
        IAutoMatchingService autoMatchingService)
    {
        _db = context;
        _cadastralService = cadastralService;
        _autoMatchingService = autoMatchingService;
    }

    private async Task<AppUser?> GetCurrentLawyerAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var lawyerId) && lawyerId > 0)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == lawyerId && u.Role == UserRole.Lawyer);
        }

        return null;
    }

    /// <summary>
    /// Черга земельних ділянок для юридичного аудиту: очікують перевірки, верифіковані або відхилені.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Queue()
    {
        var currentUser = await GetCurrentLawyerAsync();
        var plots = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        var viewModel = new LawyerQueueViewModel
        {
            CurrentUser = currentUser,
            Plots = plots,
            TotalPlotsCount = plots.Count,
            VerifiedPlotsCount = plots.Count(p => p.Status == PlotStatus.VerifiedInCatalog || p.Status == PlotStatus.Sold),
            PendingPlotsCount = plots.Count(p => p.Status == PlotStatus.PendingLegalCheck),
            RejectedPlotsCount = plots.Count(p => p.Status == PlotStatus.RejectedByLawyer)
        };

        return View(viewModel);
    }

    /// <summary>
    /// Детальний перегляд ділянки з автоматичним запитом до ДЗК/ДРРП та аналізом ринкової ціни за сотку.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conclusion(int id)
    {
        var currentUser = await GetCurrentLawyerAsync();
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plot == null)
        {
            plot = await _db.LandPlots
                .Include(p => p.Seller)
                .Include(p => p.Documents)
                .Include(p => p.LegalConclusion)
                .OrderByDescending(p => p.Status == PlotStatus.PendingLegalCheck)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();
        }

        if (plot == null)
        {
            return RedirectToAction(nameof(Queue));
        }

        var dzkExtract = await DzkRegistrySimulator.QueryRegistriesAsync(plot, _cadastralService);
        var viewModel = new LawyerConclusionViewModel
        {
            CurrentUser = currentUser,
            Plot = plot,
            DzkExtract = dzkExtract,
            DefaultConclusionNumber = plot.LegalConclusion?.ConclusionNumber ?? $"ЮВ-{DateTime.Now:yyyy}-{plot.Id:D3}",
            DefaultConclusionText = plot.LegalConclusion?.ConclusionText ?? dzkExtract.SummaryReport,
            DefaultRiskLevel = plot.LegalConclusion?.RiskLevel ?? dzkExtract.RecommendedRiskLevel
        };

        return View(viewModel);
    }

    /// <summary>
    /// Фіксує юридичний висновок адвоката: погоджує ділянку для публікації або відхиляє з актом ризиків.
    /// При успішному схваленні автоматично запускає автопідбір під активні запити клієнтів.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLegalConclusion(
        int landPlotId,
        string? conclusionNumber,
        string? riskLevel,
        bool noArrests,
        bool noServitudes,
        bool boundariesMatch,
        bool sellerMatchesDrp,
        bool noSanctions,
        string conclusionText,
        bool approve)
    {
        var currentUser = await GetCurrentLawyerAsync();
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == landPlotId);

        if (plot != null)
        {
            if (plot.Status == PlotStatus.Sold)
            {
                TempData["LawyerError"] = $"Ділянку {plot.CadastralNumber} вже продано за нотаріальною угодою. Повторна зміна статусу юридичного аудиту неможлива.";
                return RedirectToAction(nameof(Queue));
            }

            var dzkExtract = await DzkRegistrySimulator.QueryRegistriesAsync(plot, _cadastralService);
            plot.ChangeStatus(approve ? PlotStatus.VerifiedInCatalog : PlotStatus.RejectedByLawyer);

            foreach (var doc in plot.Documents)
            {
                doc.IsVerified = approve;
            }

            string resolvedConclusionNum = !string.IsNullOrWhiteSpace(conclusionNumber)
                ? conclusionNumber.Trim()
                : $"ЮВ-{DateTime.Now:yyyy}-{landPlotId:D3}";

            string resolvedRisk = !string.IsNullOrWhiteSpace(riskLevel)
                ? riskLevel.Trim()
                : (approve ? "Низький (Чисто)" : "Високий (Виявлено обтяження / аномалію)");

            var existing = await _db.LegalConclusions.FirstOrDefaultAsync(c => c.LandPlotId == landPlotId);
            if (existing == null)
            {
                var conclusion = new LegalConclusion
                {
                    ConclusionNumber = resolvedConclusionNum,
                    LandPlotId = landPlotId,
                    LawyerId = currentUser?.Id,
                    NoArrestsOrMortgages = noArrests,
                    NoServitudes = noServitudes,
                    BoundariesMatchDzk = boundariesMatch,
                    SellerMatchesDrp = sellerMatchesDrp,
                    NoSanctions = noSanctions,
                    RiskLevel = resolvedRisk,
                    DzkExtractNumber = dzkExtract.ExtractNumber,
                    CreatedAt = DateTime.Now
                };

                if (approve)
                {
                    conclusion.Approve((conclusionText ?? string.Empty).Trim(), resolvedRisk);
                }
                else
                {
                    conclusion.Reject((conclusionText ?? string.Empty).Trim(), resolvedRisk);
                }

                _db.LegalConclusions.Add(conclusion);
            }
            else
            {
                existing.ConclusionNumber = resolvedConclusionNum;
                existing.LawyerId = currentUser?.Id;
                existing.NoArrestsOrMortgages = noArrests;
                existing.NoServitudes = noServitudes;
                existing.BoundariesMatchDzk = boundariesMatch;
                existing.SellerMatchesDrp = sellerMatchesDrp;
                existing.NoSanctions = noSanctions;
                existing.RiskLevel = resolvedRisk;
                existing.DzkExtractNumber = dzkExtract.ExtractNumber;
                existing.CreatedAt = DateTime.Now;

                if (approve)
                {
                    existing.Approve((conclusionText ?? string.Empty).Trim(), resolvedRisk);
                }
                else
                {
                    existing.Reject((conclusionText ?? string.Empty).Trim(), resolvedRisk);
                }
            }

            if (!approve)
            {
                var pendingViewings = await _db.ViewingRequests
                    .Where(v => v.LandPlotId == plot.Id &&
                                v.Status != ViewingStatus.Completed &&
                                v.Status != ViewingStatus.Cancelled &&
                                v.Status != ViewingStatus.PlotRejectedByBuyer)
                    .ToListAsync();
                foreach (var v in pendingViewings)
                {
                    v.CancelViewing("Ділянку знято з публікації за результатами повторного юридичного аудиту.");
                }
            }

            await _db.SaveChangesAsync();

            int autoMatchedCount = await _autoMatchingService.MatchNewOrUpdatedPlotAsync(plot.Id);

            if (approve)
            {
                TempData["LawyerMessage"] = $"Юридичний висновок {resolvedConclusionNum} затверджено. Ділянку опубліковано в каталозі. Автоматично підібрано під {autoMatchedCount} активних заявок клієнтів.";
            }
            else
            {
                TempData["LawyerMessage"] = $"Ділянку {plot.CadastralNumber} відхилено за результатами юридичного аудиту. Сформовано акт ризиків.";
            }
        }

        return RedirectToAction(nameof(Queue));
    }

    /// <summary>
    /// Реєстр договорів купівлі-продажу, що надійшли від рієлторів на правову експертизу перед нотаріусом.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Contracts()
    {
        var deals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.LegalConclusion)
            .Include(d => d.Buyer)
            .Include(d => d.Realtor)
            .Include(d => d.Payments)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
        return View(deals);
    }

    /// <summary>
    /// Правова віза юриста: погоджує передачу договору нотаріусу або повертає рієлтору на доопрацювання.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDealContract(int dealId, string? lawyerNotes, string decision = "approve")
    {
        var currentUser = await GetCurrentLawyerAsync();
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
            if (deal.Status == DealStatus.SignedAtNotary ||
                deal.Status == DealStatus.CompletedAtNotary ||
                deal.Status == DealStatus.Cancelled)
            {
                TempData["LawyerError"] = $"Угода {deal.DraftContractNumber} перебуває у статусі «{deal.StatusDisplayName}» і не підлягає зміні правової візи.";
                return RedirectToAction(nameof(Contracts));
            }

            deal.LawyerId = currentUser?.Id;
            if (decision == "return")
            {
                deal.Status = DealStatus.ReturnedForRevision;
                deal.LawyerApprovalNotes = !string.IsNullOrWhiteSpace(lawyerNotes)
                    ? lawyerNotes.Trim()
                    : "Повернуто юристом на доопрацювання умов договору.";
                TempData["LawyerMessage"] = $"Проєкт договору {deal.DraftContractNumber} повернуто рієлтору на доопрацювання.";
            }
            else
            {
                deal.Status = DealStatus.ApprovedByLawyerForNotary;
                deal.LawyerApprovalNotes = !string.IsNullOrWhiteSpace(lawyerNotes)
                    ? lawyerNotes.Trim()
                    : "Текст договору відповідає вимогам Земельного кодексу України. Дозволено передачу пакету документів нотаріусу.";
                TempData["LawyerMessage"] = $"Проєкт договору {deal.DraftContractNumber} погоджено для передачі нотаріусу.";
            }

            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Contracts));
    }
}
