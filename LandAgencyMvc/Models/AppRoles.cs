namespace LandAgencyMvc.Models;

/// <summary>
/// Назви ролей для атрибутів <c>[Authorize(Roles = ...)]</c> та перевірок <c>User.IsInRole(...)</c>.
/// Значення збігаються з іменами елементів <see cref="UserRole"/>, які записуються в claim ролі під час входу.
/// </summary>
public static class AppRoles
{
    public const string Buyer = nameof(UserRole.Buyer);

    public const string Seller = nameof(UserRole.Seller);

    public const string Realtor = nameof(UserRole.Realtor);

    public const string Lawyer = nameof(UserRole.Lawyer);

    /// <summary>
    /// Будь-який клієнтський обліковий запис (Покупець або Продавець).
    /// Лише для атрибута <c>[Authorize(Roles = ...)]</c>, де кома означає «або».
    /// </summary>
    public const string AnyClient = Buyer + "," + Seller;

    /// <summary>
    /// Ролі клієнтів для запитів до бази даних: <c>ClientRoles.Contains(u.Role)</c> перетворюється на SQL <c>IN</c>.
    /// </summary>
    public static readonly UserRole[] ClientRoles = { UserRole.Buyer, UserRole.Seller };
}
