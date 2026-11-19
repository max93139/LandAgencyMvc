using System.Collections.Concurrent;
using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

/// <summary>
/// Запит ЛР6 «Аудит земельного банку за цільовим призначенням» у двох режимах.
/// Повний запит до бази: діапазон Id ділиться на дрібні частини, кожен потік відкриває власний DbContext
/// і власне з'єднання, бере наступну вільну частину, читає її рядки й додає до власного підсумку, а наприкінці
/// підсумки потоків зливаються. Паралельно виконуються і читання з SQLite, і перетворення рядків на об'єкти,
/// і обчислення.
/// Лише обчислення: та сама обробка над рядками, уже прочитаними в пам'ять.
/// </summary>
public static class LandBankQuery
{
    /// <summary>
    /// Скільки частин припадає на один потік. Частини дрібні, тому потік, що впорався раніше, бере наступну,
    /// і робота розподіляється рівно навіть тоді, коли в Id є прогалини після вилучень: порожня частина
    /// коштує одного короткого запиту за первинним ключем.
    /// </summary>
    public const int ChunksPerThread = 8;

    /// <summary>
    /// Ділить діапазон [minId; maxId] на частини без пропусків і перекриттів. Частин не більше, ніж ключів.
    /// </summary>
    public static IReadOnlyList<IdRange> SplitIdRange(int minId, int maxId, int parts)
    {
        if (maxId < minId)
        {
            return Array.Empty<IdRange>();
        }

        long total = (long)maxId - minId + 1;
        int count = (int)Math.Clamp(parts, 1, total);
        long size = total / count;
        long remainder = total % count;

        var ranges = new IdRange[count];
        long from = minId;
        for (int i = 0; i < count; i++)
        {
            long length = size + (i < remainder ? 1 : 0);
            ranges[i] = new IdRange((int)from, (int)(from + length - 1));
            from += length;
        }

        return ranges;
    }

    /// <summary>
    /// Рядки запиту: з бази беруться лише поля, потрібні для аудиту, без відстеження змін.
    /// </summary>
    public static IQueryable<PlotAnalysisRow> Rows(LandAgencyContext db, IdRange? range = null)
    {
        var plots = db.LandPlots.AsNoTracking();
        if (range is { } r)
        {
            plots = plots.Where(p => p.Id >= r.From && p.Id <= r.To);
        }

        return plots.Select(p => new PlotAnalysisRow(
            p.CadastralNumber, p.Purpose, p.AreaHectares, p.PriceUsd, p.SellerBankIban, p.Latitude, p.Longitude));
    }

    public static PlotAnalysisRow[] LoadRows(Func<LandAgencyContext> createContext)
    {
        using var db = createContext();
        return Rows(db).ToArray();
    }

    /// <summary>
    /// Частини діапазону Id для заданої кількості потоків. Межі беруться з індексу первинного ключа двома
    /// короткими запитами MIN і MAX.
    /// </summary>
    public static IReadOnlyList<IdRange> ReadChunks(Func<LandAgencyContext> createContext, int threads)
    {
        using var db = createContext();
        int? minId = db.LandPlots.Min(p => (int?)p.Id);
        int? maxId = db.LandPlots.Max(p => (int?)p.Id);
        return minId.HasValue && maxId.HasValue
            ? SplitIdRange(minId.Value, maxId.Value, threads * ChunksPerThread)
            : Array.Empty<IdRange>();
    }

