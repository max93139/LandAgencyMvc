namespace LandAgencyMvc.Services;

/// <summary>
/// Сервіс автоматичного підбору земельних ділянок під активні клієнтські заявки.
/// Автоматично зіставляє критерії при створенні нової заявки покупця
/// та сповіщає/оновлює результати при верифікації нової ділянки юристом.
/// </summary>
public interface IAutoMatchingService
{
    Task<int> MatchRequestAsync(int requestId);
    Task<int> MatchNewOrUpdatedPlotAsync(int plotId);
    Task<int> MatchAllActiveRequestsAsync();
}
