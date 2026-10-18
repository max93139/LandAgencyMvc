namespace LandAgencyMvc.Models;

public class DzkRegistryExtract
{
    public string ExtractNumber { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public string CadastralNumber { get; set; } = string.Empty;
    public bool IsCadastralFormatValid { get; set; }
    public string CadastralZoneCode { get; set; } = string.Empty;
    public double RegisteredAreaHa { get; set; }
    public string RegisteredPurpose { get; set; } = string.Empty;
    public decimal NormativeMonetaryValuationUah { get; set; }
    public string RegisteredOwnerName { get; set; } = string.Empty;
    public string RegisteredOwnerTaxId { get; set; } = string.Empty;
    public string OwnershipRecordNumber { get; set; } = string.Empty;
    public bool HasDrrpArrestsOrMortgages { get; set; }
    public bool HasDzkServitudesOrRestrictions { get; set; }
    public bool BoundariesMatchPublicMap { get; set; }
    public bool OwnerAbsentInDebtorsRegistry { get; set; }
    public string RecommendedRiskLevel { get; set; } = string.Empty;
    public string SummaryReport { get; set; } = string.Empty;
}
