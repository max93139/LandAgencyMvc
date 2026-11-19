namespace LandAgencyMvc.Models;

/// <summary>
/// Рядок земельного банку, потрібний для аналізу: лише поля, які читаються з бази для запиту ЛР6.
/// </summary>
public sealed record PlotAnalysisRow(
    string CadastralNumber,
    string Purpose,
    double AreaHectares,
    decimal PriceUsd,
    string SellerBankIban,
    double? Latitude,
    double? Longitude);

/// <summary>
/// Запит ЛР6 «Аудит земельного банку за цільовим призначенням». Для кожної ділянки перевіряються формат
/// кадастрового номера й контрольна сума IBAN продавця, рахуються ціна за сотку та відстань до Києва,
/// а результат групується за кодом КВЦПЗ.
/// </summary>
public static class LandBankAudit
{
    private const double KyivLatitude = 50.4501;
    private const double KyivLongitude = 30.5234;
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>
    /// Код КВЦПЗ, наприклад «02.01», з повного значення цільового призначення.
    /// </summary>
    public static string PurposeCode(string purpose)
    {
        var trimmed = (purpose ?? string.Empty).Trim();
        return trimmed.Length >= 5 ? trimmed[..5] : trimmed;
    }

    /// <summary>
    /// Перевіряє український IBAN: «UA», 27 цифр і контрольна сума за модулем 97 (ISO 13616).
    /// </summary>
    public static bool IsValidUaIban(string? iban)
    {
        if (iban == null || iban.Length != 29 || iban[0] != 'U' || iban[1] != 'A')
        {
            return false;
        }

        int remainder = 0;
        for (int i = 4; i < 29; i++)
        {
            char c = iban[i];
            if (c < '0' || c > '9')
            {
                return false;
            }

            remainder = (remainder * 10 + (c - '0')) % 97;
        }

        remainder = (remainder * 100 + 30) % 97;
        remainder = (remainder * 100 + 10) % 97;
        for (int i = 2; i < 4; i++)
        {
            char c = iban[i];
            if (c < '0' || c > '9')
            {
                return false;
            }

            remainder = (remainder * 10 + (c - '0')) % 97;
        }

        return remainder == 1;
    }

    /// <summary>
    /// Контрольні цифри для українського IBAN із заданою 25-значною частиною (МФО банку й номер рахунку).
    /// </summary>
    public static string BuildUaIban(string bban)
    {
        int remainder = 0;
        foreach (char c in bban)
        {
            remainder = (remainder * 10 + (c - '0')) % 97;
        }

        remainder = (remainder * 100 + 30) % 97;
        remainder = (remainder * 100 + 10) % 97;
        remainder = (remainder * 100) % 97;
        int check = 98 - remainder;
        return $"UA{check:D2}{bban}";
    }

