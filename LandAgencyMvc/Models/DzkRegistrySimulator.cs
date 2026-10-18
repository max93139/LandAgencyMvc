namespace LandAgencyMvc.Models;

/// <summary>
/// Сервіс емуляції міжвідомчої взаємодії з Державним земельним кадастром (ДЗК)
/// та Державним реєстром речових прав на нерухоме майно (ДРРП).
/// Виконує автоматичну верифікацію кадастрового формату, нормативної грошової оцінки (НГО) та факторів ризику.
/// </summary>
public static class DzkRegistrySimulator
{
    /// <summary>
    /// Формує електронний витяг з реєстрів ДЗК/ДРРП, перевіряючи наявність арештів,
    /// сервітутів, накладок меж і розраховуючи нормативну грошову оцінку.
    /// </summary>
    /// <param name="plot">Об'єкт земельної ділянки для аудиту.</param>
    /// <returns>Структурований витяг DzkRegistryExtract із рекомендацією рівня правового ризику.</returns>
    public static DzkRegistryExtract QueryRegistries(LandPlot plot)
    {
        var cadastral = (plot.CadastralNumber ?? string.Empty).Trim();
        bool validFormat = plot.ValidateCadastralFormat();
        string zoneCode = cadastral.Length >= 10 ? cadastral[..10] : "3222486200";

        string lowerDesc = $"{plot.Title} {plot.Description} {plot.OwnershipDocNumber}".ToLowerInvariant();
        bool hasSimulatedArrest = cadastral.EndsWith(":0000") ||
                                  cadastral.EndsWith(":9999") ||
                                  lowerDesc.Contains("арешт") ||
                                  lowerDesc.Contains("іпотек") ||
                                  lowerDesc.Contains("спір");

        bool hasSimulatedServitude = cadastral.EndsWith(":0009") ||
                                     lowerDesc.Contains("сервітут") ||
                                     lowerDesc.Contains("охоронна зона");

        bool boundariesMatch = validFormat && !lowerDesc.Contains("накладка меж");

        decimal baseRatePerHaUah = plot.Purpose.StartsWith("02.") ? 680000m
            : plot.Purpose.StartsWith("03.") ? 950000m
            : 145000m;
        decimal ngoUah = Math.Round((decimal)Math.Max(plot.AreaHectares, 0.01) * baseRatePerHaUah, 2);

        string riskLevel = hasSimulatedArrest || !boundariesMatch
            ? "Високий (Виявлено арешт / обтяження)"
            : hasSimulatedServitude
                ? "Середній (Наявний земельний сервітут)"
                : "Низький (Чисто)";

        string summary = hasSimulatedArrest
            ? $"За даними автоматизованого обміну з ДЗК та ДРРП по ділянці {cadastral} виявлено обтяження або арешт у Державному реєстрі речових прав. Рекомендовано сформувати акт ризиків та відхилити публікацію."
            : $"За даними автоматизованого запиту до АС ДЗК та ДРРП ділянка {cadastral} зареєстрована належним чином, координати поворотних точок меж узгоджені, судових арештів, іпотек та заборон відчуження не виявлено.";

        return new DzkRegistryExtract
        {
            ExtractNumber = $"ВТ-ДЗК-{DateTime.Now:yyyy}-{plot.Id:D4}",
            RequestedAt = DateTime.Now,
            CadastralNumber = cadastral,
            IsCadastralFormatValid = validFormat,
            CadastralZoneCode = zoneCode,
            RegisteredAreaHa = plot.AreaHectares,
            RegisteredPurpose = plot.Purpose,
            NormativeMonetaryValuationUah = ngoUah,
            RegisteredOwnerName = plot.Seller?.FullName ?? "Не вказано",
            RegisteredOwnerTaxId = plot.Seller?.TaxId ?? "Не вказано",
            OwnershipRecordNumber = plot.OwnershipDocNumber,
            HasDrrpArrestsOrMortgages = hasSimulatedArrest,
            HasDzkServitudesOrRestrictions = hasSimulatedServitude,
            BoundariesMatchPublicMap = boundariesMatch,
            OwnerAbsentInDebtorsRegistry = !hasSimulatedArrest,
            RecommendedRiskLevel = riskLevel,
            SummaryReport = summary
        };
    }
}
