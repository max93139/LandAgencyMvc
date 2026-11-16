using System.Security.Claims;
using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// Контролер автентифікації та реєстрації користувачів.
/// Забезпечує безпечний вхід на базі Claims-Cookie Authentication, реєстрацію клієнтів
/// та захист від несанкціонованого отримання службових ролей.
/// </summary>
public class AccountController : Controller
{
    private readonly LandAgencyContext _db;

    public AccountController(LandAgencyContext context)
    {
        _db = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToWorkspace(GetSignedInRole());
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>
    /// UC-16 «Службова авторизація співробітника»: окрема сторінка входу для рієлторів і юристів.
    /// </summary>
    [HttpGet]
    public IActionResult StaffLogin(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToWorkspace(GetSignedInRole());
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>
    /// UC-07 «Реєстрація та авторизація клієнта»: вхід для покупців і продавців.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Login(string email, string password, string? returnUrl = null) =>
        SignInAsync(email, password, returnUrl, staffEntrance: false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> StaffLogin(string email, string password, string? returnUrl = null) =>
        SignInAsync(email, password, returnUrl, staffEntrance: true);

    /// <summary>
    /// Перевіряє пароль і спрямовує користувача до робочого місця. Клієнтський і службовий входи розділені:
    /// обліковий запис іншого типу отримує підказку, куди заходити, але лише після успішної перевірки пароля.
    /// </summary>
    private async Task<IActionResult> SignInAsync(string email, string password, string? returnUrl, bool staffEntrance)
    {
        var loginAction = staffEntrance ? nameof(StaffLogin) : nameof(Login);
        var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
        var normalizedPassword = (password ?? string.Empty).Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail);

        if (user == null || !user.Authenticate(normalizedPassword))
        {
            TempData["LoginError"] = "Невірна електронна пошта або пароль.";
            return RedirectToAction(loginAction, new { returnUrl });
        }

        if (!user.IsActive)
        {
            TempData["LoginError"] = "Обліковий запис заблоковано.";
            return RedirectToAction(loginAction, new { returnUrl });
        }

        bool isStaff = user.Role is UserRole.Realtor or UserRole.Lawyer;
        if (isStaff != staffEntrance)
        {
            TempData["LoginError"] = staffEntrance
                ? "Це службовий вхід. Покупці та продавці входять через сторінку входу для клієнтів."
                : "Співробітники агентства входять через службовий вхід.";
            return RedirectToAction(isStaff ? nameof(StaffLogin) : nameof(Login), new { returnUrl });
        }

        await _db.SaveChangesAsync();
        await SignInUserAsync(user);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToWorkspace(user.Role);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToWorkspace(GetSignedInRole());
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string fullName,
        string email,
        string phone,
        string password,
        UserRole accountType = UserRole.Buyer,
        string? taxId = null,
        string? bankDetails = null)
    {
        var cleanFullName = (fullName ?? string.Empty).Trim();
        var cleanEmail = (email ?? string.Empty).Trim();
        var lowerEmail = cleanEmail.ToLower();

        if (string.IsNullOrWhiteSpace(cleanFullName) || string.IsNullOrWhiteSpace(cleanEmail) || string.IsNullOrWhiteSpace(password))
        {
            TempData["RegisterError"] = "Заповніть обов'язкові поля ПІБ, електронної пошти та пароля.";
            return RedirectToAction(nameof(Register));
        }

        if (!AppRoles.ClientRoles.Contains(accountType))
        {
            TempData["RegisterError"] = "Зареєструватися можна лише як Покупець або Продавець. Службові ролі створює агентство.";
            return RedirectToAction(nameof(Register));
        }

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == lowerEmail);
        if (existing != null)
        {
            TempData["RegisterError"] = "Користувач із такою електронною поштою вже зареєстрований.";
            return RedirectToAction(nameof(Register));
        }

        var user = new AppUser
        {
            FullName = cleanFullName,
            Email = cleanEmail,
            Phone = (phone ?? string.Empty).Trim(),
            TaxId = (taxId ?? string.Empty).Trim(),
            BankDetails = (bankDetails ?? string.Empty).Trim(),
            Role = accountType
        };
        user.SetPassword(password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInUserAsync(user);

        TempData["CabinetMessage"] = "Обліковий запис клієнта успішно створено. Ласкаво просимо до LandAgency UA!";
        return RedirectToAction("Cabinet", "Client");
    }

    /// <summary>
    /// Робоче місце, на яке потрапляє користувач після входу: рієлтор, юрист або особистий кабінет клієнта.
    /// </summary>
    private IActionResult RedirectToWorkspace(UserRole role) => role switch
    {
        UserRole.Realtor => RedirectToAction("Requests", "Realtor"),
        UserRole.Lawyer => RedirectToAction("Queue", "Lawyer"),
        _ => RedirectToAction("Cabinet", "Client")
    };

    private UserRole GetSignedInRole() =>
        Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Buyer;

    private async Task SignInUserAsync(AppUser user)
    {
        var credential = user.LicenseNumber ?? user.CertificateNumber ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("StaffCredential", credential)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("LandAgencyUserId");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
