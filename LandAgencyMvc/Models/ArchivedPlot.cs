namespace LandAgencyMvc.Models;

/// <summary>
/// Архівна копія вилученої земельної ділянки. Записується в тій самій транзакції, що й вилучення,
/// тому видалення ніколи не лишається без сліду. Таблиця лише доповнюється: тригери бази забороняють
/// змінювати й видаляти її рядки. Зв'язків із LandPlots немає навмисно, бо ділянки вже не існує.
/// </summary>
public class ArchivedPlot
{
    public int Id { get; set; }

    public int OriginalPlotId { get; set; }

    public string CadastralNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public double AreaHectares { get; set; }

    public decimal PriceUsd { get; set; }

    public string SellerName { get; set; } = string.Empty;

    public PlotStatus StatusAtRemoval { get; set; }

    public int DocumentsCount { get; set; }

    public int ViewingsCount { get; set; }

    public int MediaCount { get; set; }

    /// <summary>
    /// Причина вилучення, яку обов'язково вказує той, хто вилучає.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public string RemovedByName { get; set; } = string.Empty;

    public UserRole RemovedByRole { get; set; }

    public DateTime RemovedAtUtc { get; set; }
}
