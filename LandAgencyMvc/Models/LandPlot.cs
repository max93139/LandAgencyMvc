using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace LandAgencyMvc.Models;

/// <summary>
/// Сутність земельної ділянки з кадастровими, географічними, фінансовими та юридичними реквізитами.
/// </summary>
public class LandPlot : IVersioned
{
    public int Id { get; set; }

    /// <summary>
    /// Кадастровий номер земельної ділянки у державному форматі України (19 знаків).
    /// </summary>
    [Required]
    public string CadastralNumber { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Площа земельної ділянки у гектарах.
    /// </summary>
    public double AreaHectares { get; set; }

    /// <summary>
    /// Цільове призначення згідно з Класифікатором видів цільового призначення земель (КВЦПЗ).
    /// </summary>
    [Required]
    public string Purpose { get; set; } = string.Empty;

    public decimal PriceUsd { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Номер правовстановлюючого документа (витяг ДРРП, договір купівлі-продажу, свідоцтво про право власності).
    /// </summary>
    public string OwnershipDocNumber { get; set; } = string.Empty;

    /// <summary>
    /// Банківські реквізити власника (IBAN) для безготівкового розрахунку під час нотаріальної угоди.
    /// </summary>
    public string SellerBankIban { get; set; } = string.Empty;

    /// <summary>
    /// Поточний статус ділянки у життєвому циклі лістингу.
    /// </summary>
    public PlotStatus Status { get; set; } = PlotStatus.Draft;

    public int SellerId { get; set; }

    public AppUser? Seller { get; set; }

    public int? AssignedRealtorId { get; set; }

    public AppUser? AssignedRealtor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Широта центру ділянки (WGS-84). Координати вносить рієлтор під час складання опису (UC-12).
    /// </summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public const int MaxSaleConditionsLength = 500;

    /// <summary>
    /// Умови продажу, які задає продавець (UC-10): форма розрахунку, строки, торг, обтяження.
    /// </summary>
    public string SaleConditions { get; set; } = string.Empty;

    public Guid Version { get; set; } = Guid.NewGuid();

    public LegalConclusion? LegalConclusion { get; set; }

    public ICollection<PlotDocument> Documents { get; set; } = new List<PlotDocument>();

    public ICollection<PlotMedia> Media { get; set; } = new List<PlotMedia>();

    public ICollection<ViewingRequest> ViewingRequests { get; set; } = new List<ViewingRequest>();

    [NotMapped]
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    [NotMapped]
    public string MapUrl => HasCoordinates
        ? string.Create(CultureInfo.InvariantCulture,
            $"https://www.openstreetmap.org/?mlat={Latitude}&mlon={Longitude}#map=16/{Latitude}/{Longitude}")
        : string.Empty;

    [NotMapped]
    public double AreaSotky => Math.Round(AreaHectares * 100.0, 2);

    [NotMapped]
    public decimal PricePerSotkaUsd => AreaHectares > 0
        ? Math.Round(PriceUsd / (decimal)(AreaHectares * 100.0), 0)
        : 0m;

    [NotMapped]
    public string KadastrLiveUrl => $"https://kadastr.live/parcel/{Uri.EscapeDataString((CadastralNumber ?? string.Empty).Trim())}";

    [NotMapped]
    public string OpenDataBotUrl => $"https://opendatabot.ua/l/{Uri.EscapeDataString((CadastralNumber ?? string.Empty).Trim())}";

    [NotMapped]
    public string EffectiveSellerIban => !string.IsNullOrWhiteSpace(SellerBankIban)
        ? SellerBankIban
        : (!string.IsNullOrWhiteSpace(Seller?.BankDetails) ? Seller.BankDetails : "Не вказано");

    [NotMapped]
    public PlotDocument? PrimaryDocument => Documents.FirstOrDefault();

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        PlotStatus.Draft => "Чернетка (ще не надіслано юристу)",
        PlotStatus.PendingLegalCheck => "На перевірці у юриста (ДЗК)",
        PlotStatus.VerifiedInCatalog => "Опубліковано ✓",
        PlotStatus.RejectedByLawyer => "Відхилено (Акт ризиків)",
        PlotStatus.Sold => "Продано (Угоду завершено)",
        _ => Status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        PlotStatus.Draft => "la-badge-indigo",
        PlotStatus.VerifiedInCatalog => "la-badge-green",
        PlotStatus.Sold => "la-badge-green",
        PlotStatus.RejectedByLawyer => "la-badge-red",
        _ => "la-badge-amber"
    };

    public static IReadOnlyList<(string Code, string FullValue, string ShortLabel)> StandardPurposes { get; } = new List<(string, string, string)>
    {
        ("02.01", "02.01 Для будівництва і обслуговування житлового будинку", "02.01 Житлова забудова"),
        ("01.03", "01.03 Для ведення особистого селянського господарства", "01.03 Особисте селянське господарство (ОСГ)"),
        ("01.05", "01.05 Для індивідуального садівництва", "01.05 Садівництво"),
        ("03.07", "03.07 Для будівництва та обслуговування будівель торгівлі", "03.07 Комерція та торгівля"),
        ("11.02", "11.02 Для розміщення промислових підприємств", "11.02 Промисловість та склади")
    };

    [NotMapped]
    public bool IsEditable => Status != PlotStatus.Sold;

    /// <summary>
    /// Кадастровий номер, площа, призначення, власник і номер правовстановлюючого документа закриті, поки ділянку
    /// перевіряє юрист і після того, як він її схвалив: юрист має перевіряти незмінні дані, а опублікована ділянка
    /// не може лишатися «перевіреною» з іншими фактами. Виправити їх можна лише після відхилення юристом.
    /// </summary>
    [NotMapped]
    public bool AreLegalFactsLocked => LegalFactsLockedFor(Status);

    public static bool LegalFactsLockedFor(PlotStatus status) =>
        status is PlotStatus.PendingLegalCheck or PlotStatus.VerifiedInCatalog;

    /// <summary>
    /// Чернетку або відхилену юристом ділянку можна відправити на перевірку, лише коли до неї додано
    /// хоча б один правовстановлюючий документ: без документа юристу нічого перевіряти.
    /// Потрібне завантаження навігації Documents.
    /// </summary>
    [NotMapped]
    public bool CanBeSentForLegalCheck =>
        (Status is PlotStatus.Draft or PlotStatus.RejectedByLawyer) && Documents.Any();

    /// <summary>
    /// Відправляє ділянку юристу. Робить це користувач окремою дією, а не система автоматично.
    /// </summary>
    /// <returns>False, якщо ділянку зараз відправити не можна.</returns>
    public bool SendForLegalCheck()
    {
        if (!CanBeSentForLegalCheck)
        {
            return false;
        }

        Status = PlotStatus.PendingLegalCheck;
        return true;
    }

    /// <summary>
    /// Вилучити можна лише чернетку, яку ще не відправляли юристу і до якої не додано документів.
    /// Документи, висновок юриста, огляди й угоди потрібні для звітності, тому вони блокують вилучення,
    /// а фото й відео ділянки вилучаються разом із нею каскадно.
    /// Потрібне завантаження навігацій Documents, ViewingRequests і LegalConclusion.
    /// </summary>
    [NotMapped]
    public bool CanBeDeleted =>
        Status == PlotStatus.Draft && !Documents.Any() && !ViewingRequests.Any() && LegalConclusion == null;

    /// <summary>
    /// Оновлює запитувану вартість ділянки, якщо вона ще не була відчужена за нотаріальною угодою.
    /// </summary>
    /// <param name="newPriceUsd">Нова ціна в доларах США.</param>
    public void UpdatePrice(decimal newPriceUsd)
    {
        if (newPriceUsd > 0 && Status != PlotStatus.Sold)
        {
            PriceUsd = newPriceUsd;
        }
    }

    /// <summary>
    /// Оновлює умови продажу, якщо ділянку ще не відчужено. Текст обрізається до дозволеної довжини.
    /// </summary>
    public void UpdateSaleConditions(string? conditions)
    {
        if (Status == PlotStatus.Sold)
        {
            return;
        }

        var trimmed = (conditions ?? string.Empty).Trim();
        SaleConditions = trimmed.Length > MaxSaleConditionsLength ? trimmed[..MaxSaleConditionsLength] : trimmed;
    }

    /// <summary>
    /// Змінює стан публікації ділянки (перевірка, затвердження, відхилення або продаж).
    /// </summary>
    public void ChangeStatus(PlotStatus newStatus)
    {
        Status = newStatus;
    }

    /// <summary>
    /// Перевіряє відповідність кадастрового номера державному стандарту України:
    /// 10 цифр області/району/зони, 2 цифри кварталу, 3 цифри блоку, 4 цифри парцелі.
    /// Приклад: 3222486200:01:002:1111.
    /// </summary>
    public bool ValidateCadastralFormat()
    {
        if (string.IsNullOrWhiteSpace(CadastralNumber))
        {
            return false;
        }

        return Regex.IsMatch(CadastralNumber.Trim(), @"^\d{10}:\d{2}:\d{3}:\d{4}$");
    }

    public bool IsOwnedBy(System.Security.Claims.ClaimsPrincipal? user)
    {
        var idClaim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var userId) && userId > 0 && SellerId == userId;
    }
}

