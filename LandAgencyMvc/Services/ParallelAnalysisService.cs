using System.Diagnostics;
using System.Text.Json;
using LandAgencyMvc.Data;
using LandAgencyMvc.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

/// <summary>
/// Шляхи до баз і файлу результатів ЛР6.
/// </summary>
/// <param name="MainConnectionString">Рядок підключення робочої бази застосунку.</param>
/// <param name="SmallDatabasePath">Файл окремої малої бази з 20 записами (пункт 2 завдання).</param>
/// <param name="ResultsPath">Файл, у якому зберігаються результати останніх досліджень.</param>
public sealed record ParallelAnalysisOptions(string MainConnectionString, string SmallDatabasePath, string ResultsPath);

/// <summary>
/// ЛР6 «Паралельна обробка даних»: підготовка даних, вимірювання й збереження результатів.
/// Мала база — окремий файл, у таблиці LandPlots якого рівно 20 записів. Велике дослідження виконується на
/// робочій базі: її таблицю LandPlots програмно доповнено до 100 000 записів.
/// </summary>
public interface IParallelAnalysisService
{
    IReadOnlyList<int> ThreadOptions { get; }

    string BuildConfiguration { get; }

    /// <summary>
    /// Кількість записів у малій базі або null, якщо її ще не створено.
    /// </summary>
    Task<int?> CountSmallRowsAsync();

    Task<int> CountMainRowsAsync();

    /// <summary>
    /// Скільки записів робочої бази згенеровано програмно для ЛР6.
    /// </summary>
    Task<int> CountGeneratedRowsAsync();

    Task<DatabaseConditions> GetConditionsAsync();

    /// <summary>
    /// Створює малу базу заново й копіює в її таблицю LandPlots 20 ділянок робочої бази.
    /// </summary>
    Task<int> PrepareSmallDatabaseAsync();

    /// <summary>
    /// Програмно доповнює таблицю LandPlots робочої бази до заданої кількості записів однією транзакцією.
    /// </summary>
    Task<(int Added, TimeSpan Elapsed)> FillMainDatabaseAsync(int targetRows = ParallelAnalysisLimits.LargeSampleSize);

    /// <summary>
    /// Прибирає з робочої бази записи, згенеровані для ЛР6, і повертає її до демонстраційного стану.
    /// </summary>
    Task<int> RemoveGeneratedAsync();

    Task<ParallelRunReport> RunComparisonAsync(AnalysisDatabase database, int selectedThreads, int repetitions);

    ParallelResultsStore LoadResults();
}

public sealed class ParallelAnalysisService : IParallelAnalysisService
{
    /// <summary>
    /// Власник згенерованих ділянок. Обліковий запис неактивний і без відомого пароля, а ділянки мають статус
    /// «Чернетка», тому не потрапляють у каталог, чергу юриста, автоматичний підбір і угоди.
    /// </summary>
    public const string LoadSellerEmail = "lab6.load@landagency.ua";

    private const int InsertBatchSize = 5_000;
    private const int WarmUpMilliseconds = 1_000;

    private static readonly string[] KoatuuCodes =
    {
        "3222486200", "3221881200", "3222482001", "3221884000", "3220882600",
        "3222480400", "3223155400", "3224986200", "3210800000", "3222410100"
    };

    private static readonly (string District, double Latitude, double Longitude)[] Districts =
    {
        ("Київська область, Бучанський район", 50.54, 30.21),
        ("Київська область, Обухівський район", 50.11, 30.62),
        ("Київська область, Фастівський район", 50.08, 29.92),
        ("Київська область, Броварський район", 50.51, 30.79),
        ("Київська область, Бориспільський район", 50.35, 30.95),
        ("Київська область, Білоцерківський район", 49.80, 30.11),
        ("Київська область, Вишгородський район", 50.58, 30.49),
        ("Київська область, Яготинський район", 50.27, 31.77)
    };

