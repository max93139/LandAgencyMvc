using System.ComponentModel.DataAnnotations.Schema;

namespace LandAgencyMvc.Models;

public enum MediaKind
{
    Photo,
    Video
}

/// <summary>
/// Фото або відео земельної ділянки (UC-12). Належить ділянці й вилучається разом із нею.
/// </summary>
public class PlotMedia
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    public MediaKind Kind { get; set; }

    /// <summary>
    /// Шлях до завантаженого файлу в каталозі uploads/media або зовнішнє https-посилання на відео.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public bool IsExternalLink => Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
