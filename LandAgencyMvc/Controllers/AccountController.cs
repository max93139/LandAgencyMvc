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
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
        var normalizedPassword = (password ?? string.Empty).Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail);

        if (user == null || !user.Authenticate(normalizedPassword))
        {
            TempData["LoginError"] = "Невірна електронна пошта або пароль.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
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

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return user.Role switch
        {
            UserRole.Realtor => RedirectToAction("Requests", "Realtor"),
            UserRole.Lawyer => RedirectToAction("Queue", "Lawyer"),
            _ => RedirectToAction("Cabinet", "Client")
        };
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string fullName, string email, string phone, string taxId, string? bankDetails, string password)
    {
        var cleanEmail = (email ?? string.Empty).Trim();
        var lowerEmail = cleanEmail.ToLower();

        if (string.IsNullOrWhiteSpace(cleanEmail) || string.IsNullOrWhiteSpace(password))
        {
            TempData["RegisterError"] = "Заповніть обов'язкові поля електронної пошти та пароля.";
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
            FullName = (fullName ?? string.Empty).Trim(),
            Email = cleanEmail,
            Phone = (phone ?? string.Empty).Trim(),
            TaxId = (taxId ?? string.Empty).Trim(),
            BankDetails = (bankDetails ?? string.Empty).Trim(),
            Role = UserRole.Client
        };
        user.SetPassword(password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
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

        TempData["CabinetMessage"] = "Обліковий запис клієнта успішно створено. Ласкаво просимо до LandAgency UA!";
        return RedirectToAction("Cabinet", "Client");
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
