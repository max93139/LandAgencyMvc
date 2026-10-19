using LandAgencyMvc.Models;
using Xunit;

namespace LandAgencyMvc.Tests;

public class PasswordHashingTests
{
    [Fact]
    public void SetPassword_CreatesSecurePbkdf2Hash()
    {
        var user = new AppUser { FullName = "Іван Тестовий", Email = "ivan@test.ua" };
        user.SetPassword("SecureP@ssw0rd2026");

        Assert.NotNull(user.Password);
        Assert.NotEqual("SecureP@ssw0rd2026", user.Password);
        Assert.True(user.Password.Length > 30);
    }

    [Fact]
    public void Authenticate_ReturnsTrue_ForValidPassword()
    {
        var user = new AppUser { FullName = "Марія Тестова", Email = "maria@test.ua" };
        user.SetPassword("Secret123!");

        bool result = user.Authenticate("Secret123!");

        Assert.True(result);
    }

    [Fact]
    public void Authenticate_ReturnsFalse_ForInvalidPassword()
    {
        var user = new AppUser { FullName = "Марія Тестова", Email = "maria@test.ua" };
        user.SetPassword("Secret123!");

        bool result = user.Authenticate("WrongPassword");

        Assert.False(result);
    }

    [Fact]
    public void Authenticate_AutomaticallyUpgrades_LegacyPlaintextPassword()
    {
        var user = new AppUser
        {
            FullName = "Старий Користувач",
            Email = "legacy@test.ua",
            Password = "realtor123"
        };

        bool result = user.Authenticate("realtor123");

        Assert.True(result);
        Assert.NotEqual("realtor123", user.Password);
        Assert.True(user.Password.Length > 30);
        Assert.True(user.Authenticate("realtor123"));
    }

    [Fact]
    public void ChangePassword_Succeeds_WhenCurrentPasswordIsCorrect()
    {
        var user = new AppUser { FullName = "Петро", Email = "petro@test.ua" };
        user.SetPassword("OldPassword!1");

        bool changed = user.ChangePassword("OldPassword!1", "NewPassword!2");

        Assert.True(changed);
        Assert.False(user.Authenticate("OldPassword!1"));
        Assert.True(user.Authenticate("NewPassword!2"));
    }

    [Fact]
    public void ChangePassword_Fails_WhenCurrentPasswordIsIncorrect()
    {
        var user = new AppUser { FullName = "Петро", Email = "petro@test.ua" };
        user.SetPassword("OldPassword!1");

        bool changed = user.ChangePassword("WrongCurrent", "NewPassword!2");

        Assert.False(changed);
        Assert.True(user.Authenticate("OldPassword!1"));
    }
}
