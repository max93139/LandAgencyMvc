using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

public interface IPlotRemovalService
{
    /// <summary>
    /// Вилучає ділянку разом із документами, оглядами, юридичним висновком і медіа, а також прибирає її зі збігів
    /// заявок на підбір. Ділянку потрібно завантажити з Documents, ViewingRequests, LegalConclusion і Media.
    /// Перед видаленням у ту саму транзакцію записується архівна копія з причиною й автором вилучення,
    /// тому історія вилучених ділянок не втрачається. Усі записи змінюються одним збереженням.
    /// </summary>
    Task RemoveWithDependentsAsync(LandPlot plot, string reason, AppUser removedBy);
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

    public async Task RemoveWithDependentsAsync(LandPlot plot, string reason, AppUser removedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Потрібно вказати причину вилучення ділянки.", nameof(reason));
        }

        var sellerName = await _db.Users
            .Where(u => u.Id == plot.SellerId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? string.Empty;

        _db.ArchivedPlots.Add(new ArchivedPlot
        {
            OriginalPlotId = plot.Id,
            CadastralNumber = plot.CadastralNumber,
            Title = plot.Title,
            Location = plot.Location,
            AreaHectares = plot.AreaHectares,
            PriceUsd = plot.PriceUsd,
            SellerName = sellerName,
            StatusAtRemoval = plot.Status,
            DocumentsCount = plot.Documents.Count,
            ViewingsCount = plot.ViewingRequests.Count,
            MediaCount = plot.Media.Count,
            Reason = reason.Trim(),
            RemovedByName = removedBy.FullName,
            RemovedByRole = removedBy.Role,
            RemovedAtUtc = DateTime.UtcNow
        });

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
