using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Tests;

/// <summary>
/// Тимчасова тека з робочою й малою базами SQLite для одного тесту. Робоча база створюється за моделлю
/// застосунку в режимі WAL і містить продавця та задану кількість демонстраційних ділянок.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public TestDatabase(int demoPlots = 22)
    {
        Directory = Path.Combine(Path.GetTempPath(), "landagency-tests-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        MainPath = Path.Combine(Directory, "landagency.db");
        MainConnectionString = $"Data Source={MainPath};Foreign Keys=True;Pooling=True";

        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");

        var seller = new AppUser { FullName = "Демо продавець", Email = "demo.seller@test.ua", Phone = "+380501112233", Role = UserRole.Seller };
        seller.SetPassword("demo12345");
        db.Users.Add(seller);
        db.SaveChanges();

        for (int i = 0; i < demoPlots; i++)
        {
            db.LandPlots.Add(new LandPlot
            {
                CadastralNumber = $"3222486200:01:{i / 100:D3}:{i % 100:D4}",
                Title = $"Демо ділянка {i + 1}",
                Location = "Київська область, Бучанський район",
                AreaHectares = 0.1 + i * 0.01,
                Purpose = LandPlot.StandardPurposes[i % LandPlot.StandardPurposes.Count].FullValue,
                PriceUsd = 10_000 + i * 500,
                SellerBankIban = LandBankAudit.BuildUaIban($"305299{i:D19}"),
                Status = PlotStatus.VerifiedInCatalog,
                SellerId = seller.Id,
                Latitude = 50.4 + i * 0.001,
                Longitude = 30.5
            });
        }

        db.SaveChanges();
    }

    public string Directory { get; }

    public string MainPath { get; }

    public string MainConnectionString { get; }

    public string SmallPath => Path.Combine(Directory, "small.db");

    public string ResultsPath => Path.Combine(Directory, "results.json");

    public LandAgencyContext CreateContext() => new(new DbContextOptionsBuilder<LandAgencyContext>()
        .UseSqlite(MainConnectionString)
        .Options);

    public ParallelAnalysisService CreateService() =>
        new(new ParallelAnalysisOptions(MainConnectionString, SmallPath, ResultsPath));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