    /// <summary>
    /// Повний запит до бази заданим способом на заданій кількості потоків.
    /// </summary>
    public static async Task<List<PurposeGroupStats>> RunOnDatabaseAsync(
        AnalysisTechnique technique, Func<LandAgencyContext> createContext, int threads)
    {
        if (technique == AnalysisTechnique.Sequential)
        {
            using var db = createContext();
            var accumulator = new LandBankAccumulator();
            foreach (var row in Rows(db))
            {
                accumulator.Add(row);
            }

            return accumulator.ToResult();
        }

        var chunks = ReadChunks(createContext, threads);
        var total = new LandBankAccumulator();
        var sync = new object();
        int next = -1;

        void MergeIntoTotal(LandBankAccumulator partial)
        {
            lock (sync)
            {
                total.Merge(partial);
            }
        }

        void Work()
        {
            using var worker = new ChunkReader(createContext);
            int i;
            while ((i = Interlocked.Increment(ref next)) < chunks.Count)
            {
                worker.Read(chunks[i]);
            }

            MergeIntoTotal(worker.Accumulator);
        }

        switch (technique)
        {
            case AnalysisTechnique.Plinq:
                var plinqResult = OneAtATime(chunks.Count)
                    .AsParallel()
                    .WithDegreeOfParallelism(threads)
                    .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                    .Aggregate(
                        () => new ChunkReader(createContext),
                        (worker, i) => { worker.Read(chunks[i]); return worker; },
                        (left, right) => { left.Accumulator.Merge(right.Accumulator); right.Dispose(); return left; },
                        worker => { worker.Dispose(); return worker.Accumulator; });
                MergeIntoTotal(plinqResult);
                break;

            case AnalysisTechnique.Threads:
                var workers = new Thread[threads];
                for (int t = 0; t < threads; t++)
                {
                    workers[t] = new Thread(Work);
                    workers[t].Start();
                }

                foreach (var worker in workers)
                {
                    worker.Join();
                }

                break;

            case AnalysisTechnique.ParallelForEach:
                Parallel.ForEach(
                    OneAtATime(chunks.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = threads },
                    () => new ChunkReader(createContext),
                    (i, _, worker) => { worker.Read(chunks[i]); return worker; },
                    worker => { worker.Dispose(); MergeIntoTotal(worker.Accumulator); });
                break;

            case AnalysisTechnique.Tasks:
                await Task.WhenAll(Enumerable.Range(0, threads).Select(_ => Task.Run(async () =>
                {
                    await using var worker = new ChunkReader(createContext);
                    int i;
                    while ((i = Interlocked.Increment(ref next)) < chunks.Count)
                    {
                        await worker.ReadAsync(chunks[i]);
                    }

                    MergeIntoTotal(worker.Accumulator);
                })));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(technique));
        }

        return total.ToResult();
    }

    /// <summary>
    /// Номери частин, які видаються потокам по одній. Стандартний розподільник PLINQ і Parallel.ForEach
    /// видає елементи пачками, що зростають, і один потік міг би забрати кілька частин наперед, поки інші чекають.
    /// </summary>
    private static OrderablePartitioner<int> OneAtATime(int count) =>
        Partitioner.Create(Enumerable.Range(0, count), EnumerablePartitionerOptions.NoBuffering);

    /// <summary>
    /// Робоче місце одного потоку: власний DbContext (його не можна ділити між потоками) і власний підсумок,
    /// тому під час читання й обчислення потоки нічого не блокують.
    /// </summary>
    private sealed class ChunkReader : IDisposable, IAsyncDisposable
    {
        private readonly LandAgencyContext _db;

        public ChunkReader(Func<LandAgencyContext> createContext)
        {
            _db = createContext();
        }

        public LandBankAccumulator Accumulator { get; } = new();

        public void Read(IdRange range)
        {
            foreach (var row in Rows(_db, range))
            {
                Accumulator.Add(row);
            }
        }

        /// <summary>
        /// Асинхронне читання для Task.Run: поки SQLite читає рядки, потік не блокується й повертається в пул.
        /// </summary>
        public async Task ReadAsync(IdRange range)
        {
            await foreach (var row in Rows(_db, range).AsAsyncEnumerable())
            {
                Accumulator.Add(row);
            }
        }

        public void Dispose() => _db.Dispose();

        public ValueTask DisposeAsync() => _db.DisposeAsync();
    }

    /// <summary>
    /// Лише обчислення над рядками в пам'яті заданим способом.
    /// </summary>
    public static List<PurposeGroupStats> RunInMemory(AnalysisTechnique technique, PlotAnalysisRow[] rows, int threads) => technique switch
    {
        AnalysisTechnique.Plinq => RunPlinq(rows, threads),
        AnalysisTechnique.Threads => RunThreads(rows, threads),
        AnalysisTechnique.ParallelForEach => RunParallelForEach(rows, threads),
        AnalysisTechnique.Tasks => RunTasks(rows, threads),
        _ => RunSequential(rows)
    };

    /// <summary>
    /// Послідовне виконання в одному потоці.
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
    /// Діапазонів учетверо більше, ніж потоків, тож потік, що звільнився раніше, бере наступний.
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

    public static bool SameResult(List<PurposeGroupStats> expected, List<PurposeGroupStats> actual) =>
        expected.Count == actual.Count && expected.Zip(actual).All(pair => pair.First.SameAs(pair.Second));

    private static List<PurposeGroupStats> MergeAll(IEnumerable<LandBankAccumulator> partials)
    {
        var total = new LandBankAccumulator();
        foreach (var partial in partials)
        {
            total.Merge(partial);
        }

        return total.ToResult();
    }
}
