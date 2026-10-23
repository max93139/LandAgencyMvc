using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LandAgencyMvc.Tests;

public class AutoMatchingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LandAgencyContext _context;
    private readonly AutoMatchingService _service;

    public AutoMatchingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LandAgencyContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LandAgencyContext(options);
        _context.Database.EnsureCreated();

        _service = new AutoMatchingService(_context);
    }

    [Fact]
    public async Task MatchRequestAsync_MatchesPlots_WithinBudgetAndMatchingCriteria()
    {
        var buyer = new AppUser { FullName = "Покупець 1", Email = "buyer1@test.ua", Role = UserRole.Client };
        var seller = new AppUser { FullName = "Продавець 1", Email = "seller1@test.ua", Role = UserRole.Client };
        _context.Users.AddRange(buyer, seller);
        await _context.SaveChangesAsync();

        var matchingPlot = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0120",
            Title = "Ділянка під забудову",
            Location = "Київська область, Обухівський район",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            AreaHectares = 0.15,
            PriceUsd = 25000m,
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id
        };

        var tooExpensivePlot = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0121",
            Title = "Елітна ділянка",
            Location = "Київська область, Обухівський район",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            AreaHectares = 0.20,
            PriceUsd = 80000m,
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id
        };

        var unverifiedPlot = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0122",
            Title = "Неперевірена ділянка",
            Location = "Київська область, Обухівський район",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            AreaHectares = 0.15,
            PriceUsd = 20000m,
            Status = PlotStatus.PendingLegalCheck,
            SellerId = seller.Id
        };

        _context.LandPlots.AddRange(matchingPlot, tooExpensivePlot, unverifiedPlot);

        var request = new SelectionRequest
        {
            BuyerId = buyer.Id,
            DesiredDistrict = "Київська область, Обухівський",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            MinAreaHa = 0.10,
            MaxBudgetUsd = 30000m
        };
        _context.SelectionRequests.Add(request);
        await _context.SaveChangesAsync();

        int matchesCount = await _service.MatchRequestAsync(request.Id);

        Assert.Equal(1, matchesCount);
        Assert.Equal(matchingPlot.Id.ToString(), request.MatchedPlotIds);
        Assert.True(request.IsProcessedByRealtor);
    }

    [Fact]
    public async Task MatchNewOrUpdatedPlotAsync_UpdatesExistingActiveRequests()
    {
        var buyer = new AppUser { FullName = "Покупець 2", Email = "buyer2@test.ua", Role = UserRole.Client };
        var seller = new AppUser { FullName = "Продавець 2", Email = "seller2@test.ua", Role = UserRole.Client };
        _context.Users.AddRange(buyer, seller);
        await _context.SaveChangesAsync();

        var request = new SelectionRequest
        {
            BuyerId = buyer.Id,
            DesiredDistrict = "Львівська область",
            Purpose = "01.03 Для ведення особистого селянського господарства",
            MinAreaHa = 0.5,
            MaxBudgetUsd = 15000m
        };
        _context.SelectionRequests.Add(request);

        var newPlot = new LandPlot
        {
            CadastralNumber = "4625883600:01:002:0045",
            Title = "ОСГ біля Львова",
            Location = "Львівська область, Пустомитівський",
            Purpose = "01.03 Для ведення особистого селянського господарства",
            AreaHectares = 0.8,
            PriceUsd = 12000m,
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id
        };
        _context.LandPlots.Add(newPlot);
        await _context.SaveChangesAsync();

        int updatedCount = await _service.MatchNewOrUpdatedPlotAsync(newPlot.Id);

        Assert.Equal(1, updatedCount);
        Assert.Contains(newPlot.Id.ToString(), request.MatchedPlotIds);
        Assert.True(request.IsProcessedByRealtor);
    }

    [Fact]
    public async Task MatchRequestAsync_ReturnsZero_WhenRequestNotFound()
    {
        int result = await _service.MatchRequestAsync(99999);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task MatchNewOrUpdatedPlotAsync_ReturnsZero_WhenPlotNotVerified()
    {
        var seller = new AppUser { FullName = "Продавець 3", Email = "seller3@test.ua", Role = UserRole.Client };
        _context.Users.Add(seller);
        await _context.SaveChangesAsync();

        var unverifiedPlot = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:9999",
            Title = "Чернетка",
            Location = "Київська область",
            Purpose = "02.01",
            AreaHectares = 0.10,
            PriceUsd = 10000m,
            Status = PlotStatus.PendingLegalCheck,
            SellerId = seller.Id
        };
        _context.LandPlots.Add(unverifiedPlot);
        await _context.SaveChangesAsync();

        int result = await _service.MatchNewOrUpdatedPlotAsync(unverifiedPlot.Id);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task MatchNewOrUpdatedPlotAsync_RemovesPlotFromMatches_WhenPriceIncreasesAboveBudgetOrStatusRejected()
    {
        var buyer = new AppUser { FullName = "Покупець 4", Email = "buyer4@test.ua", Role = UserRole.Client };
        var seller = new AppUser { FullName = "Продавець 4", Email = "seller4@test.ua", Role = UserRole.Client };
        _context.Users.AddRange(buyer, seller);
        await _context.SaveChangesAsync();

        var plot = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0555",
            Title = "Ділянка для динамічного тесту",
            Location = "Київська область, Вишгородський",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            AreaHectares = 0.15,
            PriceUsd = 20000m,
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id
        };
        _context.LandPlots.Add(plot);

        var request = new SelectionRequest
        {
            BuyerId = buyer.Id,
            DesiredDistrict = "Київська область",
            Purpose = "02.01 Для будівництва і обслуговування житлового будинку",
            MinAreaHa = 0.10,
            MaxBudgetUsd = 25000m
        };
        _context.SelectionRequests.Add(request);
        await _context.SaveChangesAsync();

        await _service.MatchRequestAsync(request.Id);
        Assert.Equal(1, request.MatchedPlotsCount);
        Assert.Contains(plot.Id, request.GetMatchedPlotIds());

        plot.UpdatePrice(40000m);
        await _context.SaveChangesAsync();

        int countAfterPriceIncrease = await _service.MatchNewOrUpdatedPlotAsync(plot.Id);
        Assert.Equal(0, countAfterPriceIncrease);
        Assert.Equal(0, request.MatchedPlotsCount);
        Assert.Empty(request.GetMatchedPlotIds());
        Assert.False(request.IsProcessedByRealtor);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
