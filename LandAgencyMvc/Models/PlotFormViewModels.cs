using System.ComponentModel.DataAnnotations;

namespace LandAgencyMvc.Models;

/// <summary>
/// Базова модель представлення форми земельної ділянки.
/// Визначає атрибути DataAnnotations, що використовуються як для створення, так і для редагування.
/// </summary>
public class PlotCreateViewModel
{
    [Required(ErrorMessage = "Оберіть власника (продавця) ділянки")]
    [Display(Name = "Власник (продавець)")]
    public int SellerId { get; set; }

    [Required(ErrorMessage = "Вкажіть кадастровий номер")]
    [RegularExpression(@"^\d{10}:\d{2}:\d{3}:\d{4}$", ErrorMessage = "Формат кадастрового номера: XXXXXXXXXX:XX:XXX:XXXX (19 символів)")]
    [Display(Name = "Кадастровий номер")]
    public string CadastralNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть назву ділянки")]
    [Display(Name = "Назва ділянки")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть адресу або місцезнаходження")]
    [Display(Name = "Місцезнаходження (адреса)")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть площу")]
    [Range(0.01, 10000.0, ErrorMessage = "Площа має становити від 0.01 до 10 000 гектарів")]
    [Display(Name = "Площа (гектарів)")]
    public double AreaHectares { get; set; } = 0.15;

    [Required(ErrorMessage = "Оберіть цільове призначення")]
    [Display(Name = "Цільове призначення (КВЦПЗ)")]
    public string Purpose { get; set; } = "02.01 Для будівництва і обслуговування житлового будинку";

    [Required(ErrorMessage = "Вкажіть запитувану вартість")]
    [Range(100, 100000000, ErrorMessage = "Ціна має бути не менше $100")]
    [Display(Name = "Ціна (USD)")]
    public decimal PriceUsd { get; set; } = 25000m;

    [Required(ErrorMessage = "Вкажіть номер документа власності")]
    [Display(Name = "Номер правовстановлюючого документа")]
    public string OwnershipDocNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть банківські реквізити IBAN")]
    [RegularExpression(@"^UA\d{27}$", ErrorMessage = "IBAN має починатися з літер UA та містити 27 цифр")]
    [Display(Name = "IBAN рахунку продавця")]
    public string SellerBankIban { get; set; } = string.Empty;

    [Display(Name = "Комерційний опис об'єкта")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Назва первинного документа")]
    public string? DocTitle { get; set; }

    [Display(Name = "Тип документа")]
    public string? DocType { get; set; } = "Витяг з ДРРП про право власності";

    [Display(Name = "Дата видачі")]
    public DateTime? DocIssueDate { get; set; } = DateTime.Today;
}

/// <summary>
/// Елемент списку прикріплених документів при редагуванні ділянки.
/// </summary>
public class DocumentEditItem
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву документа")]
    [Display(Name = "Назва")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть тип документа")]
    [Display(Name = "Тип")]
    public string DocType { get; set; } = "Витяг з ДЗК";

    [Required(ErrorMessage = "Вкажіть номер документа")]
    [Display(Name = "Номер")]
    public string DocumentNumber { get; set; } = string.Empty;

    [Display(Name = "Дата видачі")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Display(Name = "Вилучити цей документ")]
    public bool Remove { get; set; }
}

/// <summary>
/// Похідна модель представлення форми редагування ділянки (успадкування PlotCreateViewModel).
/// Демонструє поліморфізм у частковому представленні _PlotFormFields.cshtml.
/// </summary>
public class PlotEditViewModel : PlotCreateViewModel
{
    public int Id { get; set; }

    public PlotStatus Status { get; set; }

    public string StatusDisplayName => Status switch
    {
        PlotStatus.PendingLegalCheck => "На перевірці у юриста (ДЗК)",
        PlotStatus.VerifiedInCatalog => "Опубліковано ✓",
        PlotStatus.RejectedByLawyer => "Відхилено (Акт ризиків)",
        PlotStatus.Sold => "Продано (Угоду завершено)",
        _ => Status.ToString()
    };

    public List<DocumentEditItem> Documents { get; set; } = new();

    public static PlotEditViewModel From(LandPlot plot)
    {
        var vm = new PlotEditViewModel
        {
            Id = plot.Id,
            SellerId = plot.SellerId,
            CadastralNumber = plot.CadastralNumber,
            Title = plot.Title,
            Location = plot.Location,
            AreaHectares = plot.AreaHectares,
            Purpose = plot.Purpose,
            PriceUsd = plot.PriceUsd,
            OwnershipDocNumber = plot.OwnershipDocNumber,
            SellerBankIban = plot.SellerBankIban,
            Description = plot.Description,
            Status = plot.Status,
            Documents = plot.Documents.Select(d => new DocumentEditItem
            {
                Id = d.Id,
                Title = d.Title,
                DocType = d.DocType,
                DocumentNumber = d.DocumentNumber,
                IssueDate = d.IssueDate
            }).ToList()
        };

        // Запасний порожній рядок для додавання нового документа при редагуванні
        vm.Documents.Add(new DocumentEditItem
        {
            Title = string.Empty,
            DocType = "Витяг з ДЗК",
            DocumentNumber = string.Empty,
            IssueDate = DateTime.Today
        });

        return vm;
    }

    public void ApplyTo(LandPlot plot)
    {
        plot.SellerId = SellerId;
        plot.CadastralNumber = CadastralNumber.Trim();
        plot.Title = Title.Trim();
        plot.Location = Location.Trim();
        plot.AreaHectares = AreaHectares;
        plot.Purpose = Purpose.Trim();
        plot.PriceUsd = PriceUsd;
        plot.OwnershipDocNumber = OwnershipDocNumber.Trim();
        plot.SellerBankIban = SellerBankIban.Trim().ToUpperInvariant();
        plot.Description = (Description ?? string.Empty).Trim();

        foreach (var item in Documents)
        {
            if (item.Id.HasValue && item.Id.Value > 0)
            {
                var doc = plot.Documents.FirstOrDefault(d => d.Id == item.Id.Value);
                if (doc != null)
                {
                    if (item.Remove)
                    {
                        plot.Documents.Remove(doc);
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.DocumentNumber))
                    {
                        doc.Title = item.Title.Trim();
                        doc.DocType = item.DocType.Trim();
                        doc.DocumentNumber = item.DocumentNumber.Trim();
                        doc.IssueDate = item.IssueDate;
                    }
                }
            }
            else if (!item.Remove && !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.DocumentNumber))
            {
                var newDoc = new PlotDocument
                {
                    LandPlotId = plot.Id,
                    Title = item.Title.Trim(),
                    DocType = item.DocType.Trim(),
                    DocumentNumber = item.DocumentNumber.Trim(),
                    IssueDate = item.IssueDate,
                    IsVerified = plot.Status == PlotStatus.VerifiedInCatalog
                };
                var dummyBytes = System.Text.Encoding.UTF8.GetBytes($"{newDoc.DocType} №{newDoc.DocumentNumber} для ділянки {plot.CadastralNumber}");
                newDoc.AttachFile(string.Empty, dummyBytes, "application/pdf");
                plot.Documents.Add(newDoc);
            }
        }
    }
}

/// <summary>
/// Модель сторінки підтвердження вилучення ділянки для демонстрації каскадного і некаскадного вилучення.
/// </summary>
public class DeletePlotConfirmationViewModel
{
    public LandPlot Plot { get; set; } = null!;
    public int DocumentsCount { get; set; }
    public int ViewingsCount { get; set; }
    public bool HasLegalConclusion { get; set; }
    public int DealsCount { get; set; }

