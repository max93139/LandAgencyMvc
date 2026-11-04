using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LandAgencyMvc.Tests;

public class Lab3LinqTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LandAgencyContext _context;
    private readonly LandAgencyQueries _queries;

    public Lab3LinqTests()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LandAgencyContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LandAgencyContext(options);
        _context.Database.EnsureCreated();
        _context.InitializeDatabase();

        _queries = new LandAgencyQueries(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Scenario1_All8Tables_HaveAtLeast10Rows()
    {
        var stats = await _queries.GetTableStatsAsync();

        Assert.Equal(8, stats.Count);
        foreach (var table in stats)
        {
            Assert.True(table.RowCount >= 10,
                $"Таблиця {table.TableName} містить {table.RowCount} рядків, очікувалося ≥ 10.");
        }

        var usersCount = await _context.Users.CountAsync();
        var plotsCount = await _context.LandPlots.CountAsync();
        var docsCount = await _context.PlotDocuments.CountAsync();
        var conclusionsCount = await _context.LegalConclusions.CountAsync();
        var viewingsCount = await _context.ViewingRequests.CountAsync();
        var selectionsCount = await _context.SelectionRequests.CountAsync();
        var dealsCount = await _context.SaleDeals.CountAsync();
        var paymentsCount = await _context.Payments.CountAsync();

        Assert.True(usersCount >= 10);
        Assert.True(plotsCount >= 10);
        Assert.True(docsCount >= 10);
        Assert.True(conclusionsCount >= 10);
        Assert.True(viewingsCount >= 10);
        Assert.True(selectionsCount >= 10);
        Assert.True(dealsCount >= 10);
        Assert.True(paymentsCount >= 10);
    }

    [Fact]
    public async Task Scenario2_IdempotentSeed_DoesNotDuplicateRows()
    {
        int initialUsers = await _context.Users.CountAsync();
        int initialPlots = await _context.LandPlots.CountAsync();

        _context.InitializeDatabase();

        int afterUsers = await _context.Users.CountAsync();
        int afterPlots = await _context.LandPlots.CountAsync();

        Assert.Equal(initialUsers, afterUsers);
        Assert.Equal(initialPlots, afterPlots);
    }

    [Fact]
    public async Task Scenario3_Query1_FilterPlots_ByCompoundCriteria()
    {
        var filter = new PlotFilter
        {
            Region = "Київська",
            Purpose = "02.01",
            MaxPrice = 30000m
        };

        var result = await _queries.FilterPlotsAsync(filter);

        Assert.NotEmpty(result.Rows);
        Assert.NotEmpty(result.SqlQuery);

        foreach (var plot in result.Rows)
        {
            Assert.Contains("Київська", plot.Location);
            Assert.Contains("02.01", plot.Purpose);
            Assert.True(plot.PriceUsd <= 30000m);
        }
    }

    [Fact]
    public async Task Scenario4_Query1_Sorting_AscendingAndDescending()
    {
        var filterAsc = new PlotFilter { Sort = PlotSort.Price, Descending = false };
        var resultAsc = await _queries.FilterPlotsAsync(filterAsc);

        for (int i = 0; i < resultAsc.Rows.Count - 1; i++)
        {
            Assert.True(resultAsc.Rows[i].PriceUsd <= resultAsc.Rows[i + 1].PriceUsd);
        }

        var filterDesc = new PlotFilter { Sort = PlotSort.Price, Descending = true };
        var resultDesc = await _queries.FilterPlotsAsync(filterDesc);

        for (int i = 0; i < resultDesc.Rows.Count - 1; i++)
        {
            Assert.True(resultDesc.Rows[i].PriceUsd >= resultDesc.Rows[i + 1].PriceUsd);
        }
    }

    [Fact]
    public async Task Scenario5_Query2_PeriodTurnover_DayMonthHalfYearAndCustom()
    {
        var dayDate = new DateOnly(2026, 10, 15);
        var dayPeriod = Period.Containing(PeriodKind.Day, dayDate);
        var dayResult = await _queries.TurnoverByPurposeAsync(dayPeriod);

        Assert.NotEmpty(dayResult.Rows);
        var dayDeals = dayResult.Rows.Sum(r => r.DealsCount);
        Assert.True(dayDeals >= 1);

        var monthPeriod = Period.Containing(PeriodKind.Month, new DateOnly(2026, 9, 1));
        var monthResult = await _queries.TurnoverByPurposeAsync(monthPeriod);
        Assert.NotEmpty(monthResult.Rows);

        var h1Period = Period.Containing(PeriodKind.HalfYear, new DateOnly(2026, 3, 1));
        Assert.Equal("I півріччя 2026", h1Period.Label);
        var h1Result = await _queries.TurnoverByPurposeAsync(h1Period);
        Assert.NotEmpty(h1Result.Rows);

        var h2Period = Period.Containing(PeriodKind.HalfYear, new DateOnly(2025, 9, 1));
        Assert.Equal("II півріччя 2025", h2Period.Label);
        var h2Result = await _queries.TurnoverByPurposeAsync(h2Period);
        Assert.NotEmpty(h2Result.Rows);

        var customPeriod = Period.Between(new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 31));
        var customResult = await _queries.TurnoverByPurposeAsync(customPeriod);
        Assert.NotEmpty(customResult.Rows);

        foreach (var row in customResult.Rows)
        {
            Assert.True(row.DealsCount > 0);
            Assert.True(row.TotalTurnoverUsd > 0);
            Assert.True(row.TotalCommissionUsd > 0);
            Assert.True(row.AveragePriceUsd >= row.MinPriceUsd && row.AveragePriceUsd <= row.MaxPriceUsd);
        }
    }

    [Fact]
    public void Scenario6_PeriodBoundaries_HalfYears_AndReversedCustomDates()
    {
        var jun30 = new DateOnly(2026, 6, 30);
        var jul01 = new DateOnly(2026, 7, 1);

        var pJun = Period.Containing(PeriodKind.HalfYear, jun30);
        var pJul = Period.Containing(PeriodKind.HalfYear, jul01);

        Assert.Equal("I півріччя 2026", pJun.Label);
        Assert.Equal("II півріччя 2026", pJul.Label);

        var forward = Period.Between(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var reversed = Period.Between(new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1));

        Assert.Equal(forward.From, reversed.From);
        Assert.Equal(forward.To, reversed.To);
    }

    [Fact]
    public async Task Scenario7_Query3_AgentPerformance_RealtorsAndLawyers()
    {
        var period = Period.Between(new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31));
        var result = await _queries.AgentPerformanceAsync(period, AgentSort.Turnover, true);

        Assert.NotEmpty(result.Rows);
        Assert.Contains(result.Rows, r => r.RoleTitle.Contains("Рієлтор"));
        Assert.Contains(result.Rows, r => r.RoleTitle.Contains("Юрист"));

        for (int i = 0; i < result.Rows.Count - 1; i++)
        {
            Assert.True(result.Rows[i].TotalTurnoverUsd >= result.Rows[i + 1].TotalTurnoverUsd);
        }
    }

    [Fact]
    public async Task Scenario8_Query4_LandBankAudit_4TableJoin()
    {
        var filter = new LandBankFilter
        {
            RiskLevel = "Низький",
            OnlyVerifiedDocs = true
        };

        var result = await _queries.LandBankAuditAsync(filter);

        Assert.NotEmpty(result.Rows);
        Assert.NotEmpty(result.SqlQuery);

        foreach (var row in result.Rows)
        {
            Assert.Contains("Низький", row.RiskLevel);
            Assert.True(row.DocumentsCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(row.SellerName));
            Assert.False(string.IsNullOrWhiteSpace(row.CadastralNumber));
        }
    }

    [Fact]
    public async Task Scenario9_Query5_FindUser_And_PBKDF2_Authentication()
    {
        var user = await _queries.FindUserAsync("seller@landagency.ua");
        Assert.NotNull(user);

        bool validPass = _queries.CheckPassword(user, "client123");
        Assert.True(validPass);

        bool invalidPass = _queries.CheckPassword(user, "WrongPassword999!");
        Assert.False(invalidPass);
    }

    [Fact]
    public async Task Scenario10_Query6_AddUser_InTransaction_RejectsDuplicate()
    {
        string email = "new.client.test@landagency.ua";

        var user = await _queries.AddUserAsync(
            "Гончаренко Богдан Іванович",
            email,
            "+380 50 123 99 88",
            UserRole.Client,
            "pass12345",
            "UA113052990000026201234567800");

        Assert.NotNull(user);
        Assert.True(user.Id > 0);

        var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(savedUser);
        Assert.True(savedUser.Authenticate("pass12345"));

        var selectionReq = await _context.SelectionRequests.FirstOrDefaultAsync(s => s.BuyerId == user.Id);
        Assert.NotNull(selectionReq);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _queries.AddUserAsync(
            "Дублікат",
            email,
            "+380 50 000 00 00",
            UserRole.Client,
            "pass12345",
            null));
    }

    [Fact]
    public async Task Scenario11_Query7_UpdateUser_AndAdminProtection()
    {
        var user = await _context.Users.FirstAsync(u => u.Email == "seller@landagency.ua");

        await _queries.UpdateUserAsync(user.Id, "Шевченко Тарас Оновлений", "+380 67 999 88 77", false, "newSecretPass123");

        var updated = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Шевченко Тарас Оновлений", updated.FullName);
        Assert.Equal("+380 67 999 88 77", updated.Phone);
        Assert.False(updated.IsActive);
        Assert.True(updated.Authenticate("newSecretPass123"));

        var admin = await _context.Users.FirstAsync(u => u.Email.Contains("admin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _queries.UpdateUserAsync(admin.Id, admin.FullName, admin.Phone, false, null));
    }

    [Fact]
    public async Task Scenario12_Query8_DeleteUser_RestrictConstraint_And_CleanDelete()
    {
        var sellerWithPlots = await _context.Users.FirstAsync(u => u.Email == "seller@landagency.ua");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _queries.DeleteUserAsync(sellerWithPlots.Id));
        Assert.Contains("Restrict", ex.Message);

        var freshUser = await _queries.AddUserAsync(
            "Тимчасовий Клієнт",
            "temp.delete.me@landagency.ua",
            "+380 99 000 11 22",
            UserRole.Client,
            "pass12345",
            null);

        string deletedName = await _queries.DeleteUserAsync(freshUser.Id);
        Assert.Equal("Тимчасовий Клієнт", deletedName);

        var checkDeleted = await _context.Users.FindAsync(freshUser.Id);
        Assert.Null(checkDeleted);
    }
}
