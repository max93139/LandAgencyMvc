using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

/// <summary>
/// Інтелектуальний двигун автоматичного підбору ділянок на основі критеріїв:
/// цільове призначення (КВЦПЗ), бюджетний ліміт, мінімальна площа та локація.
/// </summary>
public class AutoMatchingService : IAutoMatchingService
{
    private readonly LandAgencyContext _db;

    public AutoMatchingService(LandAgencyContext db)
    {
        _db = db;
    }

    public async Task<int> MatchRequestAsync(int requestId)
    {
        var request = await _db.SelectionRequests
            .Include(r => r.Buyer)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return 0;
        }

        var candidatePlots = await _db.LandPlots
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog && p.SellerId != request.BuyerId)
            .ToListAsync();

        var matches = FindMatchingPlots(request, candidatePlots);

        request.MatchedPlotIds = string.Join(",", matches.Select(p => p.Id));
        request.MatchedPlotsCount = matches.Count;
        request.IsProcessedByRealtor = true;
        request.RealtorNotes = string.IsNullOrWhiteSpace(request.RealtorNotes)
            ? $"Автоматично підібрано системою (знайдено: {matches.Count} перевірених об'єктів)."
            : request.RealtorNotes;

        await _db.SaveChangesAsync();
        return matches.Count;
    }

    public async Task<int> MatchNewOrUpdatedPlotAsync(int plotId)
    {
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId);
        if (plot == null || plot.Status != PlotStatus.VerifiedInCatalog)
        {
            return 0;
        }

        var activeRequests = await _db.SelectionRequests
            .Where(r => r.BuyerId != plot.SellerId)
            .ToListAsync();

        int matchedRequestsCount = 0;

        foreach (var req in activeRequests)
        {
            if (IsPlotMatchingRequest(plot, req))
            {
                var currentIds = (req.MatchedPlotIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToHashSet();

                if (currentIds.Add(plot.Id))
                {
                    req.MatchedPlotIds = string.Join(",", currentIds);
                    req.MatchedPlotsCount = currentIds.Count;
                    req.IsProcessedByRealtor = true;
                    req.RealtorNotes = $"Автоматично оновлено: додано новий схвалений юристом об'єкт {plot.CadastralNumber}.";
                    matchedRequestsCount++;
                }
            }
        }

        if (matchedRequestsCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        return matchedRequestsCount;
    }

    public async Task<int> MatchAllActiveRequestsAsync()
    {
        var activeRequests = await _db.SelectionRequests
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        int totalMatches = 0;
        foreach (var req in activeRequests)
        {
            totalMatches += await MatchRequestAsync(req.Id);
        }

        return totalMatches;
    }

    private static List<LandPlot> FindMatchingPlots(SelectionRequest request, List<LandPlot> plots)
    {
        string purposeCode = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.Purpose))
        {
            var firstToken = request.Purpose.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            purposeCode = firstToken.Trim();
        }

        var strictMatches = plots.Where(p =>
            (request.MaxBudgetUsd <= 0 || p.PriceUsd <= request.MaxBudgetUsd) &&
            (request.MinAreaHa <= 0 || p.AreaHectares >= request.MinAreaHa) &&
            (string.IsNullOrWhiteSpace(purposeCode) ||
             p.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase) ||
             p.Purpose.Contains(request.Purpose.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(request.DesiredDistrict) ||
             p.Location.Contains(request.DesiredDistrict, StringComparison.OrdinalIgnoreCase) ||
             p.Title.Contains(request.DesiredDistrict, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (strictMatches.Any())
        {
            return strictMatches;
        }

        var flexibleMatches = plots.Where(p =>
            (request.MaxBudgetUsd <= 0 || p.PriceUsd <= request.MaxBudgetUsd * 1.25m) &&
            (request.MinAreaHa <= 0 || p.AreaHectares >= request.MinAreaHa * 0.80) &&
            (string.IsNullOrWhiteSpace(purposeCode) ||
             p.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (flexibleMatches.Any())
        {
            return flexibleMatches;
        }

        return plots
            .Where(p => request.MaxBudgetUsd <= 0 || p.PriceUsd <= request.MaxBudgetUsd * 1.25m)
            .Take(3)
            .ToList();
    }

    private static bool IsPlotMatchingRequest(LandPlot plot, SelectionRequest request)
    {
        string purposeCode = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.Purpose))
        {
            var firstToken = request.Purpose.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            purposeCode = firstToken.Trim();
        }

        bool budgetMatch = request.MaxBudgetUsd <= 0 || plot.PriceUsd <= request.MaxBudgetUsd * 1.20m;
        bool areaMatch = request.MinAreaHa <= 0 || plot.AreaHectares >= request.MinAreaHa * 0.80;
        bool purposeMatch = string.IsNullOrWhiteSpace(purposeCode) ||
                            plot.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase) ||
                            plot.Purpose.Contains(request.Purpose.Trim(), StringComparison.OrdinalIgnoreCase);
        bool locationMatch = string.IsNullOrWhiteSpace(request.DesiredDistrict) ||
                             plot.Location.Contains(request.DesiredDistrict, StringComparison.OrdinalIgnoreCase) ||
                             plot.Title.Contains(request.DesiredDistrict, StringComparison.OrdinalIgnoreCase);

        return budgetMatch && areaMatch && purposeMatch && locationMatch;
    }
}
