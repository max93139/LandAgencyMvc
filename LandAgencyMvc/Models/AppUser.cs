using System.ComponentModel.DataAnnotations;

namespace LandAgencyMvc.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Client;

    public string BankDetails { get; set; } = string.Empty;

    public string? OwnershipDoc { get; set; }

    public double MaxBudget { get; set; }

    public string? TargetPurpose { get; set; }

    public string? SpecializationOrLicense { get; set; }

    public string? LicenseNumber { get; set; }

    public double CommissionRate { get; set; } = 3.0;

    public string? WorkPhone { get; set; }

    public string? CertificateNumber { get; set; }

    public string? Specialization { get; set; }

    public ICollection<LandPlot> OwnedPlots { get; set; } = new List<LandPlot>();

    public ICollection<ViewingRequest> ViewingRequests { get; set; } = new List<ViewingRequest>();

    public bool Authenticate(string inputPassword)
    {
        return !string.IsNullOrEmpty(Password) && Password == (inputPassword ?? string.Empty).Trim();
    }

    public bool ChangePassword(string currentPassword, string newPassword)
    {
        if (!Authenticate(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        Password = newPassword.Trim();
        return true;
    }
}
