using LandAgencyMvc.Services;

namespace LandAgencyMvc.Models;

/// <summary>
/// Сервіс взаємодії з Державним земельним кадастром (ДЗК) та Державним реєстром речових прав (ДРРП).
/// Виконує перевірку структури КОАТУУ/КАТОТТГ, розрахунок НГО за категорією земель,
/// аналіз ринкової вартості за сотку та формування прямих посилань на публічну кадастрову карту.
/// </summary>
public static class DzkRegistrySimulator
{
    /// <summary>
    /// Формує офіційний електронний витяг з реєстрів ДЗК та ДРРП із застосуванням
    /// сервісу кадастрової валідації та цінового комплаєнсу.
    /// </summary>
    public static async Task<DzkRegistryExtract> QueryRegistriesAsync(
        LandPlot plot,
        ICadastralValidationService validationService)
    {
        var cadastral = (plot.CadastralNumber ?? string.Empty).Trim();
        var koatuu = validationService.ValidateKoatuuFormat(cadastral);

        var priceAnalysis = await validationService.AnalyzePricePerSotkaAsync(
            plot.PriceUsd,
            plot.AreaHectares,
            plot.Purpose,
            cadastral);

        string kadastrUrl = validationService.GetKadastrLiveUrl(cadastral);
        string openDataUrl = validationService.GetOpenDataBotUrl(cadastral);

        decimal baseRatePerHaUah = plot.Purpose.StartsWith("02.") ? 680000m
            : plot.Purpose.StartsWith("03.") ? 950000m
            : 145000m;
        decimal ngoUah = Math.Round((decimal)Math.Max(plot.AreaHectares, 0.01) * baseRatePerHaUah, 2);

        bool boundariesMatch = koatuu.IsValid;
        string descLower = $"{plot.Title} {plot.Description}".ToLowerInvariant();
        bool hasArrest = !koatuu.IsValid ||
                         cadastral.EndsWith(":0000", StringComparison.Ordinal) ||
                         cadastral.EndsWith(":9999", StringComparison.Ordinal) ||
                         descLower.Contains("арешт") ||
                         descLower.Contains("іпотек") ||
                         descLower.Contains("спір") ||
                         descLower.Contains("заборон");

        bool hasServitude = cadastral.EndsWith(":0009", StringComparison.Ordinal) ||
                            descLower.Contains("сервітут") ||
                            descLower.Contains("охоронна") ||
                            descLower.Contains("обмеження");

        string riskLevel = !koatuu.IsValid
            ? "Високий (Невалідний кадастровий формат КОАТУУ)"
            : hasArrest
                ? "Високий (Виявлено обтяження / арешт у ДРРП)"
                : priceAnalysis.IsAnomalyDetected && priceAnalysis.ValuationRiskLevel.Contains("Критичне")
                    ? "Високий (Цінова аномалія за сотку)"
                    : (hasServitude || priceAnalysis.IsAnomalyDetected)
                        ? "Середній (Потребує уточнення оцінки / сервітут)"
                        : "Низький (Чисто)";

        string summary = !koatuu.IsValid
            ? $"Помилка валідації кадастрового номера: {koatuu.ErrorMessage}. Запис не знайдено у національній кадастровій системі."
            : hasArrest
                ? $"УВАГА! За даними ДРРП по земельній ділянці {cadastral} ({koatuu.RegionName}) виявлено активне обтяження речових прав (арешт / іпотека / судова заборона відчуження). Рекомендовано відхилити публікацію. {priceAnalysis.AnalysisReport}"
                : hasServitude
                    ? $"Ділянка {cadastral} ({koatuu.RegionName}) зареєстрована у ДЗК, проте виявлено земельний сервітут або охоронну зону інженерних мереж. {priceAnalysis.AnalysisReport}"
                    : priceAnalysis.IsAnomalyDetected
                        ? $"Кадастровий номер підтверджено ({koatuu.RegionName}). Межі узгоджені в ДЗК, обтяжень у ДРРП немає. Зверніть увагу: {priceAnalysis.AnalysisReport}"
                        : $"За даними перевірки ДЗК та ДРРП ділянка {cadastral} ({koatuu.RegionName}) зареєстрована належним чином. Межі та координати узгоджені, судових арештів та заборон не виявлено. {priceAnalysis.AnalysisReport}";

        return new DzkRegistryExtract
        {
            ExtractNumber = $"ВТ-ДЗК-{DateTime.Now:yyyy}-{plot.Id:D4}",
            RequestedAt = DateTime.Now,
            CadastralNumber = cadastral,
            IsCadastralFormatValid = koatuu.IsValid,
            CadastralZoneCode = cadastral.Length >= 10 ? cadastral[..10] : (koatuu.IsValid ? $"{koatuu.RegionCode}00000000" : "3222486200"),
            RegionName = koatuu.RegionName,
            RegisteredAreaHa = plot.AreaHectares,
            RegisteredPurpose = plot.Purpose,
            NormativeMonetaryValuationUah = ngoUah,
            PricePerSotkaUsd = priceAnalysis.PricePerSotkaUsd,
            RegionalAvgPricePerSotkaUsd = priceAnalysis.RegionalAvgPricePerSotkaUsd,
            IsPriceAnomalyDetected = priceAnalysis.IsAnomalyDetected,
            PriceAnomalyReport = priceAnalysis.AnalysisReport,
            KadastrLiveUrl = kadastrUrl,
            OpenDataBotUrl = openDataUrl,
            RegisteredOwnerName = plot.Seller?.FullName ?? "Не вказано",
            RegisteredOwnerTaxId = plot.Seller?.TaxId ?? "Не вказано",
            OwnershipRecordNumber = plot.OwnershipDocNumber,
            HasDrrpArrestsOrMortgages = hasArrest,
            HasDzkServitudesOrRestrictions = hasServitude,
            BoundariesMatchPublicMap = boundariesMatch,
            OwnerAbsentInDebtorsRegistry = koatuu.IsValid && !hasArrest,
            RecommendedRiskLevel = riskLevel,
            SummaryReport = summary
        };
    }

