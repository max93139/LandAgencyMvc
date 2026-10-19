using System.Text.RegularExpressions;
using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

/// <summary>
/// Реалізація сервісу кадастрової валідації, розрахунку ринкових аномалій за сотку
/// та формування прямих посилань на публічні кадастрові системи України.
/// </summary>
public class CadastralValidationService : ICadastralValidationService
{
    private readonly LandAgencyContext _db;

    private static readonly Dictionary<string, string> UkrainianRegions = new()
    {
        { "01", "Автономна Республіка Крим" },
        { "05", "Вінницька область" },
        { "07", "Волинська область" },
        { "12", "Дніпропетровська область" },
        { "14", "Донецька область" },
        { "18", "Житомирська область" },
        { "21", "Закарпатська область" },
        { "23", "Запорізька область" },
        { "26", "Івано-Франківська область" },
        { "32", "Київська область" },
        { "35", "Кіровоградська область" },
        { "44", "Луганська область" },
        { "46", "Львівська область" },
        { "48", "Миколаївська область" },
        { "51", "Одеська область" },
        { "53", "Полтавська область" },
        { "56", "Рівненська область" },
        { "59", "Сумська область" },
        { "61", "Тернопільська область" },
        { "63", "Харківська область" },
        { "65", "Херсонська область" },
        { "68", "Хмельницька область" },
        { "71", "Черкаська область" },
        { "73", "Чернівецька область" },
        { "74", "Чернігівська область" },
        { "80", "місто Київ" },
        { "85", "місто Севастополь" }
    };

    public CadastralValidationService(LandAgencyContext db)
    {
        _db = db;
    }

    public KoatuuValidationResult ValidateKoatuuFormat(string cadastralNumber)
    {
        if (string.IsNullOrWhiteSpace(cadastralNumber))
        {
            return new KoatuuValidationResult
            {
                IsValid = false,
                ErrorMessage = "Кадастровий номер не може бути порожнім."
            };
        }

        var trimmed = cadastralNumber.Trim();
        var match = Regex.Match(trimmed, @"^(\d{10}):(\d{2}):(\d{3}):(\d{4})$");
        if (!match.Success)
        {
            return new KoatuuValidationResult
            {
                IsValid = false,
                ErrorMessage = "Невірний формат. Кадастровий номер має відповідати формату XXXXXXXXXX:XX:XXX:XXXX (19 цифр)."
            };
        }

        var koatuu = match.Groups[1].Value;
        var zone = match.Groups[2].Value;
        var quarter = match.Groups[3].Value;
        var parcel = match.Groups[4].Value;

        var regionCode = koatuu[..2];
        if (!UkrainianRegions.TryGetValue(regionCode, out var regionName))
        {
            return new KoatuuValidationResult
            {
                IsValid = false,
                RegionCode = regionCode,
                ErrorMessage = $"Код регіону '{regionCode}' не відповідає жодній області або місту спеціального статусу України згідно з класифікатором КОАТУУ/КАТОТТГ."
            };
        }

        return new KoatuuValidationResult
        {
            IsValid = true,
            RegionCode = regionCode,
            RegionName = regionName,
            CadastralZone = zone,
            CadastralQuarter = quarter,
            ParcelNumber = parcel
        };
    }

    public async Task<MarketPriceAnalysisResult> AnalyzePricePerSotkaAsync(
        decimal priceUsd,
        double areaHectares,
        string purpose,
        string cadastralNumber)
    {
        var sotkas = Math.Max(areaHectares * 100.0, 0.1);
        var pricePerSotka = Math.Round(priceUsd / (decimal)sotkas, 2);

        var koatuu = ValidateKoatuuFormat(cadastralNumber);
        var regionPrefix = koatuu.IsValid ? koatuu.RegionCode : "32";

        var purposePrefix = !string.IsNullOrWhiteSpace(purpose) && purpose.Length >= 2
            ? purpose[..2]
            : "02";

        var verifiedPlots = await _db.LandPlots
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog && p.AreaHectares > 0 && p.PriceUsd > 0)
            .ToListAsync();

        var comparablePlots = verifiedPlots
            .Where(p => p.CadastralNumber.StartsWith(regionPrefix) && p.Purpose.StartsWith(purposePrefix))
            .ToList();

        if (!comparablePlots.Any())
        {
            comparablePlots = verifiedPlots
                .Where(p => p.Purpose.StartsWith(purposePrefix))
                .ToList();
        }

        decimal regionalAvgPerSotka;
        if (comparablePlots.Any())
        {
            regionalAvgPerSotka = Math.Round(
                comparablePlots.Average(p => p.PriceUsd / (decimal)(p.AreaHectares * 100.0)),
                2);
        }
        else
        {
            regionalAvgPerSotka = purposePrefix switch
            {
                "03" => 3200m,
                "01" => 450m,
                _ => 1500m
            };
        }

        bool isAnomaly = false;
        string riskLevel = "Низький (У межах ринку)";
        string report;

        if (pricePerSotka < regionalAvgPerSotka * 0.40m)
        {
            isAnomaly = true;
            riskLevel = "Високий (Критичне заниження ціни)";
            report = $"Ціна ${pricePerSotka:N0}/сотка складає менше 40% від середньоринкової по регіону (${regionalAvgPerSotka:N0}/сотка). Виявлено ризик тіньового розрахунку, прихованих судових обтяжень або спроби заниження податкової бази.";
        }
        else if (pricePerSotka > regionalAvgPerSotka * 2.80m)
        {
            isAnomaly = true;
            riskLevel = "Середній (Суттєво вище ринку)";
            report = $"Ціна ${pricePerSotka:N0}/сотка перевищує ринковий діапазон регіону (${regionalAvgPerSotka:N0}/сотка) більш ніж у 2.8 раза. Високий ризик тривалої експозиції та завищених очікувань продавця.";
        }
        else
        {
            report = $"Вартість ${pricePerSotka:N0}/сотка знаходиться у межах допустимого ринкового діапазону для даного цільового призначення (середня по регіону: ${regionalAvgPerSotka:N0}/сотка).";
        }

        return new MarketPriceAnalysisResult
        {
            PricePerSotkaUsd = pricePerSotka,
            RegionalAvgPricePerSotkaUsd = regionalAvgPerSotka,
            IsAnomalyDetected = isAnomaly,
            ValuationRiskLevel = riskLevel,
            AnalysisReport = report
        };
    }

    public string GetKadastrLiveUrl(string cadastralNumber)
    {
        var clean = (cadastralNumber ?? string.Empty).Trim();
        return $"https://kadastr.live/parcel/{clean}";
    }

    public string GetOpenDataBotUrl(string cadastralNumber)
    {
        var clean = (cadastralNumber ?? string.Empty).Trim();
        return $"https://opendatabot.ua/c/{clean}";
    }
}
