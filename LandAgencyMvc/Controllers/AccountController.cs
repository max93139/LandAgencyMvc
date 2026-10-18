using LandAgencyMvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

public class AccountController : Controller
{
    private readonly LandAgencyContext _db;

    public AccountController(LandAgencyContext context)
    {
        _db = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLower();
        var normalizedPassword = (password ?? string.Empty).Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail);

        if (user == null || !user.Authenticate(normalizedPassword))
        {
            TempData["LoginError"] = "Невірна електронна пошта або пароль.";
            return RedirectToAction(nameof(Login));
        }

        Response.Cookies.Append("LandAgencyUserId", user.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return user.Role switch
        {
            UserRole.Realtor => RedirectToAction("Requests", "Realtor", new { userId = user.Id }),
            UserRole.Lawyer => RedirectToAction("Queue", "Lawyer", new { userId = user.Id }),
            _ => RedirectToAction("Catalog", "Client", new { userId = user.Id })
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

        var role = UserRole.Client;
        if (lowerEmail.StartsWith("realtor@") || lowerEmail.Contains("@realtor."))
        {
            role = UserRole.Realtor;
        }
        else if (lowerEmail.StartsWith("lawyer@") || lowerEmail.Contains("@lawyer."))
        {
            role = UserRole.Lawyer;
        }

        var user = new AppUser
        {
            FullName = (fullName ?? string.Empty).Trim(),
            Email = cleanEmail,
            Phone = (phone ?? string.Empty).Trim(),
            TaxId = (taxId ?? string.Empty).Trim(),
            BankDetails = (bankDetails ?? string.Empty).Trim(),
            Password = password.Trim(),
            Role = role,
            LicenseNumber = role == UserRole.Realtor ? "RL-402" : null,
            CertificateNumber = role == UserRole.Lawyer ? "LW-881" : null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        Response.Cookies.Append("LandAgencyUserId", user.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return user.Role switch
        {
            UserRole.Realtor => RedirectToAction("Requests", "Realtor", new { userId = user.Id }),
            UserRole.Lawyer => RedirectToAction("Queue", "Lawyer", new { userId = user.Id }),
            _ => RedirectToAction("Catalog", "Client", new { userId = user.Id })
        };
    }

    [HttpGet]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("LandAgencyUserId", new CookieOptions { Path = "/" });
        return RedirectToAction(nameof(Login));
    }
}
