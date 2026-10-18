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
}
