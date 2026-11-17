using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

public interface IPlotRemovalService
{
    /// <summary>
    /// Вилучає чернетку ділянки (див. <see cref="LandPlot.CanBeDeleted"/>). Фото й відео ділянки вилучаються разом із нею
    /// каскадно, ділянка прибирається зі збігів заявок на підбір, а її копія з причиною й автором записується в архів.
    /// Усе відбувається одним збереженням, тобто в одній транзакції. Ділянку потрібно завантажити з Documents,
    /// ViewingRequests, LegalConclusion і Media.
    /// </summary>
    Task RemoveAsync(LandPlot plot, string reason, AppUser removedBy);
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

    public async Task RemoveAsync(LandPlot plot, string reason, AppUser removedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Потрібно вказати причину вилучення ділянки.", nameof(reason));
        }

        if (!plot.CanBeDeleted)
        {
            throw new InvalidOperationException("Вилучити можна лише чернетку ділянки без документів, висновку й оглядів.");
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

        var matchedRequests = await _db.SelectionRequests.Where(r => r.MatchedPlotIds != string.Empty).ToListAsync();
        foreach (var request in matchedRequests)
        {
            request.RemoveMatch(plot.Id);
        }

        // Рядки PlotMedia вилучає зв'язок ON DELETE CASCADE; файли фото й відео прибираються після збереження.
        var mediaFiles = plot.Media.ToList();
        _db.LandPlots.Remove(plot);
        await _db.SaveChangesAsync();

        mediaFiles.ForEach(_mediaStorage.DeleteFile);
    }
}
