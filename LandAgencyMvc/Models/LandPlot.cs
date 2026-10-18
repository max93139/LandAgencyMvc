using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace LandAgencyMvc.Models;

/// <summary>
/// Сутність земельної ділянки з кадастровими, географічними, фінансовими та юридичними реквізитами.
/// </summary>
public class LandPlot
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
}

