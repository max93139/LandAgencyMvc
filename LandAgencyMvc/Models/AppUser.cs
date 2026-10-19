using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LandAgencyMvc.Models;

/// <summary>
/// Обліковий запис користувача системи (клієнт платформи, рієлтор або юрист).
/// </summary>
public class AppUser
{
    private static readonly PasswordHasher<AppUser> _hasher = new();

    public int Id { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Криптографічний хеш пароля, згенерований за стандартом PBKDF2.
    /// </summary>
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

    /// <summary>
    /// Встановлює новий криптографічний хеш пароля користувача.
    /// </summary>
    /// <param name="plainPassword">Відкритий текст пароля.</param>
    public void SetPassword(string plainPassword)
    {
        Password = _hasher.HashPassword(this, (plainPassword ?? string.Empty).Trim());
    }

    /// <summary>
    /// Перевіряє автентичність введеного пароля з автоматичним перехешуванням за потреби
    /// та зворотною сумісністю для первинно засіяних облікових записів.
    /// </summary>
    /// <param name="inputPassword">Введений користувачем пароль.</param>
    /// <returns>True, якщо пароль валідний; інакше False.</returns>
    public bool Authenticate(string inputPassword)
    {
        if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(inputPassword))
        {
            return false;
        }

        var trimmed = inputPassword.Trim();
        try
        {
            var result = _hasher.VerifyHashedPassword(this, Password, trimmed);
            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    SetPassword(trimmed);
                }
                return true;
            }
        }
        catch
        {
            // У разі якщо в базі міститься успадкований незахешований пароль
        }

        if (Password == trimmed)
        {
            SetPassword(trimmed);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Змінює поточний пароль користувача на новий після успішної автентифікації.
    /// </summary>
    public bool ChangePassword(string currentPassword, string newPassword)
    {
        if (!Authenticate(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        SetPassword(newPassword);
        return true;
    }
}
