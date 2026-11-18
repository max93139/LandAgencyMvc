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

    [StringLength(LandPlot.MaxSaleConditionsLength, ErrorMessage = "Умови продажу: не більше 500 символів")]
    [Display(Name = "Умови продажу")]
    public string SaleConditions { get; set; } = string.Empty;

    [Range(44.0, 53.0, ErrorMessage = "Широта України: від 44 до 53 градусів")]
    [Display(Name = "Широта (WGS-84)")]
    public double? Latitude { get; set; }

    [Range(22.0, 41.0, ErrorMessage = "Довгота України: від 22 до 41 градуса")]
    [Display(Name = "Довгота (WGS-84)")]
    public double? Longitude { get; set; }

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

    [Display(Name = "Назва")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Вкажіть тип документа")]
    [Display(Name = "Тип")]
    public string DocType { get; set; } = "Витяг з ДЗК";

    [Display(Name = "Номер")]
    public string? DocumentNumber { get; set; }

    [Display(Name = "Дата видачі")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Display(Name = "Вилучити цей документ")]
    public bool Remove { get; set; }

    /// <summary>
    /// Лише для показу: верифікований юристом документ рієлтор не змінює й не вилучає.
    /// </summary>
    public bool IsVerified { get; set; }
}

/// <summary>
/// Похідна модель представлення форми редагування ділянки (успадкування PlotCreateViewModel).
/// Демонструє поліморфізм у частковому представленні _PlotFormFields.cshtml.
/// </summary>
public class PlotEditViewModel : PlotCreateViewModel
{
    public int Id { get; set; }

    public Guid Version { get; set; }

    public PlotStatus Status { get; set; }

    public bool LegalFactsLocked => LandPlot.LegalFactsLockedFor(Status);

    public string StatusDisplayName => Status switch
    {
        PlotStatus.Draft => "Чернетка",
        PlotStatus.PendingLegalCheck => "На перевірці у юриста (ДЗК)",
        PlotStatus.VerifiedInCatalog => "Опубліковано ✓",
        PlotStatus.RejectedByLawyer => "Відхилено (Акт ризиків)",
        PlotStatus.Sold => "Продано (Угоду завершено)",
        _ => Status.ToString()
    };

    public List<DocumentEditItem> Documents { get; set; } = new();

    public List<PlotMedia> Media { get; set; } = new();

    public static PlotEditViewModel From(LandPlot plot)
    {
        var vm = new PlotEditViewModel
        {
            Id = plot.Id,
            Version = plot.Version,
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
            SaleConditions = plot.SaleConditions,
            Latitude = plot.Latitude,
            Longitude = plot.Longitude,
            Media = plot.Media.OrderBy(m => m.Id).ToList(),
            Status = plot.Status,
            Documents = plot.Documents.Select(d => new DocumentEditItem
            {
                Id = d.Id,
                Title = d.Title,
                DocType = d.DocType,
                DocumentNumber = d.DocumentNumber,
                IssueDate = d.IssueDate,
                IsVerified = d.IsVerified
            }).ToList()
        };

        if (!plot.AreLegalFactsLocked)
        {
            vm.Documents.Add(new DocumentEditItem
            {
                Title = string.Empty,
                DocType = "Витяг з ДЗК",
                DocumentNumber = string.Empty,
                IssueDate = DateTime.Today
            });
        }

        return vm;
    }

    public void ApplyTo(LandPlot plot)
    {
        if (!plot.AreLegalFactsLocked)
        {
            plot.SellerId = SellerId;
            plot.CadastralNumber = CadastralNumber.Trim();
            plot.AreaHectares = AreaHectares;
            plot.Purpose = Purpose.Trim();
            plot.OwnershipDocNumber = OwnershipDocNumber.Trim();
        }

        plot.Title = Title.Trim();
        plot.Location = Location.Trim();
        plot.PriceUsd = PriceUsd;
        plot.SellerBankIban = SellerBankIban.Trim().ToUpperInvariant();
        plot.Description = (Description ?? string.Empty).Trim();
        plot.UpdateSaleConditions(SaleConditions);
        plot.Latitude = Latitude;
        plot.Longitude = Longitude;

        if (plot.AreLegalFactsLocked)
        {
            return;
        }

        foreach (var item in Documents)
        {
            if (item.Id.HasValue && item.Id.Value > 0)
            {
                var doc = plot.Documents.FirstOrDefault(d => d.Id == item.Id.Value);
                if (doc != null && !doc.IsVerified)
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
                    IsVerified = false
                };
                var dummyBytes = System.Text.Encoding.UTF8.GetBytes($"{newDoc.DocType} №{newDoc.DocumentNumber} для ділянки {plot.CadastralNumber}");
                newDoc.AttachFile(string.Empty, dummyBytes, "application/pdf");
                plot.Documents.Add(newDoc);
            }
        }
    }
}

/// <summary>
/// Модель сторінки вилучення ділянки. Для кожного виду пов'язаних записів правило фіксоване:
/// фото й відео вилучаються разом із ділянкою (CASCADE), а документи, висновок, огляди й угоди блокують вилучення (RESTRICT).
/// </summary>
public class DeletePlotConfirmationViewModel
{
    public LandPlot Plot { get; set; } = null!;
    public int DocumentsCount { get; set; }
    public int ViewingsCount { get; set; }
    public int MediaCount { get; set; }
    public bool HasLegalConclusion { get; set; }
    public int DealsCount { get; set; }

    public bool CanBeDeleted => DealsCount == 0 && Plot.CanBeDeleted;

    /// <summary>
    /// Причини, з яких ділянку зараз вилучити не можна; порожній список означає, що вилучення дозволене.
    /// </summary>
    public IReadOnlyList<string> Blockers
    {
        get
        {
            var blockers = new List<string>();
            if (Plot.Status != PlotStatus.Draft)
            {
                blockers.Add($"ділянка вже не чернетка (статус «{Plot.StatusDisplayName}»): її бачив юрист, тож вона лишається в історії");
            }
            if (DocumentsCount > 0)
            {
                blockers.Add($"до ділянки додано документи ({DocumentsCount}); спершу вилучіть їх у формі редагування");
            }
            if (HasLegalConclusion)
            {
                blockers.Add("є юридичний висновок, він зберігається для аудиту");
            }
            if (ViewingsCount > 0)
            {
                blockers.Add($"є заявки на огляд ({ViewingsCount})");
            }
            if (DealsCount > 0)
            {
                blockers.Add($"є угоди купівлі-продажу ({DealsCount})");
            }
            return blockers;
        }
    }
}

/// <summary>
/// Модель форми редагування чернетки нотаріальної угоди з перерахунком комісії та рахунків.
/// </summary>
public class DealEditViewModel
{
    public int Id { get; set; }

    public Guid Version { get; set; }

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
