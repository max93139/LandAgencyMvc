namespace LandAgencyMvc.Services;

/// <summary>
/// Результат валідації кадастрового номера за структурою КОАТУУ / КАТОТТГ.
/// </summary>
public class KoatuuValidationResult
{
    public bool IsValid { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string CadastralZone { get; set; } = string.Empty;
    public string CadastralQuarter { get; set; } = string.Empty;
    public string ParcelNumber { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Результат оцінки ринкової вартості сотки ділянки та виявлення аномалій.
/// </summary>
public class MarketPriceAnalysisResult
{
    public decimal PricePerSotkaUsd { get; set; }
    public decimal RegionalAvgPricePerSotkaUsd { get; set; }
    public bool IsAnomalyDetected { get; set; }
    public string ValuationRiskLevel { get; set; } = "Норма";
    public string AnalysisReport { get; set; } = string.Empty;
}

/// <summary>
/// Сервіс валідації кадастрових номерів за стандартами КОАТУУ/КАТОТТГ,
/// виявлення цінових аномалій за сотку та генерації посилань на публічні реєстри.
/// </summary>
public interface ICadastralValidationService
{
    KoatuuValidationResult ValidateKoatuuFormat(string cadastralNumber);
    Task<MarketPriceAnalysisResult> AnalyzePricePerSotkaAsync(decimal priceUsd, double areaHectares, string purpose, string cadastralNumber);
    string GetKadastrLiveUrl(string cadastralNumber);
    string GetOpenDataBotUrl(string cadastralNumber);
}
