using System.ComponentModel.DataAnnotations.Schema;

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

    [NotMapped]
    public double MinAreaSotky => Math.Round(MinAreaHa * 100.0, 2);

    [NotMapped]
    public string PurposeCode
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Purpose))
            {
                return string.Empty;
            }

            return Purpose.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
        }
    }

    public List<LandPlot> MatchPlots(IReadOnlyList<LandPlot> verifiedPlots, int? realtorId = null, string? notes = null)
    {
        var candidatePlots = verifiedPlots
            .Where(p => BuyerId == 0 || p.SellerId != BuyerId)
            .ToList();

        string purposeCode = PurposeCode;

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

        List<LandPlot> finalMatches;
        if (strictMatches.Any())
        {
            finalMatches = strictMatches;
        }
        else
        {
            var flexibleMatches = candidatePlots.Where(p =>
                (MaxBudgetUsd <= 0 || p.PriceUsd <= MaxBudgetUsd * 1.25m) &&
                (MinAreaHa <= 0 || p.AreaHectares >= MinAreaHa * 0.80) &&
                (string.IsNullOrWhiteSpace(purposeCode) ||
                 p.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            finalMatches = flexibleMatches.Any()
                ? flexibleMatches
                : candidatePlots
                    .Where(p => MaxBudgetUsd <= 0 || p.PriceUsd <= MaxBudgetUsd * 1.25m)
                    .Take(3)
                    .ToList();
        }

        MatchedPlotIds = string.Join(",", finalMatches.Select(p => p.Id));
        MatchedPlotsCount = finalMatches.Count;
        IsProcessedByRealtor = true;
        if (realtorId.HasValue)
        {
            ProcessedByRealtorId = realtorId;
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            RealtorNotes = notes.Trim();
        }
        else if (string.IsNullOrWhiteSpace(RealtorNotes))
        {
            RealtorNotes = $"Автоматично підібрано системою (знайдено: {finalMatches.Count} перевірених об'єктів).";
        }

        return finalMatches;
    }

    public bool IsPlotMatching(LandPlot plot)
    {
        if (BuyerId != 0 && plot.SellerId == BuyerId)
        {
            return false;
        }

        string purposeCode = PurposeCode;
        bool budgetMatch = MaxBudgetUsd <= 0 || plot.PriceUsd <= MaxBudgetUsd * 1.20m;
        bool areaMatch = MinAreaHa <= 0 || plot.AreaHectares >= MinAreaHa * 0.80;
        bool purposeMatch = string.IsNullOrWhiteSpace(purposeCode) ||
                            plot.Purpose.Contains(purposeCode, StringComparison.OrdinalIgnoreCase) ||
                            plot.Purpose.Contains(Purpose.Trim(), StringComparison.OrdinalIgnoreCase);
        bool locationMatch = string.IsNullOrWhiteSpace(DesiredDistrict) ||
                             plot.Location.Contains(DesiredDistrict, StringComparison.OrdinalIgnoreCase) ||
                             plot.Title.Contains(DesiredDistrict, StringComparison.OrdinalIgnoreCase);

        return budgetMatch && areaMatch && purposeMatch && locationMatch;
    }
}
