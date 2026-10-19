namespace LandAgencyMvc.Models;

/// <summary>
/// Електронний витяг з Державного земельного кадастру (ДЗК) та Державного реєстру речових прав (ДРРП).
/// Містить результати валідації КОАТУУ, розрахунок НГО, аналіз ринкової ціни за сотку
/// та прямі верифікаційні посилання на публічні реєстри.
/// </summary>
public class DzkRegistryExtract
{
    public string ExtractNumber { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public string CadastralNumber { get; set; } = string.Empty;
    public bool IsCadastralFormatValid { get; set; }
    public string CadastralZoneCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public double RegisteredAreaHa { get; set; }
    public string RegisteredPurpose { get; set; } = string.Empty;
    public decimal NormativeMonetaryValuationUah { get; set; }
    public decimal PricePerSotkaUsd { get; set; }
    public decimal RegionalAvgPricePerSotkaUsd { get; set; }
    public bool IsPriceAnomalyDetected { get; set; }
    public string PriceAnomalyReport { get; set; } = string.Empty;
    public string KadastrLiveUrl { get; set; } = string.Empty;
    public string OpenDataBotUrl { get; set; } = string.Empty;
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
