using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace LandAgencyMvc.Models;

public class PlotDocument
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string DocType { get; set; } = string.Empty;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; } = DateTime.Today;

    public string FileUrl { get; set; } = string.Empty;

    public bool IsSignedByKep { get; set; }

    public string KepSignatureHash { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public void AttachFile(string fileUrl)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            FileUrl = fileUrl.Trim();
        }
    }

    public void ApplyKepSignature(string signerName, string taxId)
    {
        IsSignedByKep = true;
        var payload = $"{DocumentNumber}|{signerName}|{taxId}|{DateTime.UtcNow:yyyyMMdd}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        KepSignatureHash = $"КЕП-{Convert.ToHexString(hashBytes)[..16]}";
    }
}
