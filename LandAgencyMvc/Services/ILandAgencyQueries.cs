using System.Linq.Expressions;
using LandAgencyMvc.Models;

namespace LandAgencyMvc.Services;

public enum PeriodKind
{
    Day,
    Month,
    HalfYear,
    Custom
}

public enum PlotSort
{
    Date,
    Price,
    Area,
    PricePerSotka,
    Title,
    Location
}

public enum TurnoverSort
{
    Turnover,
    Deals,
    Area,
    Name
}

public enum AgentSort
{
    Turnover,
    Deals,
    Commission,
    Name
}

public readonly record struct Period(DateTime From, DateTime To, string Label)
{
    private static readonly string[] Months =
    [
        "січень", "лютий", "березень", "квітень", "травень", "червень",
        "липень", "серпень", "вересень", "жовтень", "листопад", "грудень"
    ];

    public static Period Containing(PeriodKind kind, DateOnly date)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        return kind switch
        {
            PeriodKind.Day => new(day, day.AddDays(1), day.ToString("dd.MM.yyyy")),
            PeriodKind.Month => new(new DateTime(day.Year, day.Month, 1),
                new DateTime(day.Year, day.Month, 1).AddMonths(1),
                $"{Months[day.Month - 1]} {day.Year}"),
            _ => day.Month <= 6
                ? new(new DateTime(day.Year, 1, 1), new DateTime(day.Year, 7, 1), $"I півріччя {day.Year}")
                : new(new DateTime(day.Year, 7, 1), new DateTime(day.Year + 1, 1, 1), $"II півріччя {day.Year}")
        };
    }

    public static Period Between(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var days = to.DayNumber - from.DayNumber + 1;
        return new(from.ToDateTime(TimeOnly.MinValue),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            $"з {from:dd.MM.yyyy} по {to:dd.MM.yyyy} ({days} дн.)");
    }
}

public sealed record QueryResult<T>(IReadOnlyList<T> Rows, string SqlQuery);

public sealed class PlotFilter
{
    public string? Region { get; set; }
    public string? Purpose { get; set; }
    public PlotStatus? Status { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public string? Participant { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PlotSort Sort { get; set; } = PlotSort.Date;
    public bool Descending { get; set; } = true;
}

public sealed record PlotRow(
    int Id,
    string CadastralNumber,
    string Title,
    string Location,
    double AreaHectares,
    double AreaSotky,
    string Purpose,
    decimal PriceUsd,
    decimal PricePerSotkaUsd,
    PlotStatus Status,
    string SellerName,
    string SellerPhone,
    string RealtorName,
    int DocumentsCount,
    string RiskLevel,
    DateTime CreatedAt);

public sealed record PurposeTurnoverRow(
    string Purpose,
    int DealsCount,
    double TotalAreaHectares,
    decimal TotalTurnoverUsd,
    decimal TotalCommissionUsd,
    decimal AveragePriceUsd,
    decimal MinPriceUsd,
    decimal MaxPriceUsd);

public sealed record AgentPerformanceRow(
    string AgentName,
    string RoleTitle,
    string Phone,
    int DealsCount,
    double TotalAreaHectares,
    decimal TotalTurnoverUsd,
    decimal TotalCommissionUsd);

public sealed class LandBankFilter
{
    public string? Region { get; set; }
    public string? RiskLevel { get; set; }
    public string? Purpose { get; set; }
    public PlotStatus? Status { get; set; }
    public bool OnlyVerifiedDocs { get; set; }
}

public sealed record LandBankRow(
    int PlotId,
    string CadastralNumber,
    string Title,
    string Location,
    string Purpose,
    double AreaHectares,
    decimal PriceUsd,
    string SellerName,
    string SellerPhone,
    string SellerIban,
    int DocumentsCount,
    string RiskLevel,
    bool IsLegalApproved,
    PlotStatus Status,
    DateTime RegistrationDate = default);

public sealed record TableStatsRow(string TableName, string ClassName, int RowCount, string Description);

public sealed record BenchmarkComparisonRow(
    string CadastralNumber,
    string Location,
    decimal ActualPriceUsd,
    decimal BenchmarkPriceUsd,
    decimal DeviationPercent,
    string EvaluationVerdict);

public sealed record LinqShowcaseResult(
    int TotalPlotsWithAllDocsVerified,
    int TotalBuyersWithAnyViewing,
    IReadOnlyList<string> AllDistinctPurposes,
    IReadOnlyList<string> MarketIntersectRegions,
    IReadOnlyList<string> AllMarketRegionsUnion,
    IReadOnlyList<string> BuyersWithoutDeals,
    IReadOnlyList<string> FlattenedDocTitles,
    string AggregatedCadastralRoll,
    IReadOnlyList<BenchmarkComparisonRow> ZippedBenchmarks,
    IReadOnlyList<PlotRow> MultiSortedPlots,
    IReadOnlyList<PlotRow> PaginatedPlots,
    IReadOnlyList<string> PlinqHighValuePlots);

public interface ILandAgencyQueries
{
    Task<List<TableStatsRow>> GetTableStatsAsync();

    Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter);

    Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(
        Period period, TurnoverSort sort = TurnoverSort.Turnover, bool descending = true);

    Task<QueryResult<AgentPerformanceRow>> AgentPerformanceAsync(
        Period period, AgentSort sort = AgentSort.Turnover, bool descending = true);

    Task<QueryResult<LandBankRow>> LandBankAuditAsync(LandBankFilter filter);

    Task<AppUser?> FindUserAsync(string email);

    bool CheckPassword(AppUser user, string password);

    Task<AppUser> AddUserAsync(string fullName, string email, string phone, UserRole role, string password, string? bankDetails);

    Task UpdateUserAsync(int id, string fullName, string phone, bool isActive, string? newPassword);

    Task<string> DeleteUserAsync(int id);

    Task<LinqShowcaseResult> GetLinqShowcaseAsync();
}
