using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace LandAgencyMvc.Models;

public class LandPlot
{
    public int Id { get; set; }

    [Required]
    public string CadastralNumber { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;

    public double AreaHectares { get; set; }

    [Required]
    public string Purpose { get; set; } = string.Empty;

    public decimal PriceUsd { get; set; }

    public string Description { get; set; } = string.Empty;

    public string OwnershipDocNumber { get; set; } = string.Empty;

    public string SellerBankIban { get; set; } = string.Empty;

    public PlotStatus Status { get; set; } = PlotStatus.PendingLegalCheck;

    public int SellerId { get; set; }

    public AppUser? Seller { get; set; }

    public int? AssignedRealtorId { get; set; }

    public AppUser? AssignedRealtor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public LegalConclusion? LegalConclusion { get; set; }

    public ICollection<PlotDocument> Documents { get; set; } = new List<PlotDocument>();

    public ICollection<ViewingRequest> ViewingRequests { get; set; } = new List<ViewingRequest>();

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        PlotStatus.PendingLegalCheck => "На перевірці у юриста (ДЗК)",
        PlotStatus.VerifiedInCatalog => "Опубліковано ✓",
        PlotStatus.RejectedByLawyer => "Відхилено (Акт ризиків)",
        PlotStatus.Sold => "Продано (Угоду завершено)",
        _ => Status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        PlotStatus.VerifiedInCatalog => "la-badge-green",
        PlotStatus.Sold => "la-badge-green",
        PlotStatus.RejectedByLawyer => "la-badge-red",
        _ => "la-badge-amber"
    };

    public void UpdatePrice(decimal newPriceUsd)
    {
        if (newPriceUsd > 0 && Status != PlotStatus.Sold)
        {
            PriceUsd = newPriceUsd;
        }
    }

    public void ChangeStatus(PlotStatus newStatus)
    {
        Status = newStatus;
    }

    public bool ValidateCadastralFormat()
    {
        if (string.IsNullOrWhiteSpace(CadastralNumber))
        {
            return false;
        }

        return Regex.IsMatch(CadastralNumber.Trim(), @"^\d{10}:\d{2}:\d{3}:\d{4}$");
    }
}
