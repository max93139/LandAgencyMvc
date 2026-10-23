using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LandAgencyMvc.Tests;

public class CadastralValidationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LandAgencyContext _context;
    private readonly CadastralValidationService _service;

    public CadastralValidationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LandAgencyContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LandAgencyContext(options);
        _context.Database.EnsureCreated();

        _service = new CadastralValidationService(_context);
    }

    [Theory]
    [InlineData("3221884000:03:001:0120", true, "32", "Київська область")]
    [InlineData("8000000000:66:123:0001", true, "80", "місто Київ")]
    [InlineData("4625883600:01:002:0045", true, "46", "Львівська область")]
    [InlineData("0710100000:11:001:0002", true, "07", "Волинська область")]
    [InlineData("3221884000-03-001-0120", false, "", "")]
    [InlineData("322188400:03:001:0120", false, "", "")]
    [InlineData("3221884000:3:001:0120", false, "", "")]
    [InlineData("3221884000:03:01:0120", false, "", "")]
    [InlineData("3221884000:03:001:012", false, "", "")]
    [InlineData("9921884000:03:001:0120", false, "99", "")]
    [InlineData("", false, "", "")]
    [InlineData("   ", false, "", "")]
    [InlineData("invalid-cadastral-format", false, "", "")]
    public void ValidateKoatuuFormat_CorrectlyValidatesFormatAndPrefix(
        string cadastralNumber,
        bool expectedValid,
        string expectedRegionCode,
        string expectedRegionName)
    {
        var result = _service.ValidateKoatuuFormat(cadastralNumber);

        Assert.Equal(expectedValid, result.IsValid);
        if (expectedValid)
        {
            Assert.Equal(expectedRegionCode, result.RegionCode);
            Assert.Equal(expectedRegionName, result.RegionName);
            Assert.NotEmpty(result.CadastralZone);
            Assert.NotEmpty(result.CadastralQuarter);
            Assert.NotEmpty(result.ParcelNumber);
        }
        else
        {
            Assert.NotEmpty(result.ErrorMessage);
        }
    }

    [Fact]
    public async Task AnalyzePricePerSotkaAsync_IdentifiesPriceAnomalies()
    {
        // 0.10 ha = 10 sotkas. Default residential benchmark is $1500/sotka ($15,000 total)
        // 1. Normal price: $15,000 for 10 sotkas ($1500/sotka)
        var normal = await _service.AnalyzePricePerSotkaAsync(15000m, 0.10, "02.01 Для будівництва", "3221884000:03:001:0120");
        Assert.False(normal.IsAnomalyDetected);
        Assert.Equal(1500m, normal.PricePerSotkaUsd);
        Assert.Equal("Низький (У межах ринку)", normal.ValuationRiskLevel);

        // 2. Severely undervalued: $5,000 for 10 sotkas ($500/sotka < 40% of 1500 = $600)
        var undervalued = await _service.AnalyzePricePerSotkaAsync(5000m, 0.10, "02.01 Для будівництва", "3221884000:03:001:0120");
        Assert.True(undervalued.IsAnomalyDetected);
        Assert.Equal(500m, undervalued.PricePerSotkaUsd);
        Assert.Contains("Високий", undervalued.ValuationRiskLevel);

        // 3. Excessively overvalued: $50,000 for 10 sotkas ($5000/sotka > 280% of 1500 = $4200)
        var overvalued = await _service.AnalyzePricePerSotkaAsync(50000m, 0.10, "02.01 Для будівництва", "3221884000:03:001:0120");
        Assert.True(overvalued.IsAnomalyDetected);
        Assert.Equal(5000m, overvalued.PricePerSotkaUsd);
        Assert.Contains("Середній", overvalued.ValuationRiskLevel);
    }

    [Fact]
    public async Task AnalyzePricePerSotkaAsync_HandlesZeroOrNegativeInputsSafely()
    {
        var zeroArea = await _service.AnalyzePricePerSotkaAsync(10000m, 0.0, "02.01 Будівництво", "3221884000:03:001:0120");
        Assert.True(zeroArea.PricePerSotkaUsd > 0);

        var zeroPrice = await _service.AnalyzePricePerSotkaAsync(0m, 0.10, "02.01 Будівництво", "3221884000:03:001:0120");
        Assert.Equal(0m, zeroPrice.PricePerSotkaUsd);
        Assert.True(zeroPrice.IsAnomalyDetected);
    }

    [Fact]
    public void ValidateKoatuuFormat_HandlesNullInputSafely()
    {
        var result = _service.ValidateKoatuuFormat(null!);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void GetPublicRegistryUrls_FormatsKadastrLiveAndOpenDataBot()
    {
        string cadNum = "3221884000:03:001:0120";

        string kadastrLive = _service.GetKadastrLiveUrl(cadNum);
        string openDataBot = _service.GetOpenDataBotUrl(cadNum);

        Assert.Equal("https://kadastr.live/parcel/3221884000:03:001:0120", kadastrLive);
        Assert.Equal("https://opendatabot.ua/c/3221884000:03:001:0120", openDataBot);
    }

    [Fact]
    public async Task DzkRegistrySimulator_DetectsArrestsAndServitudes_Correctly()
    {
        var cleanPlot = new LandPlot
        {
            Id = 1,
            CadastralNumber = "3221884000:03:001:0120",
            Title = "Чиста ділянка",
            Description = "Асфальтований під'їзд, газ",
            Purpose = "02.01 Для будівництва",
            AreaHectares = 0.15,
            PriceUsd = 22500m
        };
        var cleanExtract = await DzkRegistrySimulator.QueryRegistriesAsync(cleanPlot, _service);
        Assert.False(cleanExtract.HasDrrpArrestsOrMortgages);
        Assert.False(cleanExtract.HasDzkServitudesOrRestrictions);
        Assert.StartsWith("Низький", cleanExtract.RecommendedRiskLevel);

        var arrestPlot = new LandPlot
        {
            Id = 2,
            CadastralNumber = "3221884000:03:001:0000",
            Title = "Ділянка з обтяженням",
            Description = "Наявний судовий арешт у реєстрі",
            Purpose = "02.01 Для будівництва",
            AreaHectares = 0.15,
            PriceUsd = 22500m
        };
        var arrestExtract = await DzkRegistrySimulator.QueryRegistriesAsync(arrestPlot, _service);
        Assert.True(arrestExtract.HasDrrpArrestsOrMortgages);
        Assert.False(arrestExtract.OwnerAbsentInDebtorsRegistry);
        Assert.StartsWith("Високий", arrestExtract.RecommendedRiskLevel);

        var servitudePlot = new LandPlot
        {
            Id = 3,
            CadastralNumber = "3221884000:03:001:0009",
            Title = "Ділянка з охоронною зоною",
            Description = "Встановлено земельний сервітут проходу",
            Purpose = "02.01 Для будівництва",
            AreaHectares = 0.15,
            PriceUsd = 22500m
        };
        var servitudeExtract = await DzkRegistrySimulator.QueryRegistriesAsync(servitudePlot, _service);
        Assert.False(servitudeExtract.HasDrrpArrestsOrMortgages);
        Assert.True(servitudeExtract.HasDzkServitudesOrRestrictions);
        Assert.StartsWith("Середній", servitudeExtract.RecommendedRiskLevel);
    }

    [Fact]
    public void InitializeDatabase_SeedsCompleteDomainData_WhenDatabaseIsEmpty()
    {
        _context.InitializeDatabase();

        Assert.True(_context.Users.Count() >= 4);
        Assert.True(_context.LandPlots.Count() >= 4);
        Assert.True(_context.PlotDocuments.Any());
        Assert.True(_context.LegalConclusions.Any());
        Assert.True(_context.SelectionRequests.Any());
        Assert.True(_context.ViewingRequests.Any());
        Assert.True(_context.SaleDeals.Any());
        Assert.True(_context.Payments.Count() >= 2);

        foreach (var plot in _context.LandPlots)
        {
            Assert.DoesNotContain("іпотечне обтяження", plot.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("занижена оцінка", plot.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("перевірки юриста", plot.Description, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var req in _context.SelectionRequests)
        {
            Assert.DoesNotContain("(ID:", req.RealtorNotes, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DzkRegistrySimulator_DetectsArrestsAndPriceAnomalies_WithoutDebugArtifactsInTitle()
    {
        _context.InitializeDatabase();
        var gatnePlot = _context.LandPlots.First(p => p.CadastralNumber == "3221884000:03:001:0000");

        Assert.Equal("Ділянка під житлову забудову в с. Гатне", gatnePlot.Title);
        Assert.DoesNotContain("обтяження", gatnePlot.Title, StringComparison.OrdinalIgnoreCase);

        var extract = await DzkRegistrySimulator.QueryRegistriesAsync(gatnePlot, _service);

        Assert.True(extract.HasDrrpArrestsOrMortgages);
        Assert.True(extract.IsPriceAnomalyDetected);
        Assert.StartsWith("Високий", extract.RecommendedRiskLevel);
        Assert.Contains("3221884000", extract.CadastralZoneCode);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