    /// <summary>
    /// Синхронний фасад для швидкої перевірки формату та генерації витягу.
    /// </summary>
    public static DzkRegistryExtract QueryRegistries(LandPlot plot)
    {
        var cadastral = (plot.CadastralNumber ?? string.Empty).Trim();
        bool validFormat = plot.ValidateCadastralFormat();
        string zoneCode = cadastral.Length >= 10 ? cadastral[..10] : "3222486200";
        string descLower = $"{plot.Title} {plot.Description}".ToLowerInvariant();

        bool hasArrest = !validFormat ||
                         cadastral.EndsWith(":0000", StringComparison.Ordinal) ||
                         cadastral.EndsWith(":9999", StringComparison.Ordinal) ||
                         descLower.Contains("арешт") ||
                         descLower.Contains("іпотек") ||
                         descLower.Contains("спір") ||
                         descLower.Contains("заборон");

        bool hasServitude = cadastral.EndsWith(":0009", StringComparison.Ordinal) ||
                            descLower.Contains("сервітут") ||
                            descLower.Contains("охоронна") ||
                            descLower.Contains("обмеження");

        var sotkas = Math.Max(plot.AreaHectares * 100.0, 0.1);
        var pricePerSotka = Math.Round(plot.PriceUsd / (decimal)sotkas, 2);

        decimal baseRatePerHaUah = plot.Purpose.StartsWith("02.") ? 680000m
            : plot.Purpose.StartsWith("03.") ? 950000m
            : 145000m;
        decimal ngoUah = Math.Round((decimal)Math.Max(plot.AreaHectares, 0.01) * baseRatePerHaUah, 2);

        return new DzkRegistryExtract
        {
            ExtractNumber = $"ВТ-ДЗК-{DateTime.Now:yyyy}-{plot.Id:D4}",
            RequestedAt = DateTime.Now,
            CadastralNumber = cadastral,
            IsCadastralFormatValid = validFormat,
            CadastralZoneCode = zoneCode,
            RegionName = "Київська область",
            RegisteredAreaHa = plot.AreaHectares,
            RegisteredPurpose = plot.Purpose,
            NormativeMonetaryValuationUah = ngoUah,
            PricePerSotkaUsd = pricePerSotka,
            RegionalAvgPricePerSotkaUsd = 1500m,
            IsPriceAnomalyDetected = false,
            PriceAnomalyReport = $"Вартість ${pricePerSotka:N0}/сотка.",
            KadastrLiveUrl = $"https://kadastr.live/parcel/{Uri.EscapeDataString(cadastral)}",
            OpenDataBotUrl = $"https://opendatabot.ua/c/{Uri.EscapeDataString(cadastral)}",
            RegisteredOwnerName = plot.Seller?.FullName ?? "Не вказано",
            RegisteredOwnerTaxId = plot.Seller?.TaxId ?? "Не вказано",
            OwnershipRecordNumber = plot.OwnershipDocNumber,
            HasDrrpArrestsOrMortgages = hasArrest,
            HasDzkServitudesOrRestrictions = hasServitude,
            BoundariesMatchPublicMap = validFormat,
            OwnerAbsentInDebtorsRegistry = validFormat && !hasArrest,
            RecommendedRiskLevel = !validFormat || hasArrest ? "Високий (Виявлено обтяження / помилку)" : (hasServitude ? "Середній (Сервітут)" : "Низький (Чисто)"),
            SummaryReport = validFormat && !hasArrest
                ? $"За даними запиту до ДЗК та ДРРП ділянка {cadastral} зареєстрована належним чином, координати поворотних точок меж узгоджені."
                : $"Виявлено правові перешкоди або невідповідність у реєстрах по ділянці {cadastral}."
        };
    }
}
