namespace LandAgencyMvc.Models;

public class SelectionRequest
{
    public int Id { get; set; }

    public int BuyerId { get; set; }

    public AppUser? Buyer { get; set; }

    public string DesiredDistrict { get; set; } = string.Empty;

    public double MinAreaHa { get; set; }

    public decimal MaxBudgetUsd { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public bool IsProcessedByRealtor { get; set; }

    public int? ProcessedByRealtorId { get; set; }

    public AppUser? ProcessedByRealtor { get; set; }

    public string MatchedPlotIds { get; set; } = string.Empty;

    public int MatchedPlotsCount { get; set; }

    public string RealtorNotes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<LandPlot> MatchPlots(IReadOnlyList<LandPlot> verifiedPlots, int? realtorId, string? notes)
    {
        var candidatePlots = verifiedPlots
            .Where(p => p.SellerId != BuyerId)
            .ToList();

        if (!candidatePlots.Any())
        {
            candidatePlots = verifiedPlots.ToList();
        }

        string purposeCode = string.Empty;
        if (!string.IsNullOrWhiteSpace(Purpose))
        {
            var firstToken = Purpose.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            purposeCode = firstToken.Trim();
        }

        var strictMatches = candidatePlots.Where(p =>
            (MaxBudgetUsd <= 0 || p.PriceUsd <= MaxBudgetUsd) &&
            (MinAreaHa <= 0 || p.AreaHectares >= MinAreaHa) &&
            (string.IsNullOrWhiteSpace(purposeCode) ||
             p.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase) ||
             p.Purpose.Contains(Purpose.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(DesiredDistrict) ||
             p.Location.Contains(DesiredDistrict, StringComparison.OrdinalIgnoreCase) ||
             p.Title.Contains(DesiredDistrict, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var finalMatches = strictMatches.Any()
            ? strictMatches
            : candidatePlots.Where(p =>
                (MaxBudgetUsd <= 0 || p.PriceUsd <= MaxBudgetUsd * 1.25m) &&
                (string.IsNullOrWhiteSpace(purposeCode) ||
                 p.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase))).ToList();

        if (!finalMatches.Any() && candidatePlots.Any())
        {
            finalMatches = candidatePlots
                .Where(p => MaxBudgetUsd <= 0 || p.PriceUsd <= MaxBudgetUsd * 1.25m)
                .Take(3)
                .ToList();
        }

        MatchedPlotIds = string.Join(",", finalMatches.Select(p => p.Id));
        MatchedPlotsCount = finalMatches.Count;
        IsProcessedByRealtor = true;
        ProcessedByRealtorId = realtorId;
        RealtorNotes = !string.IsNullOrWhiteSpace(notes)
            ? notes.Trim()
            : $"Підібрано ділянок у каталозі: {finalMatches.Count}";

        return finalMatches;
    }
}
