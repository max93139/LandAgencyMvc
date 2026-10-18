namespace LandAgencyMvc.Models;

public class RealtorRequestsViewModel
{
    public AppUser? CurrentUser { get; set; }

    public List<SelectionRequest> SelectionRequests { get; set; } = new();

    public List<LandPlot> AllPlots { get; set; } = new();

    public int? SelectedRequestId { get; set; }

    public List<LandPlot> MatchedPlotsPreview { get; set; } = new();
}
