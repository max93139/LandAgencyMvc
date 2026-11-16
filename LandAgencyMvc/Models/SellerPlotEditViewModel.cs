using System.ComponentModel.DataAnnotations;

namespace LandAgencyMvc.Models;

/// <summary>
/// Поля ділянки, які продавець може змінювати сам. Кадастровий номер, площа й призначення належать до юридичних
/// фактів, що перевіряє юрист, тому на формі вони лише показуються.
/// </summary>
public class SellerPlotEditViewModel
{
    public int Id { get; set; }

    public Guid Version { get; set; }

    public string CadastralNumber { get; set; } = string.Empty;

    public double AreaHectares { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string StatusDisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть назву оголошення")]
    [Display(Name = "Назва оголошення")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть адресу розташування")]
    [Display(Name = "Адреса розташування")]
    public string Location { get; set; } = string.Empty;

    [Range(100, 100000000, ErrorMessage = "Ціна має бути не менше $100")]
    [Display(Name = "Ціна (USD)")]
    public decimal PriceUsd { get; set; }

    [Required(ErrorMessage = "Вкажіть банківські реквізити IBAN")]
    [RegularExpression(@"^UA\d{27}$", ErrorMessage = "IBAN має починатися з літер UA та містити 27 цифр")]
    [Display(Name = "IBAN рахунку продавця")]
    public string SellerBankIban { get; set; } = string.Empty;

    [Display(Name = "Опис ділянки та комунікацій")]
    public string Description { get; set; } = string.Empty;

    [StringLength(LandPlot.MaxSaleConditionsLength, ErrorMessage = "Умови продажу: не більше 500 символів")]
    [Display(Name = "Умови продажу")]
    public string SaleConditions { get; set; } = string.Empty;

    public static SellerPlotEditViewModel From(LandPlot plot)
    {
        var vm = new SellerPlotEditViewModel
        {
            Id = plot.Id,
            Version = plot.Version,
            Title = plot.Title,
            Location = plot.Location,
            PriceUsd = plot.PriceUsd,
            SellerBankIban = plot.SellerBankIban,
            Description = plot.Description,
            SaleConditions = plot.SaleConditions
        };
        vm.ShowReadOnlyFieldsOf(plot);
        return vm;
    }

    public void ShowReadOnlyFieldsOf(LandPlot plot)
    {
        CadastralNumber = plot.CadastralNumber;
        AreaHectares = plot.AreaHectares;
        Purpose = plot.Purpose;
        StatusDisplayName = plot.StatusDisplayName;
    }

    public void ApplyTo(LandPlot plot)
    {
        plot.Title = Title.Trim();
        plot.Location = Location.Trim();
        plot.UpdatePrice(PriceUsd);
        plot.SellerBankIban = SellerBankIban.Trim().ToUpperInvariant();
        plot.Description = (Description ?? string.Empty).Trim();
        plot.UpdateSaleConditions(SaleConditions);
    }
}
