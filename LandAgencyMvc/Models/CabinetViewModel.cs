namespace LandAgencyMvc.Models;

public class CabinetViewModel
{
    public AppUser? CurrentUser { get; set; }

    public List<LandPlot> MyPlots { get; set; } = new();

    public List<SelectionRequest> MySelectionRequests { get; set; } = new();

    public List<LandPlot> MatchedPlotsForUser { get; set; } = new();

    public List<ViewingRequest> MyViewings { get; set; } = new();

    public List<SaleDeal> MyDeals { get; set; } = new();

    public List<SaleDeal> MySellerDeals { get; set; } = new();

    public bool HasActiveDealForPlot(int landPlotId) =>
        MyDeals.Any(d => d.LandPlotId == landPlotId && d.Status != DealStatus.Cancelled);

    public bool CanRequestPurchase(ViewingRequest viewing) =>
        viewing.Status == ViewingStatus.Completed &&
        (viewing.LandPlot == null || viewing.LandPlot.Status == PlotStatus.VerifiedInCatalog) &&
        !HasActiveDealForPlot(viewing.LandPlotId);
}

public class CatalogViewModel
{
    public List<LandPlot> Plots { get; set; } = new();

    public string? FilterPurpose { get; set; }

    public decimal? FilterMaxBudget { get; set; }

    public double? FilterMinArea { get; set; }

    public string? FilterSearch { get; set; }

    public int TotalCount => Plots.Count;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(FilterPurpose) ||
        FilterMaxBudget.HasValue ||
        FilterMinArea.HasValue ||
        !string.IsNullOrWhiteSpace(FilterSearch);
}

public class RealtorDealsViewModel
{
    public AppUser? CurrentUser { get; set; }

    public List<SaleDeal> Deals { get; set; } = new();

    public List<LandPlot> AvailablePlots { get; set; } = new();

    public List<AppUser> Clients { get; set; } = new();

    public int? PreselectedPlotId { get; set; }

    public int? PreselectedBuyerId { get; set; }

    public decimal? PreselectedPrice { get; set; }
}

public class LawyerQueueViewModel
{
    public AppUser? CurrentUser { get; set; }

    public List<LandPlot> Plots { get; set; } = new();

    public int TotalPlotsCount { get; set; }

    public int VerifiedPlotsCount { get; set; }

    public int PendingPlotsCount { get; set; }

    public int RejectedPlotsCount { get; set; }
}

public class LawyerConclusionViewModel
{
    public AppUser? CurrentUser { get; set; }

    public LandPlot Plot { get; set; } = null!;

    public DzkRegistryExtract? DzkExtract { get; set; }

    public string DefaultConclusionNumber { get; set; } = string.Empty;

    public string DefaultConclusionText { get; set; } = string.Empty;

    public string DefaultRiskLevel { get; set; } = "Низький (Чисто)";

    public bool NoArrestsOrMortgages =>
        Plot.LegalConclusion?.NoArrestsOrMortgages ?? !(DzkExtract?.HasDrrpArrestsOrMortgages ?? false);

    public bool NoServitudes =>
        Plot.LegalConclusion?.NoServitudes ?? !(DzkExtract?.HasDzkServitudesOrRestrictions ?? false);

    public bool BoundariesMatchDzk =>
        Plot.LegalConclusion?.BoundariesMatchDzk ?? (DzkExtract?.BoundariesMatchPublicMap ?? true);

    public bool SellerMatchesDrp =>
        Plot.LegalConclusion?.SellerMatchesDrp ?? true;

    public bool NoSanctions =>
        Plot.LegalConclusion?.NoSanctions ?? (DzkExtract?.OwnerAbsentInDebtorsRegistry ?? true);
}
