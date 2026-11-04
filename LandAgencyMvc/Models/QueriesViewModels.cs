using LandAgencyMvc.Services;

namespace LandAgencyMvc.Models;

public class QueriesDashboardViewModel
{
    public List<TableStatsRow> TableStats { get; set; } = new();
    public int TotalTables => TableStats.Count;
    public int TotalRows => TableStats.Sum(t => t.RowCount);
}

public class PlotsFilterViewModel
{
    public PlotFilter Filter { get; set; } = new();
    public QueryResult<PlotRow> Result { get; set; } = new(new List<PlotRow>(), string.Empty);
}

public class PeriodTurnoverViewModel
{
    public PeriodKind Kind { get; set; } = PeriodKind.Month;
    public DateOnly Date { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public TurnoverSort Sort { get; set; } = TurnoverSort.Turnover;
    public bool Descending { get; set; } = true;
    public Period Period { get; set; }
    public QueryResult<PurposeTurnoverRow> ByPurpose { get; set; } = new(new List<PurposeTurnoverRow>(), string.Empty);
    public QueryResult<AgentPerformanceRow> ByAgent { get; set; } = new(new List<AgentPerformanceRow>(), string.Empty);

    public int TotalDeals => ByPurpose.Rows.Sum(r => r.DealsCount);
    public double TotalAreaHa => ByPurpose.Rows.Sum(r => r.TotalAreaHectares);
    public decimal TotalTurnoverUsd => ByPurpose.Rows.Sum(r => r.TotalTurnoverUsd);
    public decimal TotalCommissionUsd => ByPurpose.Rows.Sum(r => r.TotalCommissionUsd);
    public decimal AvgDealPriceUsd => TotalDeals > 0 ? Math.Round(TotalTurnoverUsd / TotalDeals, 2) : 0m;
}

public class LandBankViewModel
{
    public LandBankFilter Filter { get; set; } = new();
    public QueryResult<LandBankRow> Result { get; set; } = new(new List<LandBankRow>(), string.Empty);

    public int TotalPlots => Result.Rows.Count;
    public double TotalAreaHa => Math.Round(Result.Rows.Sum(r => r.AreaHectares), 2);
    public decimal TotalValueUsd => Result.Rows.Sum(r => r.PriceUsd);
    public int TotalCleanPlots => Result.Rows.Count(r => r.IsLegalApproved);
}

public class UsersManagementViewModel
{
    public List<AppUser> Users { get; set; } = new();
    public string? SearchEmail { get; set; }
    public AppUser? FoundUser { get; set; }
    public string? CheckPasswordEmail { get; set; }
    public bool? PasswordCheckResult { get; set; }
}

public class AddUserFormModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Client;
    public string Password { get; set; } = string.Empty;
    public string? BankDetails { get; set; }
}

public class UpdateUserFormModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? NewPassword { get; set; }
}
