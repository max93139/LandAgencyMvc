using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace LandAgencyMvc.Models;

/// <summary>
/// Правовстановлюючий або технічний документ земельної ділянки (витяг ДЗК, витяг ДРРП, КЕП-підпис).
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
    /// Відносний або абсолютний шлях до завантаженого файлу документа у сховищі.
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Ознака наявності кваліфікованого електронного підпису (КЕП/ЕЦП).
    /// </summary>
    public bool IsSignedByKep { get; set; }

    /// <summary>
    /// Криптографічний хеш електронного підпису КЕП (SHA-256).
    /// </summary>
    public string KepSignatureHash { get; set; } = string.Empty;

    /// <summary>
    /// Статус юридичної верифікації документа адвокатом/юристом агентства.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Прив'язує URL-шлях збереженого файлу до запису документа.
    /// </summary>
    public void AttachFile(string fileUrl)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            FileUrl = fileUrl.Trim();
        }
    }

    /// <summary>
    /// Накладає імітацію КЕП/ЕЦП: формує payload (номер, ПІБ, РНОКПП, дата UTC)
    /// та обчислює контрольний хеш за стандартом SHA-256.
    /// </summary>
    /// <param name="signerName">ПІБ підписанта.</param>
    /// <param name="taxId">Ідентифікаційний код РНОКПП.</param>
    public void ApplyKepSignature(string signerName, string taxId)
    {
        IsSignedByKep = true;
        var payload = $"{DocumentNumber}|{signerName}|{taxId}|{DateTime.UtcNow:yyyyMMdd}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        KepSignatureHash = $"КЕП-{Convert.ToHexString(hashBytes)[..16]}";
    }
}

