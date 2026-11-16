using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

public interface IPlotRemovalService
{
    /// <summary>
    /// Вилучає ділянку разом із документами, оглядами, юридичним висновком і медіа, а також прибирає її зі збігів
    /// заявок на підбір. Ділянку потрібно завантажити з Documents, ViewingRequests, LegalConclusion і Media.
    /// Усі записи видаляються одним збереженням, тобто в одній транзакції.
    /// </summary>
    Task RemoveWithDependentsAsync(LandPlot plot);
}

public sealed class PlotRemovalService : IPlotRemovalService
{
    private readonly LandAgencyContext _db;
    private readonly IPlotMediaStorage _mediaStorage;

    public PlotRemovalService(LandAgencyContext db, IPlotMediaStorage mediaStorage)
    {
        _db = db;
        _mediaStorage = mediaStorage;
    }

    public async Task RemoveWithDependentsAsync(LandPlot plot)
    {
        _db.PlotDocuments.RemoveRange(plot.Documents);
        _db.ViewingRequests.RemoveRange(plot.ViewingRequests);
        if (plot.LegalConclusion != null)
        {
            _db.LegalConclusions.Remove(plot.LegalConclusion);
        }

        var matchedRequests = await _db.SelectionRequests.Where(r => r.MatchedPlotIds != string.Empty).ToListAsync();
        foreach (var request in matchedRequests)
        {
            request.RemoveMatch(plot.Id);
        }

        var mediaFiles = plot.Media.ToList();
        _db.LandPlots.Remove(plot);
        await _db.SaveChangesAsync();

        mediaFiles.ForEach(_mediaStorage.DeleteFile);
    }
}
