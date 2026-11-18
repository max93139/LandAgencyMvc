using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using LandAgencyMvc.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

/// <summary>
/// ЛР6 «Паралельна обробка даних». Окрема база landagency_bench.db має ту саму схему, що й робоча, тому
/// 100 000 згенерованих ділянок не потрапляють у каталог, реєстр рієлтора та підбір.
/// </summary>
public interface IParallelAnalysisService
{
    bool BenchExists { get; }

    Task<int> CountBenchRowsAsync();

    /// <summary>
    /// Створює базу аналізу заново й копіює в таблицю LandPlots 20 ділянок із робочої бази.
    /// </summary>
    Task<int> PrepareSmallSampleAsync(LandAgencyContext mainDb);

    /// <summary>
    /// Програмно доповнює таблицю LandPlots бази аналізу до 100 000 записів.
    /// </summary>
    /// <returns>Кількість доданих записів і час генерації.</returns>
    Task<(int Added, TimeSpan Elapsed)> GenerateLargeSampleAsync(LandAgencyContext mainDb);

    Task<ParallelRunReport> RunComparisonAsync(int selectedThreads, int repetitions);

    ParallelResultsStore LoadResults();

    IReadOnlyList<int> ThreadOptions { get; }

    string BuildConfiguration { get; }
}

public sealed class ParallelAnalysisService : IParallelAnalysisService
{
    private const string BenchSellerEmail = "bench.seller@landagency.ua";
    private const int InsertBatchSize = 5_000;

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

    private readonly string _benchPath;
    private readonly string _resultsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ParallelAnalysisService(IWebHostEnvironment environment)
    {
        _benchPath = Path.Combine(environment.ContentRootPath, "landagency_bench.db");
        _resultsPath = Path.Combine(environment.ContentRootPath, "parallel_results.json");
    }

    public bool BenchExists => File.Exists(_benchPath);

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

    public async Task<int> CountBenchRowsAsync()
    {
        if (!BenchExists)
        {
            return 0;
        }

        await using var db = CreateBenchContext();
        return await db.LandPlots.CountAsync();
    }

