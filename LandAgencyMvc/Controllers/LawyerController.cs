using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Робоче місце штатного юриста агентства.
/// Відповідає за правовий аудит земельних ділянок (ДЗК/ДРРП), формування висновків
/// та юридичну експертизу проектів договорів купівлі-продажу для нотаріуса.
/// </summary>
public class LawyerController : Controller
{
    private readonly LandAgencyContext _db;

    public LawyerController(LandAgencyContext context)
    {
        _db = context;
    }

    private async Task<AppUser?> SetCurrentUserAsync(int userId)
    {
        int resolvedId = userId;
        if (resolvedId <= 0 &&
            Request.Cookies.TryGetValue("LandAgencyUserId", out var cookieVal) &&
            int.TryParse(cookieVal, out var parsedCookieId))
        {
            resolvedId = parsedCookieId;
        }

        AppUser? user = null;
        if (resolvedId > 0)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == resolvedId && u.Role == UserRole.Lawyer);
        }

        user ??= await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Lawyer);

        ViewBag.CurrentUser = user;
        ViewBag.CurrentUserId = user?.Id ?? 0;
        return user;
    }

    /// <summary>
    /// Черга земельних ділянок для юридичного аудиту: очікують перевірки, верифіковані або відхилені.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Queue(int userId)

    {
        await SetCurrentUserAsync(userId);
        var plots = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        ViewBag.TotalPlotsCount = plots.Count;
        ViewBag.VerifiedPlotsCount = plots.Count(p => p.Status == PlotStatus.VerifiedInCatalog || p.Status == PlotStatus.Sold);
        ViewBag.PendingPlotsCount = plots.Count(p => p.Status == PlotStatus.PendingLegalCheck);
        ViewBag.RejectedPlotsCount = plots.Count(p => p.Status == PlotStatus.RejectedByLawyer);

        return View(plots);
    }

    /// <summary>
    /// Детальний перегляд ділянки з автоматичним запитом до ДЗК/ДРРП для складання висновку.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conclusion(int id, int userId)
    {
        await SetCurrentUserAsync(userId);
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
            return RedirectToAction(nameof(Queue), new { userId });
        }

        var dzkExtract = DzkRegistrySimulator.QueryRegistries(plot);
        ViewBag.DzkExtract = dzkExtract;
        ViewBag.DefaultConclusionNumber = plot.LegalConclusion?.ConclusionNumber ?? $"ЮВ-{DateTime.Now:yyyy}-{plot.Id:D3}";
        ViewBag.DefaultConclusionText = plot.LegalConclusion?.ConclusionText ?? dzkExtract.SummaryReport;
        ViewBag.DefaultRiskLevel = plot.LegalConclusion?.RiskLevel ?? dzkExtract.RecommendedRiskLevel;
        return View(plot);
    }

    /// <summary>
    /// Фіксує юридичний висновок адвоката: погоджує ділянку для публікації або відхиляє з актом ризиків.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitLegalConclusion(
        int userId,
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
        var currentUser = await SetCurrentUserAsync(userId);
        var plot = await _db.LandPlots
            .Include(p => p.Seller)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == landPlotId);

        if (plot != null)
        {
            var dzkExtract = DzkRegistrySimulator.QueryRegistries(plot);
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
                : (approve ? "Низький (Чисто)" : "Високий (Виявлено арешт / обтяження)");

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

            await _db.SaveChangesAsync();
            TempData["LawyerMessage"] = approve
                ? $"Юридичний висновок {resolvedConclusionNum} затверджено. Ділянку опубліковано в каталозі."
                : $"Ділянку {plot.CadastralNumber} відхилено за результатами перевірки ДЗК/ДРРП. Сформовано акт ризиків.";
        }

        return RedirectToAction(nameof(Queue), new { userId = currentUser?.Id ?? userId });
    }

    /// <summary>
    /// Реєстр договорів купівлі-продажу, що надійшли від рієлторів на правову експертизу перед нотаріусом.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Contracts(int userId)
    {
        await SetCurrentUserAsync(userId);
        var deals = await _db.SaleDeals
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.Seller)
            .Include(d => d.LandPlot)
            .ThenInclude(p => p!.LegalConclusion)
            .Include(d => d.Buyer)
            .Include(d => d.Realtor)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
        return View(deals);
    }

    /// <summary>
    /// Правова віза юриста: погоджує передачу договору нотаріусу або повертає рієлтору на доопрацювання.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ApproveDealContract(int userId, int dealId, string? lawyerNotes, string decision = "approve")

    {
        var currentUser = await SetCurrentUserAsync(userId);
        var deal = await _db.SaleDeals.FindAsync(dealId);
        if (deal != null)
        {
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

        return RedirectToAction(nameof(Contracts), new { userId = currentUser?.Id ?? userId });
    }
}