    public bool CanBeDeletedNonCascade =>
        DealsCount == 0 && DocumentsCount == 0 && ViewingsCount == 0 && !HasLegalConclusion;

    public bool CanBeDeletedCascade => DealsCount == 0 && Plot.Status != PlotStatus.Sold;
}

/// <summary>
/// Модель форми редагування чернетки нотаріальної угоди з перерахунком комісії та рахунків.
/// </summary>
public class DealEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "Номер чернетки")]
    public string DraftContractNumber { get; set; } = string.Empty;

    [Display(Name = "Об'єкт нерухомості")]
    public string PlotTitle { get; set; } = string.Empty;

    [Display(Name = "Кадастровий номер")]
    public string CadastralNumber { get; set; } = string.Empty;

    [Display(Name = "Покупець")]
    public string BuyerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть узгоджену суму продажу")]
    [Range(500, 100000000, ErrorMessage = "Сума має становити не менше $500")]
    [Display(Name = "Фінальна ціна ділянки (USD)")]
    public decimal FinalPriceUsd { get; set; }

    [Required(ErrorMessage = "Вкажіть нотаріальну контору")]
    [Display(Name = "Нотаріальна контора")]
    public string NotaryOffice { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть заплановану дату посвідчення")]
    [Display(Name = "Дата посвідчення угоди")]
    public DateTime NotaryDate { get; set; }

    [Display(Name = "Комісійна винагорода агентства (3%)")]
    public decimal AgencyCommissionUsd => Math.Round(FinalPriceUsd * 0.03m, 2);

    [Display(Name = "Загальна сума до сплати")]
    public decimal TotalAmountUsd => FinalPriceUsd + AgencyCommissionUsd;
}
