using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Tests;

/// <summary>
/// Сценарії ЛР6 на справжніх файлах SQLite: доповнення робочої бази, мала база, паралельний запит до бази
/// і прибирання згенерованих записів.
/// </summary>
public class ParallelAnalysisServiceTests
{
    [Fact]
    public async Task Fill_adds_exactly_up_to_target_and_does_not_duplicate()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();

        var (added, _) = await service.FillMainDatabaseAsync(targetRows: 3_000);
        var (addedAgain, _) = await service.FillMainDatabaseAsync(targetRows: 3_000);

        Assert.Equal(2_978, added);
        Assert.Equal(0, addedAgain);
        Assert.Equal(3_000, await service.CountMainRowsAsync());
        Assert.Equal(2_978, await service.CountGeneratedRowsAsync());

        await using var db = database.CreateContext();
        Assert.Equal(3_000, await db.LandPlots.Select(p => p.CadastralNumber).Distinct().CountAsync());
        Assert.All(await db.LandPlots.Where(p => p.Seller!.Email == ParallelAnalysisService.LoadSellerEmail).Select(p => p.Status).Distinct().ToListAsync(),
            status => Assert.Equal(PlotStatus.Draft, status));
        Assert.False(await db.Users.Where(u => u.Email == ParallelAnalysisService.LoadSellerEmail).Select(u => u.IsActive).SingleAsync());
    }

    [Fact]
    public async Task Generated_rows_never_reach_the_catalog()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.FillMainDatabaseAsync(targetRows: 2_000);

        await using var db = database.CreateContext();
        Assert.Equal(22, await db.LandPlots.CountAsync(p => p.Status == PlotStatus.VerifiedInCatalog));
    }

    [Fact]
    public async Task Small_database_holds_exactly_20_demo_rows()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.FillMainDatabaseAsync(targetRows: 1_000);

        int copied = await service.PrepareSmallDatabaseAsync();

        Assert.Equal(ParallelAnalysisLimits.SmallSampleSize, copied);
        Assert.Equal(ParallelAnalysisLimits.SmallSampleSize, await service.CountSmallRowsAsync());
        Assert.Equal(1_000, await service.CountMainRowsAsync());
    }

    [Fact]
    public async Task Every_technique_on_the_database_gives_the_sequential_result()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.FillMainDatabaseAsync(targetRows: 4_000);
        Func<LandAgencyContext> create = database.CreateContext;

        var expected = await LandBankQuery.RunOnDatabaseAsync(AnalysisTechnique.Sequential, create, 1);
        var inMemory = LandBankQuery.RunSequential(LandBankQuery.LoadRows(create));
        Assert.True(LandBankQuery.SameResult(expected, inMemory));
        Assert.Equal(4_000, expected.Sum(g => g.Count));

        foreach (var technique in TechniqueTiming.ParallelTechniques)
        {
            foreach (int threads in new[] { 1, 2, 3, 7, 18 })
            {
                var actual = await LandBankQuery.RunOnDatabaseAsync(technique, create, threads);
                Assert.True(LandBankQuery.SameResult(expected, actual), $"{technique} на {threads} потоках");
            }
        }
    }

    [Fact]
    public async Task Comparison_on_small_database_measures_all_techniques_with_matching_results()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.PrepareSmallDatabaseAsync();

        var report = await service.RunComparisonAsync(AnalysisDatabase.Small, selectedThreads: 4, repetitions: 3);

        Assert.Equal(20, report.RowCount);
        Assert.True(report.AllResultsMatch);
        int degrees = service.ThreadOptions.Append(4).Distinct().Count();
        Assert.Equal(1 + 4 * degrees, report.DatabaseQuery.Timings.Count);
        Assert.Equal(1 + 4 * degrees, report.MemoryOnly.Timings.Count);
        Assert.NotNull(service.LoadResults().For(AnalysisDatabase.Small));
        Assert.Null(service.LoadResults().For(AnalysisDatabase.Main));
    }

    [Fact]
    public async Task Remove_generated_returns_the_database_to_demo_state()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.FillMainDatabaseAsync(targetRows: 1_500);

        int removed = await service.RemoveGeneratedAsync();

        Assert.Equal(1_478, removed);
        Assert.Equal(22, await service.CountMainRowsAsync());
        Assert.Equal(0, await service.CountGeneratedRowsAsync());
        await using var db = database.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == ParallelAnalysisService.LoadSellerEmail));
    }

    [Fact]
    public async Task Reset_returns_the_study_to_step_one()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.PrepareSmallDatabaseAsync();
        await service.RunComparisonAsync(AnalysisDatabase.Small, selectedThreads: 2, repetitions: 1);
        await service.FillMainDatabaseAsync(targetRows: 1_200);

        int removed = await service.ResetStudyAsync();

        Assert.Equal(1_178, removed);
        Assert.Equal(22, await service.CountMainRowsAsync());
        Assert.Null(await service.CountSmallRowsAsync());
        Assert.Null(service.LoadResults().For(AnalysisDatabase.Small));
        Assert.Null(service.LoadResults().For(AnalysisDatabase.Main));

        Assert.Equal(ParallelAnalysisLimits.SmallSampleSize, await service.PrepareSmallDatabaseAsync());
    }

    [Fact]
    public async Task Every_technique_gives_the_sequential_result_when_ids_have_gaps()
    {
        using var database = new TestDatabase(demoPlots: 22);
        var service = database.CreateService();
        await service.FillMainDatabaseAsync(targetRows: 3_000);
        await service.RemoveGeneratedAsync();
        await service.FillMainDatabaseAsync(targetRows: 3_000);
        Func<LandAgencyContext> create = database.CreateContext;

        var chunks = LandBankQuery.ReadChunks(create, threads: 4);
        await using (var db = database.CreateContext())
        {
            int covered = 0;
            foreach (var chunk in chunks)
            {
                covered += await db.LandPlots.CountAsync(p => p.Id >= chunk.From && p.Id <= chunk.To);
            }

            Assert.Equal(4 * LandBankQuery.ChunksPerThread, chunks.Count);
            Assert.Equal(3_000, covered);
        }

        var expected = await LandBankQuery.RunOnDatabaseAsync(AnalysisTechnique.Sequential, create, 1);
        foreach (var technique in TechniqueTiming.ParallelTechniques)
        {
            foreach (int threads in new[] { 2, 4, 10 })
            {
                var actual = await LandBankQuery.RunOnDatabaseAsync(technique, create, threads);
                Assert.True(LandBankQuery.SameResult(expected, actual), $"{technique} на {threads} потоках");
            }
        }
    }

    [Fact]
    public async Task Database_conditions_report_wal_pooling_and_thread_pool()
    {
        using var database = new TestDatabase(demoPlots: 1);
        var service = database.CreateService();

        var conditions = await service.GetConditionsAsync();

        Assert.True(conditions.IsWal);
        Assert.True(conditions.PoolingEnabled);
        Assert.True(conditions.MinWorkerThreads >= conditions.RequiredWorkerThreads);
    }
}
