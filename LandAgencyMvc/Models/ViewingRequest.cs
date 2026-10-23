using System.ComponentModel.DataAnnotations.Schema;

namespace LandAgencyMvc.Models;

public class ViewingRequest
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    public int BuyerId { get; set; }

    public AppUser? Buyer { get; set; }

    public int? RealtorId { get; set; }

    public AppUser? Realtor { get; set; }

    public DateTime PreferredDate { get; set; }

    public string TimeSlot { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public string ResultNotes { get; set; } = string.Empty;

    public bool BuyerReadyToBuy { get; set; }

    public ViewingStatus Status { get; set; } = ViewingStatus.NewRequest;

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        ViewingStatus.NewRequest => "Нова заявка",
        ViewingStatus.Scheduled => "Огляд призначено ✓",
        ViewingStatus.Completed => "Огляд проведено ✓",
        ViewingStatus.PlotRejectedByBuyer => "Не підійшла (Уточнення критеріїв)",
        ViewingStatus.Cancelled => "Скасовано",
        _ => Status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        ViewingStatus.Completed => "la-badge-green",
        ViewingStatus.Scheduled => "la-badge-green",
        ViewingStatus.Cancelled => "la-badge-red",
        ViewingStatus.PlotRejectedByBuyer => "la-badge-red",
        _ => "la-badge-amber"
    };

    [NotMapped]
    public bool CanCreateDeal => Status == ViewingStatus.Completed &&
                                 (LandPlot == null || LandPlot.Status == PlotStatus.VerifiedInCatalog);

    [NotMapped]
    public bool CanCancelByClient => Status is ViewingStatus.NewRequest or ViewingStatus.Scheduled;

    public void ConfirmViewing(int? realtorId)
    {
        RealtorId = realtorId;
        Status = ViewingStatus.Scheduled;
    }

    public void CancelViewing(string? reason = null)
    {
        Status = ViewingStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            ResultNotes = reason.Trim();
        }
    }

    public void CompleteViewing(bool fitsBuyer, string? notes = null)
    {
        BuyerReadyToBuy = fitsBuyer;
        Status = fitsBuyer ? ViewingStatus.Completed : ViewingStatus.PlotRejectedByBuyer;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ResultNotes = notes.Trim();
        }
    }
}
