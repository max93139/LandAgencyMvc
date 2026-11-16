using LandAgencyMvc.Models;

namespace LandAgencyMvc.Services;

public sealed record MediaSaveResult(PlotMedia? Media, string? Error);

public interface IPlotMediaStorage
{
    /// <summary>
    /// Перевіряє й зберігає фото або відео ділянки: завантажений файл або https-посилання на відео.
    /// </summary>
    Task<MediaSaveResult> SaveAsync(int plotId, MediaKind kind, IFormFile? file, string? videoUrl, string? caption);

    /// <summary>
    /// Вилучає з диска файл, завантажений для медіа. Зовнішні посилання ігноруються.
    /// </summary>
    void DeleteFile(PlotMedia media);
}

public sealed class PlotMediaStorage : IPlotMediaStorage
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxVideoBytes = 25 * 1024 * 1024;
    private const string UploadsSubfolder = "media";

    private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] VideoExtensions = { ".mp4", ".webm" };

    private readonly IWebHostEnvironment _env;

    public PlotMediaStorage(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<MediaSaveResult> SaveAsync(
        int plotId, MediaKind kind, IFormFile? file, string? videoUrl, string? caption)
    {
        var cleanCaption = (caption ?? string.Empty).Trim();

        if (kind == MediaKind.Video && !string.IsNullOrWhiteSpace(videoUrl))
        {
            if (!Uri.TryCreate(videoUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return new MediaSaveResult(null, "Посилання на відео має бути повною адресою, що починається з https://");
            }

            return new MediaSaveResult(NewMedia(plotId, kind, uri.ToString(), cleanCaption), null);
        }

        if (file == null || file.Length == 0)
        {
            return new MediaSaveResult(null, kind == MediaKind.Video
                ? "Оберіть відеофайл або вкажіть посилання на відео."
                : "Оберіть файл фотографії.");
        }

        var allowedExtensions = kind == MediaKind.Photo ? PhotoExtensions : VideoExtensions;
        var maxBytes = kind == MediaKind.Photo ? MaxPhotoBytes : MaxVideoBytes;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return new MediaSaveResult(null, $"Дозволені формати: {string.Join(", ", allowedExtensions)}.");
        }

        if (file.Length > maxBytes)
        {
            return new MediaSaveResult(null, $"Файл завеликий: максимум {maxBytes / (1024 * 1024)} МБ.");
        }

        var folder = GetMediaFolder();
        Directory.CreateDirectory(folder);

        var fileName = $"plot_{plotId}_{Guid.NewGuid():N}{extension}";
        await using (var stream = File.Create(Path.Combine(folder, fileName)))
        {
            await file.CopyToAsync(stream);
        }

        return new MediaSaveResult(NewMedia(plotId, kind, $"/uploads/{UploadsSubfolder}/{fileName}", cleanCaption), null);
    }

    public void DeleteFile(PlotMedia media)
    {
        if (media.IsExternalLink)
        {
            return;
        }

        var path = Path.Combine(GetMediaFolder(), Path.GetFileName(media.Url));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetMediaFolder() =>
        Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", UploadsSubfolder);

    private static PlotMedia NewMedia(int plotId, MediaKind kind, string url, string caption) => new()
    {
        LandPlotId = plotId,
        Kind = kind,
        Url = url,
        Caption = caption,
        UploadedAt = DateTime.Now
    };
}