    public async Task<int> PrepareSmallSampleAsync(LandAgencyContext mainDb)
    {
        await _gate.WaitAsync();
        try
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
            {
                File.Delete(_benchPath + suffix);
            }

            await using var db = CreateBenchContext();
            await db.Database.EnsureCreatedAsync();

            var seller = new AppUser
            {
                FullName = "Продавець вибірки ЛР6",
                Email = BenchSellerEmail,
                Phone = "+380000000000",
                Role = UserRole.Seller
            };
            seller.SetPassword(Guid.NewGuid().ToString("N"));
            db.Users.Add(seller);
            await db.SaveChangesAsync();

            var source = await mainDb.LandPlots
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Take(ParallelAnalysisLimits.SmallSampleSize)
                .ToListAsync();

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

    public async Task<(int Added, TimeSpan Elapsed)> GenerateLargeSampleAsync(LandAgencyContext mainDb)
    {
        if (!BenchExists)
        {
            await PrepareSmallSampleAsync(mainDb);
        }

        await _gate.WaitAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var db = CreateBenchContext();
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            int sellerId = await db.Users.Where(u => u.Email == BenchSellerEmail).Select(u => u.Id).FirstAsync();
            int existing = await db.LandPlots.CountAsync();
            int toAdd = Math.Max(0, ParallelAnalysisLimits.LargeSampleSize - existing);

            var random = new Random(2026);
            var createdBase = new DateTime(2025, 1, 1, 9, 0, 0);

            await using var transaction = await db.Database.BeginTransactionAsync();
            for (int start = 0; start < toAdd; start += InsertBatchSize)
            {
                int end = Math.Min(start + InsertBatchSize, toAdd);
                var batch = new List<LandPlot>(end - start);
                for (int i = start; i < end; i++)
                {
                    batch.Add(GeneratePlot(i, sellerId, random, createdBase));
                }

                db.LandPlots.AddRange(batch);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }

            await transaction.CommitAsync();
            stopwatch.Stop();
            return (toAdd, stopwatch.Elapsed);
        }
        finally
        {
            _gate.Release();
        }
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

        double roll = random.NextDouble();
        var status = roll < 0.70 ? PlotStatus.VerifiedInCatalog : roll < 0.85 ? PlotStatus.PendingLegalCheck : PlotStatus.Draft;

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
            Status = status,
            SellerId = sellerId,
            CreatedAt = createdBase.AddMinutes(i),
            Latitude = Math.Round(latitude + (random.NextDouble() - 0.5) * 0.5, 6),
            Longitude = Math.Round(longitude + (random.NextDouble() - 0.5) * 0.5, 6),
            SaleConditions = "Розрахунок у два етапи."
        };
    }

    public async Task<ParallelRunReport> RunComparisonAsync(int selectedThreads, int repetitions)
    {
        selectedThreads = Math.Clamp(selectedThreads, 1, 64);
        repetitions = Math.Clamp(repetitions, 3, 50);

        await _gate.WaitAsync();
        try
        {
            var report = await Task.Run(() => RunComparison(selectedThreads, repetitions));
            var store = LoadResults();
            if (report.RowCount <= ParallelAnalysisLimits.SmallSampleSize)
            {
                store.Small = report;
            }
            else
            {
                store.Large = report;
            }

            await File.WriteAllTextAsync(_resultsPath, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    private ParallelRunReport RunComparison(int selectedThreads, int repetitions)
    {
        var readTimings = MeasureReads(selectedThreads, out var rows);

        var expected = RunSequential(rows);
        WarmUp(rows);
        var report = new ParallelRunReport
        {
            RowCount = rows.Length,
            SelectedThreads = selectedThreads,
            Repetitions = repetitions,
            RunAtUtc = DateTime.UtcNow,
            BuildConfiguration = BuildConfiguration,
            ReadTimings = readTimings,
            Groups = expected
        };

        report.Timings.Add(Measure(AnalysisTechnique.Sequential, 1, repetitions, expected, () => RunSequential(rows)));

        var degrees = ThreadOptions.Append(selectedThreads).Distinct().OrderBy(n => n).ToList();
        foreach (var technique in new[] { AnalysisTechnique.Plinq, AnalysisTechnique.Threads, AnalysisTechnique.ParallelForEach, AnalysisTechnique.Tasks })
        {
            foreach (int n in degrees)
            {
                report.Timings.Add(Measure(technique, n, repetitions, expected, () => Run(technique, rows, n)));
            }
        }

        return report;
    }

    /// <summary>
    /// Прогрів перед вимірюваннями: JIT спершу компілює методи без оптимізацій і лише після кількох викликів
    /// замінює їх оптимізованими. Без прогріву перший виміряний спосіб (послідовний) був би несправедливо повільним.
    /// </summary>
    private void WarmUp(PlotAnalysisRow[] rows)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            RunSequential(rows);
            foreach (int n in ThreadOptions)
            {
                RunPlinq(rows, n);
                RunThreads(rows, n);
                RunParallelForEach(rows, n);
                RunTasks(rows, n);
            }
        }
        while (stopwatch.ElapsedMilliseconds < 1500);
    }

    private static TechniqueTiming Measure(
        AnalysisTechnique technique, int threads, int repetitions, List<PurposeGroupStats> expected, Func<List<PurposeGroupStats>> run)
    {
        var warmup = run();
        bool matches = SameResult(expected, warmup);

        var samples = new double[repetitions];
        var stopwatch = new Stopwatch();
        for (int r = 0; r < repetitions; r++)
        {
            stopwatch.Restart();
            var result = run();
            stopwatch.Stop();
            samples[r] = stopwatch.Elapsed.TotalMilliseconds;
            matches &= SameResult(expected, result);
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

    private static bool SameResult(List<PurposeGroupStats> expected, List<PurposeGroupStats> actual) =>
        expected.Count == actual.Count && expected.Zip(actual).All(pair => pair.First.SameAs(pair.Second));

    private static List<PurposeGroupStats> Run(AnalysisTechnique technique, PlotAnalysisRow[] rows, int threads) => technique switch
    {
        AnalysisTechnique.Plinq => RunPlinq(rows, threads),
        AnalysisTechnique.Threads => RunThreads(rows, threads),
        AnalysisTechnique.ParallelForEach => RunParallelForEach(rows, threads),
        AnalysisTechnique.Tasks => RunTasks(rows, threads),
        _ => RunSequential(rows)
    };

    /// <summary>
    /// Послідовне виконання запиту в одному потоці.
    /// </summary>
    public static List<PurposeGroupStats> RunSequential(PlotAnalysisRow[] rows)
    {
        var accumulator = new LandBankAccumulator();
        foreach (var row in rows)
        {
            accumulator.Add(row);
        }

        return accumulator.ToResult();
    }

    /// <summary>
    /// PLINQ на заданій кількості ядер: кожен потік накопичує свій підсумок, потім підсумки зливаються.
    /// </summary>
    public static List<PurposeGroupStats> RunPlinq(PlotAnalysisRow[] rows, int threads) =>
        rows.AsParallel()
            .WithDegreeOfParallelism(threads)
            .Aggregate(
                () => new LandBankAccumulator(),
                (accumulator, row) => { accumulator.Add(row); return accumulator; },
                (left, right) => { left.Merge(right); return left; },
                accumulator => accumulator.ToResult());

    /// <summary>
    /// Багатопоточність вручну: масив ділиться на рівні частини, кожну обробляє окремий Thread.
    /// </summary>
    public static List<PurposeGroupStats> RunThreads(PlotAnalysisRow[] rows, int threads)
    {
        var partials = new LandBankAccumulator[threads];
        var workers = new Thread[threads];
        int chunk = (rows.Length + threads - 1) / threads;

        for (int t = 0; t < threads; t++)
        {
            int index = t;
            int from = Math.Min(rows.Length, index * chunk);
            int to = Math.Min(rows.Length, from + chunk);
            workers[t] = new Thread(() =>
            {
                var accumulator = new LandBankAccumulator();
                for (int i = from; i < to; i++)
                {
                    accumulator.Add(rows[i]);
                }

                partials[index] = accumulator;
            });
            workers[t].Start();
        }

        foreach (var worker in workers)
        {
            worker.Join();
        }

        return MergeAll(partials);
    }

    /// <summary>
    /// TPL: Parallel.ForEach по діапазонах індексів із локальним підсумком для кожного потоку.
    /// </summary>
    public static List<PurposeGroupStats> RunParallelForEach(PlotAnalysisRow[] rows, int threads)
    {
        var total = new LandBankAccumulator();
        var sync = new object();
        int rangeSize = Math.Max(1, rows.Length / (threads * 4));

        Parallel.ForEach(
            Partitioner.Create(0, rows.Length, rangeSize),
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            () => new LandBankAccumulator(),
            (range, _, accumulator) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    accumulator.Add(rows[i]);
                }

                return accumulator;
            },
            accumulator =>
            {
                lock (sync)
                {
                    total.Merge(accumulator);
                }
            });

        return total.ToResult();
    }

    /// <summary>
    /// TPL: окреме завдання Task.Run на кожну частину масиву, очікування всіх завдань.
    /// </summary>
    public static List<PurposeGroupStats> RunTasks(PlotAnalysisRow[] rows, int threads)
    {
        int chunk = (rows.Length + threads - 1) / threads;
        var tasks = new Task<LandBankAccumulator>[threads];
        for (int t = 0; t < threads; t++)
        {
            int from = Math.Min(rows.Length, t * chunk);
            int to = Math.Min(rows.Length, from + chunk);
            tasks[t] = Task.Run(() =>
            {
                var accumulator = new LandBankAccumulator();
                for (int i = from; i < to; i++)
                {
                    accumulator.Add(rows[i]);
                }

                return accumulator;
            });
        }

        Task.WaitAll(tasks);
        return MergeAll(tasks.Select(t => t.Result));
    }

    private static List<PurposeGroupStats> MergeAll(IEnumerable<LandBankAccumulator> partials)
    {
        var total = new LandBankAccumulator();
        foreach (var partial in partials)
        {
            total.Merge(partial);
        }

        return total.ToResult();
    }

    /// <summary>
    /// Читання рядків із бази: одним з'єднанням і паралельно діапазонами Id, кожен діапазон своїм контекстом
    /// (DbContext не можна ділити між потоками).
    /// </summary>
    private List<ReadTiming> MeasureReads(int threads, out PlotAnalysisRow[] rows)
    {
        const int readRepetitions = 3;
        var sequentialSamples = new List<double>();
        var parallelSamples = new List<double>();
        PlotAnalysisRow[] sequentialRows = Array.Empty<PlotAnalysisRow>();
        int parallelCount = 0;

        ReadSequential();
        for (int r = 0; r < readRepetitions; r++)
        {
            var stopwatch = Stopwatch.StartNew();
            sequentialRows = ReadSequential();
            sequentialSamples.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            parallelCount = ReadParallel(threads).Length;
            parallelSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        rows = sequentialRows;
        return new List<ReadTiming>
        {
            new() { Name = "Послідовне читання", Connections = 1, MedianMs = Math.Round(Median(sequentialSamples), 3), RowsRead = sequentialRows.Length },
            new() { Name = "Паралельне читання діапазонами Id", Connections = threads, MedianMs = Math.Round(Median(parallelSamples), 3), RowsRead = parallelCount }
        };
    }

    private PlotAnalysisRow[] ReadSequential()
    {
        using var db = CreateBenchContext();
        return Project(db.LandPlots.AsNoTracking()).ToArray();
    }

    private PlotAnalysisRow[] ReadParallel(int threads)
    {
        int minId, maxId;
        using (var db = CreateBenchContext())
        {
            minId = db.LandPlots.Min(p => p.Id);
            maxId = db.LandPlots.Max(p => p.Id);
        }

        int span = (maxId - minId + threads) / threads;
        var parts = new PlotAnalysisRow[threads][];
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            int from = minId + t * span;
            int to = from + span;
            using var db = CreateBenchContext();
            parts[t] = Project(db.LandPlots.AsNoTracking().Where(p => p.Id >= from && p.Id < to)).ToArray();
        });

        return parts.SelectMany(p => p).ToArray();
    }

    private static IQueryable<PlotAnalysisRow> Project(IQueryable<LandPlot> plots) => plots.Select(p => new PlotAnalysisRow(
        p.CadastralNumber, p.Purpose, p.AreaHectares, p.PriceUsd, p.SellerBankIban, p.Latitude, p.Longitude));

    private static double Median(List<double> samples)
    {
        var sorted = samples.OrderBy(x => x).ToList();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    public ParallelResultsStore LoadResults()
    {
        if (!File.Exists(_resultsPath))
        {
            return new ParallelResultsStore();
        }

        try
        {
            return JsonSerializer.Deserialize<ParallelResultsStore>(File.ReadAllText(_resultsPath)) ?? new ParallelResultsStore();
        }
        catch (JsonException)
        {
            return new ParallelResultsStore();
        }
    }

    private LandAgencyContext CreateBenchContext() => new(new DbContextOptionsBuilder<LandAgencyContext>()
        .UseSqlite($"Data Source={_benchPath};Foreign Keys=True")
        .Options);
}
