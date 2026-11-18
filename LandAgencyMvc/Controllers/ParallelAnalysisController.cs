using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Claims;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Controllers;

/// <summary>
/// ЛР6 «Паралельна обробка даних»: сторінка рієлтора, на якій один і той самий запит аудиту земельного банку
/// виконується послідовно й паралельно (PLINQ, Thread, Parallel.ForEach, Task) на 20 і на 100 000 записах.
/// </summary>
[Authorize(Roles = AppRoles.Realtor)]
public class ParallelAnalysisController : Controller
{
    private static readonly Lazy<string> ProcessorName = new(DetectProcessorName);

    private readonly LandAgencyContext _db;
    private readonly IParallelAnalysisService _analysis;

    public ParallelAnalysisController(LandAgencyContext db, IParallelAnalysisService analysis)
    {
        _db = db;
        _analysis = analysis;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var results = _analysis.LoadResults();
        var model = new ParallelAnalysisViewModel
        {
            CurrentUser = await GetCurrentRealtorAsync(),
            ProcessorCount = Environment.ProcessorCount,
            ProcessorName = ProcessorName.Value,
            RuntimeDescription = RuntimeInformation.FrameworkDescription,
            OsDescription = RuntimeInformation.OSDescription,
            BuildConfiguration = _analysis.BuildConfiguration,
            BenchExists = _analysis.BenchExists,
            BenchRowCount = await _analysis.CountBenchRowsAsync(),
            ThreadOptions = _analysis.ThreadOptions,
            SelectedThreads = Environment.ProcessorCount,
            Results = results
        };

        var current = model.Current;
        if (current != null)
        {
            model.SelectedThreads = current.SelectedThreads;
            model.Repetitions = current.Repetitions;
        }

        return View(model);
    }

    /// <summary>
    /// Мала вибірка: база аналізу створюється заново, у таблицю LandPlots копіюються 20 ділянок робочої бази.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareSmallSample()
    {
        int copied = await _analysis.PrepareSmallSampleAsync(_db);
        TempData["RealtorMessage"] = $"Базу аналізу створено заново: у таблиці LandPlots {copied} ділянок, скопійованих із робочої бази.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Завдання 3 ЛР6: програмне доповнення таблиці LandPlots бази аналізу до 100 000 записів.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateLargeSample()
    {
        var (added, elapsed) = await _analysis.GenerateLargeSampleAsync(_db);
        int total = await _analysis.CountBenchRowsAsync();
        TempData["RealtorMessage"] = added > 0
            ? $"Програмно додано {added:N0} ділянок за {elapsed.TotalSeconds:F1} с; у таблиці LandPlots бази аналізу {total:N0} записів."
            : $"У таблиці LandPlots бази аналізу вже {total:N0} записів.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Виконує запит усіма способами на поточному обсязі даних і зберігає результати.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(int threads, int repetitions)
    {
        if (!_analysis.BenchExists)
        {
            TempData["RealtorError"] = "Спершу підготуйте малу вибірку або згенеруйте 100 000 записів.";
            return RedirectToAction(nameof(Index));
        }

        var report = await _analysis.RunComparisonAsync(threads, repetitions);
        TempData["RealtorMessage"] = report.AllResultsMatch
            ? $"Порівняння на {report.RowCount:N0} записах виконано: усі способи дали однаковий результат."
            : $"Порівняння на {report.RowCount:N0} записах виконано, але результати деяких способів відрізняються від послідовного.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AppUser?> GetCurrentRealtorAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) && id > 0
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            : null;
    }

    private static string DetectProcessorName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("sysctl", "-n machdep.cpu.brand_string")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                var name = process?.StandardOutput.ReadToEnd().Trim();
                process?.WaitForExit();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return RuntimeInformation.ProcessArchitecture.ToString();
            }
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }
}
