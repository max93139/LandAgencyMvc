namespace LandAgencyMvc.Models;

/// <summary>
/// Архів ділянок рієлтора: продані ділянки (їх угоди завершено, тому вони вже не в роботі)
/// і вилучені з реєстру ділянки з причиною вилучення.
/// </summary>
public class PlotArchiveViewModel
{
    public List<SaleDeal> SoldDeals { get; set; } = new();

    public List<ArchivedPlot> RemovedPlots { get; set; } = new();
}
