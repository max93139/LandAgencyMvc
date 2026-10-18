using System.ComponentModel.DataAnnotations.Schema;

namespace LandAgencyMvc.Models;

/// <summary>
/// Угода купівлі-продажу земельної ділянки.
/// Керує 3-етапним життєвим циклом:
/// 1) Підготовка проекту рієлтором (DraftPreparedByRealtor)
/// 2) Правова експертиза юристом (ApprovedByLawyerForNotary / ReturnedForRevision)
/// 3) Нотаріальне посвідчення та держреєстрація в ДРРП (SignedAtNotary -> CompletedAtNotary).
/// </summary>
public class SaleDeal
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    public int BuyerId { get; set; }

    public AppUser? Buyer { get; set; }

    public int? RealtorId { get; set; }

    public AppUser? Realtor { get; set; }

    public int? LawyerId { get; set; }

    public AppUser? Lawyer { get; set; }

    /// <summary>
    /// Фінальна ціна відчуження ділянки в доларах США.
    /// </summary>
    public decimal FinalPriceUsd { get; set; }

    /// <summary>
    /// Комісійна винагорода агентства (за замовчуванням 3% від фінальної ціни).
    /// </summary>
    public decimal AgencyCommissionUsd { get; set; }

    public string NotaryOffice { get; set; } = string.Empty;

    public DateTime NotaryDate { get; set; }

    public string DraftContractNumber { get; set; } = string.Empty;

    /// <summary>
    /// Номер у реєстрі нотаріальних дій після офіційного посвідчення договору.
    /// </summary>
    public string? NotarialRegistryNumber { get; set; }

    public string LawyerApprovalNotes { get; set; } = string.Empty;

    public DealStatus Status { get; set; } = DealStatus.DraftPreparedByRealtor;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [NotMapped]
    public bool IsFullyPaid => Payments.Any() && Payments.All(p => p.IsPaid);

    [NotMapped]
    public decimal TotalAmountUsd => FinalPriceUsd + AgencyCommissionUsd;

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        DealStatus.DraftPreparedByRealtor => "Очікує візи юриста",
        DealStatus.ReturnedForRevision => "Повернуто на доопрацювання",
        DealStatus.ApprovedByLawyerForNotary => "Погоджено до нотаріуса ✓",
        DealStatus.SignedAtNotary => "Посвідчено у нотаріуса ✓",
        DealStatus.CompletedAtNotary => "Право зареєстровано в ДРРП ✓",
        _ => Status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        DealStatus.ApprovedByLawyerForNotary => "la-badge-green",
        DealStatus.CompletedAtNotary => "la-badge-green",
        DealStatus.ReturnedForRevision => "la-badge-red",
        _ => "la-badge-indigo"
    };

    /// <summary>
    /// Ініціалізує чернетку договору купівлі-продажу та розраховує комісію агентства.
    /// </summary>
    public void PrepareContractDraft(string contractNumber, decimal finalPriceUsd, double commissionRatePercent = 3.0)
    {
        DraftContractNumber = contractNumber.Trim();
        FinalPriceUsd = finalPriceUsd;
        CalculateCommission(commissionRatePercent);
        Status = DealStatus.DraftPreparedByRealtor;
    }

    /// <summary>
    /// Розраховує суму комісійної винагороди агентства нерухомості.
    /// </summary>
    public void CalculateCommission(double ratePercent = 3.0)
    {
        AgencyCommissionUsd = Math.Round(FinalPriceUsd * (decimal)(ratePercent / 100.0), 2);
    }

    /// <summary>
    /// Генерує графік платежів угоди: оплату вартості продавцю за IBAN та комісію агентства.
    /// </summary>
    public List<Payment> CreateDefaultPayments(string? sellerIban, bool isPaid = false, string? senderIban = null, string? agencyIban = null)
    {
        string resolvedSellerIban = !string.IsNullOrWhiteSpace(sellerIban)
            ? sellerIban.Trim()
            : (!string.IsNullOrWhiteSpace(LandPlot?.Seller?.BankDetails)
                ? LandPlot.Seller.BankDetails.Trim()
                : "Рахунок продавця згідно з договором");

        string resolvedAgencyIban = !string.IsNullOrWhiteSpace(agencyIban)
            ? agencyIban.Trim()
            : (!string.IsNullOrWhiteSpace(Realtor?.BankDetails)
                ? Realtor.BankDetails.Trim()
                : "UA903052990000026000001002003");

        var p1 = new Payment
        {
            SaleDealId = Id,
            AmountUsd = FinalPriceUsd,
            PaymentType = "Оплата вартості земельної ділянки за договором (IBAN)",
            SenderIban = senderIban ?? string.Empty,
            RecipientIban = resolvedSellerIban,
            IsPaid = isPaid,
            PaymentDate = DateTime.Now
        };
        p1.GenerateReceiptNumber(Id, 1);

        var p2 = new Payment
        {
            SaleDealId = Id,
            AmountUsd = AgencyCommissionUsd,
            PaymentType = "Комісійна винагорода агентства (3%)",
            SenderIban = senderIban ?? string.Empty,
            RecipientIban = resolvedAgencyIban,
            IsPaid = isPaid,
            PaymentDate = DateTime.Now
        };
        p2.GenerateReceiptNumber(Id, 2);

        return new List<Payment> { p1, p2 };
    }

    /// <summary>
    /// Фіксує нотаріальне підписання договору сторонами після погодження юристом.
    /// </summary>
    /// <param name="notarialNumber">Номер запису в реєстрі нотаріальних дій.</param>
    public bool SignByParties(string notarialNumber)
    {
        if (Status != DealStatus.ApprovedByLawyerForNotary && Status != DealStatus.SignedAtNotary)
        {
            return false;
        }

        NotarialRegistryNumber = notarialNumber.Trim();
        Status = DealStatus.SignedAtNotary;
        return true;
    }

    /// <summary>
    /// Завершує угоду: фіксує держреєстрацію права власності за покупцем у ДРРП
    /// та переводить земельну ділянку у статус 'Sold' (Продано).
    /// </summary>
    public bool RegisterOwnershipRight()
    {
        if (Status != DealStatus.SignedAtNotary && Status != DealStatus.CompletedAtNotary)
        {
            return false;
        }

        Status = DealStatus.CompletedAtNotary;
        if (LandPlot != null)
        {
            LandPlot.ChangeStatus(PlotStatus.Sold);
        }

        return true;
    }
}

