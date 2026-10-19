using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace LandAgencyMvc.Models;

/// <summary>
/// Правовстановлюючий або технічний документ земельної ділянки (витяг ДЗК, витяг ДРРП, КЕП-підпис)
/// із контролем цілісності вмісту за алгоритмом SHA-256.
/// </summary>
public class PlotDocument
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Тип документа (ДЗК, ДРРП, Акт встановлення меж, Договір тощо).
    /// </summary>
    [Required]
    public string DocType { get; set; } = string.Empty;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Відносний шлях до збереженого файлу документа у веб-сховищі (/uploads/...).
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Контрольний криптографічний хеш вмісту файлу за стандартом SHA-256 (64 hex-символи).
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Розмір завантаженого файлу в байтах.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// MIME-тип вмісту завантаженого файлу (application/pdf, image/jpeg тощо).
    /// </summary>
    public string MimeType { get; set; } = "application/pdf";

    /// <summary>
    /// Ознака наявності кваліфікованого електронного підпису (КЕП/ЕЦП).
    /// </summary>
    public bool IsSignedByKep { get; set; }

    /// <summary>
    /// Криптографічний відбиток електронного підпису КЕП (SHA-256).
    /// </summary>
    public string KepSignatureHash { get; set; } = string.Empty;

    public string? SignerFullName { get; set; }

    public string? SignerTaxId { get; set; }

    public DateTime? SignedAt { get; set; }

    /// <summary>
    /// Статус юридичної верифікації документа адвокатом/юристом агентства.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Прив'язує URL-шлях збереженого файлу та записує параметри цілісності.
    /// </summary>
    public void AttachFile(string fileUrl, byte[]? fileBytes = null, string? mimeType = null)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            FileUrl = fileUrl.Trim();
        }

        if (fileBytes != null && fileBytes.Length > 0)
        {
            FileSizeBytes = fileBytes.LongLength;
            var hashBytes = SHA256.HashData(fileBytes);
            FileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            MimeType = mimeType.Trim();
        }
    }

    /// <summary>
    /// Накладає цифровий підпис КЕП на фактичний двійковий вміст файлу та метадані підписанта.
    /// </summary>
    public void ApplyKepSignature(string signerName, string taxId, byte[]? fileBytes = null)
    {
        IsSignedByKep = true;
        SignerFullName = (signerName ?? string.Empty).Trim();
        SignerTaxId = (taxId ?? string.Empty).Trim();
        SignedAt = DateTime.UtcNow;

        string targetHash = FileHash;
        if (fileBytes != null && fileBytes.Length > 0)
        {
            var hashBytes = SHA256.HashData(fileBytes);
            targetHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            FileHash = targetHash;
            FileSizeBytes = fileBytes.LongLength;
        }

        var signaturePayload = $"{targetHash}|{DocumentNumber}|{SignerFullName}|{SignerTaxId}|{SignedAt:yyyy-MM-ddTHH:mm:ssZ}";
        var signatureBytes = SHA256.HashData(Encoding.UTF8.GetBytes(signaturePayload));
        KepSignatureHash = $"КЕП-SHA256-{Convert.ToHexString(signatureBytes)[..24]}";
    }

    /// <summary>
    /// Перевіряє цілісність збереженого файлу шляхом зіставлення його поточного SHA-256 хешу з еталонним.
    /// </summary>
    public bool VerifyFileIntegrity(byte[] currentFileBytes)
    {
        if (string.IsNullOrWhiteSpace(FileHash) || currentFileBytes == null || currentFileBytes.Length == 0)
        {
            return false;
        }

        var currentHash = Convert.ToHexString(SHA256.HashData(currentFileBytes)).ToLowerInvariant();
        return string.Equals(FileHash, currentHash, StringComparison.OrdinalIgnoreCase);
    }
}
