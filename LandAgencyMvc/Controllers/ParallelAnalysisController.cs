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
/// виконується послідовно й паралельно (PLINQ, Thread, Parallel.ForEach, Task.Run) на малій базі з 20 записами
/// і на робочій базі, доповненій до 100 000 записів.
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
    public async Task<IActionResult> Index(AnalysisDatabase? show)
    {
        var results = _analysis.LoadResults();
        var shown = show ?? (results.For(AnalysisDatabase.Main) != null ? AnalysisDatabase.Main : AnalysisDatabase.Small);
        var model = new ParallelAnalysisViewModel
        {
            CurrentUser = await GetCurrentRealtorAsync(),
            ProcessorCount = Environment.ProcessorCount,
            ProcessorName = ProcessorName.Value,
            RuntimeDescription = RuntimeInformation.FrameworkDescription,
            OsDescription = RuntimeInformation.OSDescription,
            BuildConfiguration = _analysis.BuildConfiguration,
            SmallRowCount = await _analysis.CountSmallRowsAsync(),
            MainRowCount = await _analysis.CountMainRowsAsync(),
            GeneratedRowCount = await _analysis.CountGeneratedRowsAsync(),
            Conditions = await _analysis.GetConditionsAsync(),
            ThreadOptions = _analysis.ThreadOptions,
            SelectedThreads = Environment.ProcessorCount,
            Results = results,
            Shown = shown
        };

        var current = model.ShownReport;
        if (current != null)
        {
            model.SelectedThreads = current.SelectedThreads;
            model.Repetitions = current.Repetitions;
        }

        return View(model);
    }

    /// <summary>
    /// Пункт 2 завдання: окрема мала база, у таблиці LandPlots якої рівно 20 записів, скопійованих із робочої бази.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareSmallDatabase()
    {
        int copied = await _analysis.PrepareSmallDatabaseAsync();
        TempData["RealtorMessage"] = $"Малу базу створено заново: у таблиці LandPlots {copied} записів, скопійованих із робочої бази.";
        return RedirectToAction(nameof(Index), new { show = AnalysisDatabase.Small });
    }

    /// <summary>
    /// Пункт 3 завдання: програмне доповнення таблиці LandPlots робочої бази до 100 000 записів.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FillMainDatabase()
    {
        var (added, elapsed) = await _analysis.FillMainDatabaseAsync();
        int total = await _analysis.CountMainRowsAsync();
        TempData["RealtorMessage"] = added > 0
            ? $"Програмно додано {added:N0} записів за {elapsed.TotalSeconds:F1} с; у таблиці LandPlots робочої бази {total:N0} записів."
            : $"У таблиці LandPlots робочої бази вже {total:N0} записів.";
        return RedirectToAction(nameof(Index), new { show = AnalysisDatabase.Main });
    }

    /// <summary>
    /// Прибирає згенеровані записи, щоб повернути робочу базу до демонстраційного стану ЛР № 4–5.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGenerated()
    {
        int removed = await _analysis.RemoveGeneratedAsync();
        TempData["RealtorMessage"] = removed > 0
            ? $"З робочої бази прибрано {removed:N0} згенерованих записів."
            : "Згенерованих записів у робочій базі немає.";
        return RedirectToAction(nameof(Index), new { show = AnalysisDatabase.Main });
    }

    /// <summary>
    /// Скидає дослідження до кроку 1, щоб пройти всі кроки ще раз.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset()
    {
        int removed = await _analysis.ResetStudyAsync();
        TempData["RealtorMessage"] = removed > 0
            ? $"Дослідження скинуто: прибрано {removed:N0} згенерованих записів, малу базу й результати видалено."
            : "Дослідження скинуто: малу базу й результати видалено.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Виконує запит усіма способами на обраній базі й зберігає результати.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(AnalysisDatabase database, int threads, int repetitions)
    {
        if (database == AnalysisDatabase.Small && await _analysis.CountSmallRowsAsync() is not > 0)
        {
            TempData["RealtorError"] = "Спершу створіть малу базу (крок 1).";
            return RedirectToAction(nameof(Index), new { show = database });
        }

        var report = await _analysis.RunComparisonAsync(database, threads, repetitions);
        TempData["RealtorMessage"] = report.AllResultsMatch
            ? $"Дослідження на {report.RowCount:N0} записах виконано: усі способи дали однаковий результат."
            : $"Дослідження на {report.RowCount:N0} записах виконано, але результати деяких способів відрізняються від послідовного.";
        return RedirectToAction(nameof(Index), new { show = database });
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
