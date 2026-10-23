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

        var matches = request.MatchPlots(candidatePlots, request.ProcessedByRealtorId, request.RealtorNotes);

        await _db.SaveChangesAsync();
        return matches.Count;
    }

    public async Task<int> MatchNewOrUpdatedPlotAsync(int plotId)
    {
        var plot = await _db.LandPlots.FirstOrDefaultAsync(p => p.Id == plotId);
        if (plot == null)
        {
            return 0;
        }

        var activeRequests = await _db.SelectionRequests
            .Where(r => r.BuyerId != plot.SellerId)
            .ToListAsync();

        int matchedRequestsCount = 0;
        bool anyModified = false;

        foreach (var req in activeRequests)
        {
            bool wasChanged = req.SyncPlotMatch(plot);
            if (wasChanged)
            {
                anyModified = true;
                if (plot.Status == PlotStatus.VerifiedInCatalog && req.GetMatchedPlotIds().Contains(plot.Id))
                {
                    matchedRequestsCount++;
                }
            }
        }

        if (anyModified)
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
}