    private static readonly (string Purpose, decimal BasePerSotkaUsd)[] Purposes =
    {
        ("02.01 Для будівництва і обслуговування житлового будинку", 1500m),
        ("01.03 Для ведення особистого селянського господарства", 400m),
        ("01.05 Для індивідуального садівництва", 700m),
        ("03.07 Для будівництва та обслуговування будівель торгівлі", 3000m),
        ("11.02 Для розміщення промислових підприємств", 1200m)
    };

    private readonly ParallelAnalysisOptions _options;
    private readonly string _smallConnectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ParallelAnalysisService(ParallelAnalysisOptions options)
    {
        _options = options;
        _smallConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.SmallDatabasePath,
            ForeignKeys = true,
            Pooling = true
        }.ToString();

        EnsureThreadPoolCapacity();
    }

    public string BuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    public IReadOnlyList<int> ThreadOptions => new[] { 1, 2, 4, 8, Environment.ProcessorCount }
        .Distinct()
        .OrderBy(n => n)
        .ToList();

    /// <summary>
    /// Коли частини запиту чекають на читання з бази, вони займають потоки пулу. Пул додає нові потоки повільно,
    /// приблизно по одному за пів секунди, тому без запасу частини стояли б у черзі й вимір показував би очікування
    /// пулу, а не роботу. Мінімум потоків пулу піднімається до найбільшої кількості частин плюс кількість ядер.
    /// </summary>
    private int RequiredWorkerThreads => ThreadOptions.Max() + Environment.ProcessorCount;

    private void EnsureThreadPoolCapacity()
    {
        ThreadPool.GetMinThreads(out int workers, out int io);
        if (workers < RequiredWorkerThreads)
        {
            ThreadPool.SetMinThreads(RequiredWorkerThreads, io);
        }
    }

    public async Task<int?> CountSmallRowsAsync()
    {
        if (!File.Exists(_options.SmallDatabasePath))
        {
            return null;
        }

        await using var db = CreateSmallContext();
        return await db.LandPlots.CountAsync();
    }

    public async Task<int> CountMainRowsAsync()
    {
        await using var db = CreateMainReadContext();
        return await db.LandPlots.CountAsync();
    }

    public async Task<int> CountGeneratedRowsAsync()
    {
        await using var db = CreateMainReadContext();
        return await db.LandPlots.CountAsync(p => p.Seller != null && p.Seller.Email == LoadSellerEmail);
    }

    public async Task<DatabaseConditions> GetConditionsAsync()
    {
        await using var connection = new SqliteConnection(_options.MainConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var journalMode = (string?)await command.ExecuteScalarAsync() ?? string.Empty;

        ThreadPool.GetMinThreads(out int workers, out _);
        return new DatabaseConditions
        {
            JournalMode = journalMode,
            PoolingEnabled = new SqliteConnectionStringBuilder(_options.MainConnectionString).Pooling,
            MinWorkerThreads = workers,
            RequiredWorkerThreads = RequiredWorkerThreads
        };
    }

    public async Task<int> PrepareSmallDatabaseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
            {
                File.Delete(_options.SmallDatabasePath + suffix);
            }

            List<LandPlot> source;
            await using (var mainDb = CreateMainReadContext())
            {
                source = await mainDb.LandPlots
                    .Where(p => p.Seller == null || p.Seller.Email != LoadSellerEmail)
                    .OrderBy(p => p.Id)
                    .Take(ParallelAnalysisLimits.SmallSampleSize)
                    .ToListAsync();
            }

            await using var db = CreateSmallContext();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");

            var seller = NewLoadSeller();
            db.Users.Add(seller);
            await db.SaveChangesAsync();

            db.LandPlots.AddRange(source.Select(p => new LandPlot
            {
                CadastralNumber = p.CadastralNumber,
                Title = p.Title,
                Location = p.Location,
                AreaHectares = p.AreaHectares,
                Purpose = p.Purpose,
                PriceUsd = p.PriceUsd,
                Description = p.Description,
                OwnershipDocNumber = p.OwnershipDocNumber,
                SellerBankIban = p.SellerBankIban,
                Status = p.Status,
                SellerId = seller.Id,
                CreatedAt = p.CreatedAt,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                SaleConditions = p.SaleConditions
            }));
            await db.SaveChangesAsync();
            return source.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(int Added, TimeSpan Elapsed)> FillMainDatabaseAsync(int targetRows = ParallelAnalysisLimits.LargeSampleSize)
    {
        await _gate.WaitAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var db = CreateMainWriteContext();
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            int existing = await db.LandPlots.CountAsync();
            int toAdd = Math.Max(0, targetRows - existing);
            if (toAdd == 0)
            {
                return (0, stopwatch.Elapsed);
            }

            await using var transaction = await db.Database.BeginTransactionAsync();

            var seller = await db.Users.FirstOrDefaultAsync(u => u.Email == LoadSellerEmail);
            if (seller == null)
            {
                seller = NewLoadSeller();
                db.Users.Add(seller);
                await db.SaveChangesAsync();
            }

            int alreadyGenerated = await db.LandPlots.CountAsync(p => p.SellerId == seller.Id);
            var random = new Random(2026 + alreadyGenerated);
            // Дати згенерованих записів раніші за справжні, тож реєстр рієлтора, відсортований від нових до старих,
            // і далі починається зі справжніх ділянок.
            var createdBase = new DateTime(2020, 1, 1, 9, 0, 0);

            for (int start = 0; start < toAdd; start += InsertBatchSize)
            {
                int end = Math.Min(start + InsertBatchSize, toAdd);
                var batch = new List<LandPlot>(end - start);
                for (int i = start; i < end; i++)
                {
                    batch.Add(GeneratePlot(alreadyGenerated + i, seller.Id, random, createdBase));
                }

                db.LandPlots.AddRange(batch);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }

            await transaction.CommitAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
            stopwatch.Stop();
            return (toAdd, stopwatch.Elapsed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> RemoveGeneratedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var db = CreateMainWriteContext();
            var seller = await db.Users.FirstOrDefaultAsync(u => u.Email == LoadSellerEmail);
            if (seller == null)
            {
                return 0;
            }

            await using var transaction = await db.Database.BeginTransactionAsync();
            int removed = await db.LandPlots.Where(p => p.SellerId == seller.Id).ExecuteDeleteAsync();
            db.Users.Remove(seller);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");

            var store = LoadResults();
            store.Large = null;
            await SaveResultsAsync(store);
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AppUser NewLoadSeller()
    {
        var seller = new AppUser
        {
            FullName = "Тестове наповнення ЛР6",
            Email = LoadSellerEmail,
            Phone = "+380000000000",
            Role = UserRole.Seller,
            IsActive = false
        };
        seller.SetPassword(Guid.NewGuid().ToString("N"));
        return seller;
    }

    /// <summary>
    /// Створює одну згенеровану ділянку. Кадастровий номер унікальний для кожного i; приблизно 3% номерів
    /// і 2% IBAN навмисно некоректні, щоб перевіркам запиту було що відсіювати.
    /// </summary>
    private static LandPlot GeneratePlot(int i, int sellerId, Random random, DateTime createdBase)
    {
        string koatuu = KoatuuCodes[i % KoatuuCodes.Length];
        int quarter = i / KoatuuCodes.Length / 10_000 % 1000;
        int parcel = i / KoatuuCodes.Length % 10_000;
        string cadastral = random.NextDouble() < 0.03
            ? $"{koatuu}:90:{quarter:D3}-{parcel:D4}"
            : $"{koatuu}:90:{quarter:D3}:{parcel:D4}";

        var (purpose, basePerSotka) = Purposes[random.Next(Purposes.Length)];
        var (district, latitude, longitude) = Districts[random.Next(Districts.Length)];

        double area = Math.Round(0.04 + random.NextDouble() * 2.96, 4);
        decimal perSotka = basePerSotka * (decimal)(0.6 + random.NextDouble());
        decimal price = Math.Max(100m, Math.Round(perSotka * (decimal)(area * 100.0), 0));

        string iban = LandBankAudit.BuildUaIban($"305299{i:D19}");
        if (random.NextDouble() < 0.02)
        {
            iban = iban[..10] + (iban[10] == '9' ? '0' : (char)(iban[10] + 1)) + iban[11..];
        }

        return new LandPlot
        {
            CadastralNumber = cadastral,
            Title = $"Згенерована ділянка №{i + 1}",
            Location = $"{district}, масив {i % 500 + 1}",
            AreaHectares = area,
            Purpose = purpose,
            PriceUsd = price,
            Description = "Запис згенеровано програмно для ЛР6.",
            OwnershipDocNumber = $"ДРРП-{50_000_000 + i}",
            SellerBankIban = iban,
            Status = PlotStatus.Draft,
            SellerId = sellerId,
            CreatedAt = createdBase.AddMinutes(i),
            Latitude = Math.Round(latitude + (random.NextDouble() - 0.5) * 0.5, 6),
            Longitude = Math.Round(longitude + (random.NextDouble() - 0.5) * 0.5, 6),
            SaleConditions = "Розрахунок у два етапи."
        };
    }

    public async Task<ParallelRunReport> RunComparisonAsync(AnalysisDatabase database, int selectedThreads, int repetitions)
    {
        selectedThreads = Math.Clamp(selectedThreads, 1, 64);
        repetitions = Math.Clamp(repetitions, 3, 30);

        await _gate.WaitAsync();
        try
        {
            EnsureThreadPoolCapacity();
            var report = await Task.Run(() => RunComparisonCoreAsync(database, selectedThreads, repetitions));
            var store = LoadResults();
            if (database == AnalysisDatabase.Small)
            {
                store.Small = report;
            }
            else
            {
                store.Large = report;
            }

            await SaveResultsAsync(store);
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ParallelRunReport> RunComparisonCoreAsync(AnalysisDatabase database, int selectedThreads, int repetitions)
    {
        Func<LandAgencyContext> createContext = database == AnalysisDatabase.Small ? CreateSmallContext : CreateMainReadContext;
        var degrees = ThreadOptions.Append(selectedThreads).Distinct().OrderBy(n => n).ToList();

        var expected = await LandBankQuery.RunOnDatabaseAsync(AnalysisTechnique.Sequential, createContext, 1);
        var rows = LandBankQuery.LoadRows(createContext);
        await WarmUpAsync(createContext, rows, degrees);

        var report = new ParallelRunReport
        {
            Database = database,
            RowCount = rows.Length,
            SelectedThreads = selectedThreads,
            Repetitions = repetitions,
            RunAtUtc = DateTime.UtcNow,
            BuildConfiguration = BuildConfiguration,
            Groups = expected
        };

        report.DatabaseQuery.Timings.Add(await MeasureAsync(AnalysisTechnique.Sequential, 1, repetitions, expected,
            () => LandBankQuery.RunOnDatabaseAsync(AnalysisTechnique.Sequential, createContext, 1)));
        report.MemoryOnly.Timings.Add(await MeasureAsync(AnalysisTechnique.Sequential, 1, repetitions, expected,
            () => Task.FromResult(LandBankQuery.RunSequential(rows))));

        foreach (var technique in TechniqueTiming.ParallelTechniques)
        {
            foreach (int n in degrees)
            {
                report.DatabaseQuery.Timings.Add(await MeasureAsync(technique, n, repetitions, expected,
                    () => LandBankQuery.RunOnDatabaseAsync(technique, createContext, n)));
                report.MemoryOnly.Timings.Add(await MeasureAsync(technique, n, repetitions, expected,
                    () => Task.FromResult(LandBankQuery.RunInMemory(technique, rows, n))));
            }
        }

        return report;
    }

    /// <summary>
    /// Прогрів перед вимірюваннями: JIT спершу компілює методи без оптимізацій і лише після кількох викликів
    /// замінює їх оптимізованими, EF Core будує модель і кешує скомпільовані запити, пул відкриває з'єднання,
    /// а SQLite заповнює кеш сторінок. Без прогріву перший виміряний спосіб був би несправедливо повільним.
    /// </summary>
    private static async Task WarmUpAsync(Func<LandAgencyContext> createContext, PlotAnalysisRow[] rows, IReadOnlyList<int> degrees)
    {
        foreach (var technique in TechniqueTiming.ParallelTechniques)
        {
            await LandBankQuery.RunOnDatabaseAsync(technique, createContext, degrees[^1]);
        }

        var stopwatch = Stopwatch.StartNew();
        do
        {
            LandBankQuery.RunSequential(rows);
            foreach (var technique in TechniqueTiming.ParallelTechniques)
            {
                foreach (int n in degrees)
                {
                    LandBankQuery.RunInMemory(technique, rows, n);
                }
            }
        }
        while (stopwatch.ElapsedMilliseconds < WarmUpMilliseconds);
    }

    /// <summary>
    /// Один прогін без виміру, потім задана кількість вимірюваних прогонів; у результат іде медіана.
    /// Кожен прогін порівнюється з результатом послідовного запиту.
    /// </summary>
    private static async Task<TechniqueTiming> MeasureAsync(
        AnalysisTechnique technique, int threads, int repetitions, List<PurposeGroupStats> expected,
        Func<Task<List<PurposeGroupStats>>> run)
    {
        bool matches = LandBankQuery.SameResult(expected, await run());

        var samples = new double[repetitions];
        var stopwatch = new Stopwatch();
        for (int r = 0; r < repetitions; r++)
        {
            stopwatch.Restart();
            var result = await run();
            stopwatch.Stop();
            samples[r] = stopwatch.Elapsed.TotalMilliseconds;
            matches &= LandBankQuery.SameResult(expected, result);
        }

        Array.Sort(samples);
        double median = repetitions % 2 == 1
            ? samples[repetitions / 2]
            : (samples[repetitions / 2 - 1] + samples[repetitions / 2]) / 2;

        return new TechniqueTiming
        {
            Technique = technique,
            Threads = threads,
            MedianMs = Math.Round(median, 4),
            MinMs = Math.Round(samples[0], 4),
            MaxMs = Math.Round(samples[^1], 4),
            ResultMatches = matches
        };
    }

    public ParallelResultsStore LoadResults()
    {
        if (!File.Exists(_options.ResultsPath))
        {
            return new ParallelResultsStore();
        }

        try
        {
            return JsonSerializer.Deserialize<ParallelResultsStore>(File.ReadAllText(_options.ResultsPath)) ?? new ParallelResultsStore();
        }
        catch (JsonException)
        {
            return new ParallelResultsStore();
        }
    }

    private Task SaveResultsAsync(ParallelResultsStore store) =>
        File.WriteAllTextAsync(_options.ResultsPath, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>
    /// Контекст для читання робочої бази: кожна паралельна частина створює власний, бо DbContext не можна
    /// використовувати з кількох потоків одночасно. З'єднання береться з пулу, тож не відкривається щоразу заново.
    /// </summary>
    private LandAgencyContext CreateMainReadContext() => new(new DbContextOptionsBuilder<LandAgencyContext>()
        .UseSqlite(_options.MainConnectionString)
        .Options);

    /// <summary>
    /// Контекст для запису в робочу базу з тим самим перехоплювачем надійності, що й у застосунку.
    /// </summary>
    private LandAgencyContext CreateMainWriteContext() => new(new DbContextOptionsBuilder<LandAgencyContext>()
        .UseSqlite(_options.MainConnectionString)
        .AddInterceptors(new SqliteDurabilityInterceptor())
        .Options);

    private LandAgencyContext CreateSmallContext() => new(new DbContextOptionsBuilder<LandAgencyContext>()
        .UseSqlite(_smallConnectionString)
        .Options);
}
