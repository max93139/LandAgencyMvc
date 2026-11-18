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
        AnalysisTechnique.Sequential => "Послідовний LINQ",
        AnalysisTechnique.Plinq => "PLINQ (кілька ядер)",
        AnalysisTechnique.Threads => "Багатопоточність (Thread)",
        AnalysisTechnique.ParallelForEach => "TPL: Parallel.ForEach",
        AnalysisTechnique.Tasks => "TPL: Task.Run + Task.WaitAll",
        _ => technique.ToString()
    };
}

/// <summary>
/// Час читання рядків із бази: послідовно одним з'єднанням або паралельно діапазонами Id.
/// </summary>
public sealed class ReadTiming
{
    public string Name { get; set; } = string.Empty;

    public int Connections { get; set; }

    public double MedianMs { get; set; }

    public int RowsRead { get; set; }
}

/// <summary>
/// Результати одного прогону порівняння на певному обсязі даних.
/// </summary>
public sealed class ParallelRunReport
{
    public int RowCount { get; set; }

    public int SelectedThreads { get; set; }

    public int Repetitions { get; set; }

    public DateTime RunAtUtc { get; set; }

    public string BuildConfiguration { get; set; } = string.Empty;

    public List<TechniqueTiming> Timings { get; set; } = new();

    public List<ReadTiming> ReadTimings { get; set; } = new();

    public List<PurposeGroupStats> Groups { get; set; } = new();

    public double SequentialMedianMs => Timings.FirstOrDefault(t => t.Technique == AnalysisTechnique.Sequential)?.MedianMs ?? 0;

    public IEnumerable<TechniqueTiming> AtSelectedThreads => Timings
        .Where(t => t.Technique == AnalysisTechnique.Sequential || t.Threads == SelectedThreads);

    public double SpeedupOf(TechniqueTiming timing) => timing.MedianMs > 0 ? SequentialMedianMs / timing.MedianMs : 0;

    public bool AllResultsMatch => Timings.All(t => t.ResultMatches);
}

/// <summary>
/// Збережені результати для малої (до 20 записів) і великої вибірки.
/// </summary>
public sealed class ParallelResultsStore
{
    public ParallelRunReport? Small { get; set; }

    public ParallelRunReport? Large { get; set; }
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

    public bool BenchExists { get; set; }

    public int BenchRowCount { get; set; }

    public int SelectedThreads { get; set; }

    public int Repetitions { get; set; } = 10;

    public IReadOnlyList<int> ThreadOptions { get; set; } = Array.Empty<int>();

    public ParallelResultsStore Results { get; set; } = new();

    public ParallelRunReport? Current => BenchRowCount <= ParallelAnalysisLimits.SmallSampleSize ? Results.Small : Results.Large;
}

public static class ParallelAnalysisLimits
{
    public const int SmallSampleSize = 20;

    public const int LargeSampleSize = 100_000;
}
