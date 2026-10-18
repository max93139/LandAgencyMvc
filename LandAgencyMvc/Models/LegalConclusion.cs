namespace LandAgencyMvc.Models;

public class LegalConclusion
{
    public int Id { get; set; }

    public string ConclusionNumber { get; set; } = string.Empty;

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    public int? LawyerId { get; set; }

    public AppUser? Lawyer { get; set; }

    public bool NoArrestsOrMortgages { get; set; } = true;

    public bool NoServitudes { get; set; } = true;

    public bool BoundariesMatchDzk { get; set; } = true;

    public bool SellerMatchesDrp { get; set; } = true;

    public bool NoSanctions { get; set; } = true;

    public string RiskLevel { get; set; } = "Низький (Чисто)";

    public string DzkExtractNumber { get; set; } = string.Empty;

    public string ConclusionText { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool HasArrests => !NoArrestsOrMortgages;

    public bool HasServitudes => !NoServitudes;

    public void Approve(string text, string? riskLevel = null)
    {
        IsApproved = true;
        ConclusionText = text;
        if (!string.IsNullOrWhiteSpace(riskLevel))
        {
            RiskLevel = riskLevel.Trim();
        }
        else if (string.IsNullOrWhiteSpace(RiskLevel))
        {
            RiskLevel = "Низький (Чисто)";
        }
    }

    public void Reject(string reason, string? riskLevel = null)
    {
        IsApproved = false;
        ConclusionText = reason;
        RiskLevel = !string.IsNullOrWhiteSpace(riskLevel)
            ? riskLevel.Trim()
            : "Високий (Виявлено арешт / обтяження)";
    }
}