    /// <summary>
    /// Відстань від центру ділянки до центру Києва за формулою гаверсинуса, округлена до метра.
    /// </summary>
    public static long DistanceToKyivMeters(double latitude, double longitude)
    {
        double dLat = ToRadians(latitude - KyivLatitude);
        double dLon = ToRadians(longitude - KyivLongitude);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(KyivLatitude)) * Math.Cos(ToRadians(latitude)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (long)Math.Round(EarthRadiusMeters * c);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Проміжний підсумок по групах КВЦПЗ. Кожен потік накопичує власний екземпляр, а наприкінці екземпляри
/// зливаються методом <see cref="Merge"/>. Суми цілочисельні або decimal, тому результат не залежить
/// від порядку обробки рядків і однаковий для всіх способів розпаралелювання.
/// Ключ словника — повне значення призначення, щоб не створювати рядок коду КВЦПЗ для кожної ділянки:
/// виділення пам'яті в кожному рядку змушувало б потоки чекати на збирач сміття.
/// </summary>
public sealed class LandBankAccumulator
{
    private readonly Dictionary<string, PurposeGroupStats> _groups = new();

    public void Add(PlotAnalysisRow row)
    {
        if (!_groups.TryGetValue(row.Purpose, out var stats))
        {
            stats = new PurposeGroupStats { PurposeCode = LandBankAudit.PurposeCode(row.Purpose) };
            _groups[row.Purpose] = stats;
        }

        stats.Count++;
        if (!LandPlot.IsValidCadastralFormat(row.CadastralNumber))
        {
            stats.InvalidCadastralCount++;
        }

        if (!LandBankAudit.IsValidUaIban(row.SellerBankIban))
        {
            stats.InvalidIbanCount++;
        }

        if (row.AreaHectares > 0)
        {
            decimal perSotka = Math.Round(row.PriceUsd / (decimal)(row.AreaHectares * 100.0), 2);
            stats.SumPricePerSotkaUsd += perSotka;
            if (perSotka > stats.MaxPricePerSotkaUsd)
            {
                stats.MaxPricePerSotkaUsd = perSotka;
            }
        }

        if (row.Latitude.HasValue && row.Longitude.HasValue)
        {
            stats.WithCoordinatesCount++;
            stats.SumDistanceMeters += LandBankAudit.DistanceToKyivMeters(row.Latitude.Value, row.Longitude.Value);
        }
    }

    public void Merge(LandBankAccumulator other)
    {
        foreach (var (purpose, theirs) in other._groups)
        {
            if (_groups.TryGetValue(purpose, out var mine))
            {
                mine.Add(theirs);
            }
            else
            {
                _groups[purpose] = theirs.Clone();
            }
        }
    }

    public List<PurposeGroupStats> ToResult()
    {
        var byCode = new Dictionary<string, PurposeGroupStats>();
        foreach (var stats in _groups.Values)
        {
            if (byCode.TryGetValue(stats.PurposeCode, out var existing))
            {
                existing.Add(stats);
            }
            else
            {
                byCode[stats.PurposeCode] = stats.Clone();
            }
        }

        return byCode.Values.OrderBy(g => g.PurposeCode, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Підсумок по одній групі цільового призначення.
/// </summary>
public sealed class PurposeGroupStats
{
    public string PurposeCode { get; set; } = string.Empty;

    public int Count { get; set; }

    public int InvalidCadastralCount { get; set; }

    public int InvalidIbanCount { get; set; }

    public decimal SumPricePerSotkaUsd { get; set; }

    public decimal MaxPricePerSotkaUsd { get; set; }

    public int WithCoordinatesCount { get; set; }

    public long SumDistanceMeters { get; set; }

    public decimal AvgPricePerSotkaUsd => Count > 0 ? Math.Round(SumPricePerSotkaUsd / Count, 2) : 0m;

    public double AvgDistanceKm => WithCoordinatesCount > 0 ? Math.Round(SumDistanceMeters / 1000.0 / WithCoordinatesCount, 1) : 0;

    public PurposeGroupStats Clone() => (PurposeGroupStats)MemberwiseClone();

    public void Add(PurposeGroupStats other)
    {
        Count += other.Count;
        InvalidCadastralCount += other.InvalidCadastralCount;
        InvalidIbanCount += other.InvalidIbanCount;
        SumPricePerSotkaUsd += other.SumPricePerSotkaUsd;
        MaxPricePerSotkaUsd = Math.Max(MaxPricePerSotkaUsd, other.MaxPricePerSotkaUsd);
        WithCoordinatesCount += other.WithCoordinatesCount;
        SumDistanceMeters += other.SumDistanceMeters;
    }

    public bool SameAs(PurposeGroupStats other) =>
        PurposeCode == other.PurposeCode &&
        Count == other.Count &&
        InvalidCadastralCount == other.InvalidCadastralCount &&
        InvalidIbanCount == other.InvalidIbanCount &&
        SumPricePerSotkaUsd == other.SumPricePerSotkaUsd &&
        MaxPricePerSotkaUsd == other.MaxPricePerSotkaUsd &&
        WithCoordinatesCount == other.WithCoordinatesCount &&
        SumDistanceMeters == other.SumDistanceMeters;
}


/// <summary>
/// Діапазон первинних ключів, який читає й обробляє одна паралельна частина запиту. Межі включні.
/// </summary>
public readonly record struct IdRange(int From, int To);

/// <summary>
/// Спосіб виконання запиту.
/// </summary>
public enum AnalysisTechnique
{
    Sequential,
    Plinq,
    Threads,
    ParallelForEach,
    Tasks
}

/// <summary>
/// База, на якій виконується дослідження: окрема мала база з 20 записами або робоча база застосунку.
/// </summary>
public enum AnalysisDatabase
{
    Small,
    Main
}

/// <summary>
/// Час одного способу виконання на заданій кількості потоків.
/// </summary>
public sealed class TechniqueTiming
{
    public AnalysisTechnique Technique { get; set; }

    public int Threads { get; set; }

    public double MedianMs { get; set; }

    public double MinMs { get; set; }

    public double MaxMs { get; set; }

    public bool ResultMatches { get; set; }

    public string DisplayName => DisplayNameOf(Technique);

    public static string DisplayNameOf(AnalysisTechnique technique) => technique switch
    {
        AnalysisTechnique.Sequential => "Послідовно (LINQ)",
        AnalysisTechnique.Plinq => "PLINQ (кілька ядер)",
        AnalysisTechnique.Threads => "Багатопоточність (Thread)",
        AnalysisTechnique.ParallelForEach => "TPL: Parallel.ForEach",
        AnalysisTechnique.Tasks => "TPL: Task.Run",
        _ => technique.ToString()
    };

    public static string ShortNameOf(AnalysisTechnique technique) => technique switch
    {
        AnalysisTechnique.Sequential => "Послідовно",
        AnalysisTechnique.Plinq => "PLINQ",
        AnalysisTechnique.Threads => "Thread",
        AnalysisTechnique.ParallelForEach => "Parallel.ForEach",
        AnalysisTechnique.Tasks => "Task.Run",
        _ => technique.ToString()
    };

    public static IReadOnlyList<AnalysisTechnique> ParallelTechniques { get; } = new[]
    {
        AnalysisTechnique.Plinq, AnalysisTechnique.Threads, AnalysisTechnique.ParallelForEach, AnalysisTechnique.Tasks
    };
}

/// <summary>
/// Виміри одного режиму: послідовний спосіб і кожен паралельний спосіб на кожній кількості потоків.
/// </summary>
public sealed class TimingSet
{
    public List<TechniqueTiming> Timings { get; set; } = new();

    public double SequentialMedianMs => Timings.FirstOrDefault(t => t.Technique == AnalysisTechnique.Sequential)?.MedianMs ?? 0;

    public IEnumerable<int> Degrees => Timings
        .Where(t => t.Technique != AnalysisTechnique.Sequential)
        .Select(t => t.Threads)
        .Distinct()
        .OrderBy(n => n);

    public TechniqueTiming? Find(AnalysisTechnique technique, int threads) => Timings.FirstOrDefault(t =>
        t.Technique == technique && (technique == AnalysisTechnique.Sequential || t.Threads == threads));

    /// <summary>
    /// Прискорення відносно послідовного способу: у скільки разів паралельний спосіб швидший.
    /// </summary>
    public double SpeedupOf(TechniqueTiming timing) => timing.MedianMs > 0 ? SequentialMedianMs / timing.MedianMs : 0;

    public TechniqueTiming? Best => Timings
        .Where(t => t.Technique != AnalysisTechnique.Sequential)
        .OrderBy(t => t.MedianMs)
        .FirstOrDefault();
}

/// <summary>
/// Результати одного дослідження на певній базі.
/// Database — повний запит: кожна частина сама читає свій діапазон рядків із бази й обробляє його.
/// Memory — лише обчислювальна частина над рядками, уже прочитаними в пам'ять.
/// </summary>
public sealed class ParallelRunReport
{
    public AnalysisDatabase Database { get; set; }

    public int RowCount { get; set; }

    public int SelectedThreads { get; set; }

    public int Repetitions { get; set; }

    public DateTime RunAtUtc { get; set; }

    public string BuildConfiguration { get; set; } = string.Empty;

    public TimingSet DatabaseQuery { get; set; } = new();

    public TimingSet MemoryOnly { get; set; } = new();

    public List<PurposeGroupStats> Groups { get; set; } = new();

    public bool IsComplete => DatabaseQuery.Timings.Count > 0;

    public bool AllResultsMatch => DatabaseQuery.Timings.Concat(MemoryOnly.Timings).All(t => t.ResultMatches);
}

/// <summary>
/// Збережені результати для малої бази й робочої бази.
/// </summary>
public sealed class ParallelResultsStore
{
    public ParallelRunReport? Small { get; set; }

    public ParallelRunReport? Large { get; set; }

    public ParallelRunReport? For(AnalysisDatabase database)
    {
        var report = database == AnalysisDatabase.Small ? Small : Large;
        return report is { IsComplete: true } ? report : null;
    }
}

/// <summary>
/// Умови, без яких кілька потоків не можуть ефективно читати одну базу SQLite.
/// </summary>
public sealed class DatabaseConditions
{
    public string JournalMode { get; set; } = string.Empty;

    public bool PoolingEnabled { get; set; }

    public int MinWorkerThreads { get; set; }

    public int RequiredWorkerThreads { get; set; }

    public bool IsWal => string.Equals(JournalMode, "wal", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Модель сторінки «Паралельний аналіз».
/// </summary>
public sealed class ParallelAnalysisViewModel
{
    public AppUser? CurrentUser { get; set; }

    public int ProcessorCount { get; set; }

    public string ProcessorName { get; set; } = string.Empty;

    public string RuntimeDescription { get; set; } = string.Empty;

    public string OsDescription { get; set; } = string.Empty;

    public string BuildConfiguration { get; set; } = string.Empty;

    /// <summary>
    /// Кількість записів у таблиці LandPlots малої бази або null, якщо малу базу ще не створено.
    /// </summary>
    public int? SmallRowCount { get; set; }

    public int MainRowCount { get; set; }

    public int GeneratedRowCount { get; set; }

    public DatabaseConditions Conditions { get; set; } = new();

    public int SelectedThreads { get; set; }

    public int Repetitions { get; set; } = ParallelAnalysisLimits.DefaultRepetitions;

    public IReadOnlyList<int> ThreadOptions { get; set; } = Array.Empty<int>();

    public ParallelResultsStore Results { get; set; } = new();

    public AnalysisDatabase Shown { get; set; }

    public ParallelRunReport? ShownReport => Results.For(Shown);

    public bool SmallReady => SmallRowCount is > 0;

    public bool MainFilled => MainRowCount >= ParallelAnalysisLimits.LargeSampleSize;
}

public static class ParallelAnalysisLimits
{
    public const int SmallSampleSize = 20;

    public const int LargeSampleSize = 100_000;

    public const int DefaultRepetitions = 7;
}
