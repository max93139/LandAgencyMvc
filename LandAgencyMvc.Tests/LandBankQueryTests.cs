using LandAgencyMvc.Models;
using LandAgencyMvc.Services;

namespace LandAgencyMvc.Tests;

/// <summary>
/// Запит ЛР6 без бази: ділення діапазону Id, перевірки реквізитів і однаковий результат обчислень у пам'яті.
/// </summary>
public class LandBankQueryTests
{
    public static TheoryData<int, int, int> Ranges => new()
    {
        { 1, 100_000, 80 },
        { 1, 20, 144 },
        { 5, 7, 18 },
        { 1, 1, 4 },
        { 23, 299_978, 16 },
        { 1, 10, 3 }
    };

    [Theory]
    [MemberData(nameof(Ranges))]
    public void SplitIdRange_covers_every_key_once(int minId, int maxId, int parts)
    {
        var ranges = LandBankQuery.SplitIdRange(minId, maxId, parts);

        Assert.Equal(Math.Min(parts, maxId - minId + 1), ranges.Count);
        Assert.Equal(minId, ranges[0].From);
        Assert.Equal(maxId, ranges[^1].To);
        for (int i = 1; i < ranges.Count; i++)
        {
            Assert.Equal(ranges[i - 1].To + 1, ranges[i].From);
        }

        int sizeDifference = ranges.Max(r => r.To - r.From) - ranges.Min(r => r.To - r.From);
        Assert.True(sizeDifference <= 1, "частини мають бути майже однакові");
    }

    [Fact]
    public void SplitIdRange_of_empty_table_is_empty()
    {
        Assert.Empty(LandBankQuery.SplitIdRange(10, 9, 4));
    }

    [Theory]
    [InlineData("3222486200:01:002:1111", true)]
    [InlineData(" 3222486200:01:002:1111 ", true)]
    [InlineData("3222486200:01:002-1111", false)]
    [InlineData("3222486200:01:02:1111", false)]
    [InlineData("32224862A0:01:002:1111", false)]
    [InlineData(null, false)]
    public void Cadastral_number_format_is_checked(string? number, bool valid)
    {
        Assert.Equal(valid, LandPlot.IsValidCadastralFormat(number));
    }

    [Fact]
    public void Iban_checksum_detects_a_changed_digit()
    {
        string iban = LandBankAudit.BuildUaIban("3052990000026007233566001");
        string broken = iban[..10] + (iban[10] == '9' ? '0' : (char)(iban[10] + 1)) + iban[11..];

        Assert.True(LandBankAudit.IsValidUaIban(iban));
        Assert.False(LandBankAudit.IsValidUaIban(broken));
        Assert.False(LandBankAudit.IsValidUaIban("UA21"));
    }

    [Fact]
    public void Every_technique_in_memory_gives_the_sequential_result()
    {
        var rows = SampleRows(5_000);
        var expected = LandBankQuery.RunSequential(rows);

        foreach (var technique in TechniqueTiming.ParallelTechniques)
        {
            foreach (int threads in new[] { 1, 2, 3, 7, 18 })
            {
                var actual = LandBankQuery.RunInMemory(technique, rows, threads);
                Assert.True(LandBankQuery.SameResult(expected, actual), $"{technique} на {threads} потоках");
            }
        }
    }

    [Fact]
    public void Accumulator_counts_invalid_details_and_groups_by_purpose_code()
    {
        var rows = new[]
        {
            new PlotAnalysisRow("3222486200:01:002:1111", "02.01 Житло", 0.1, 1_000m, LandBankAudit.BuildUaIban("3052990000026007233566001"), 50.45, 30.52),
            new PlotAnalysisRow("bad", "02.01 Житло (інша назва)", 0.2, 4_000m, "UA00", null, null),
            new PlotAnalysisRow("3222486200:01:002:1112", "01.03 ОСГ", 1.0, 10_000m, "UA00", 50.45, 30.52)
        };

        var result = LandBankQuery.RunSequential(rows);

        var housing = Assert.Single(result, g => g.PurposeCode == "02.01");
        Assert.Equal(2, housing.Count);
        Assert.Equal(1, housing.InvalidCadastralCount);
        Assert.Equal(1, housing.InvalidIbanCount);
        Assert.Equal(200m, housing.MaxPricePerSotkaUsd);
        Assert.Equal(1, housing.WithCoordinatesCount);
        Assert.Equal(2, result.Count);
    }

    private static PlotAnalysisRow[] SampleRows(int count)
    {
        var random = new Random(7);
        var purposes = LandPlot.StandardPurposes.Select(p => p.FullValue).ToArray();
        return Enumerable.Range(0, count).Select(i => new PlotAnalysisRow(
            random.NextDouble() < 0.05 ? "broken" : $"3222486200:90:{i / 10_000:D3}:{i % 10_000:D4}",
            purposes[random.Next(purposes.Length)],
            Math.Round(0.05 + random.NextDouble() * 2, 4),
            Math.Round((decimal)(1_000 + random.NextDouble() * 50_000), 0),
            LandBankAudit.BuildUaIban($"305299{i:D19}"),
            random.NextDouble() < 0.9 ? 50 + random.NextDouble() : null,
            30 + random.NextDouble())).ToArray();
    }
}
