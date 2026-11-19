using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace LandAgencyMvc.Tests;

/// <summary>
/// Наскрізні сценарії через HTTP: застосунок запускається повністю, з власною робочою базою з початкових даних,
/// а запити йдуть так само, як із браузера, з cookie входу й токенами захисту форм.
/// </summary>
public sealed class ParallelAnalysisPageTests : IClassFixture<ParallelAnalysisPageTests.AppFactory>
{
    private readonly AppFactory _factory;

    public ParallelAnalysisPageTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_visitor_is_sent_to_login()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/ParallelAnalysis");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("lawyer@landagency.ua", "lawyer123")]
    [InlineData("buyer@landagency.ua", "client123")]
    public async Task Other_roles_cannot_open_the_page_or_generate_data(string email, string password)
    {
        var client = await LoginAsync(email, password);

        var page = await client.GetAsync("/ParallelAnalysis");
        Assert.Contains("/Account/AccessDenied", page.RequestMessage!.RequestUri!.ToString());

        int before = await _factory.CountMainPlotsAsync();
        var fill = await client.PostAsync("/ParallelAnalysis/FillMainDatabase", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal("/Account/AccessDenied", fill.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal(before, await _factory.CountMainPlotsAsync());
    }

    [Fact]
    public async Task Realtor_walks_through_the_small_database_steps()
    {
        var client = await LoginAsync("realtor@landagency.ua", "realtor123");

        string page = await client.GetStringAsync("/ParallelAnalysis");
        Assert.Contains("Як провести дослідження", page);
        Assert.Contains("Технології розпаралелювання", page);
        Assert.Contains("journal_mode = wal", page);

        string afterPrepare = await PostFormAsync(client, page, "/ParallelAnalysis/PrepareSmallDatabase", new());
        Assert.Contains("у таблиці LandPlots 20 записів", afterPrepare);

        string afterRun = await PostFormAsync(client, afterPrepare, "/ParallelAnalysis/Run", new()
        {
            ["database"] = "Small",
            ["threads"] = "2",
            ["repetitions"] = "3"
        });
        Assert.Contains("Дослідження на 20 записах виконано: усі способи дали однаковий результат.", afterRun);
        Assert.Contains("Запит до бази: медіана часу", afterRun);
        Assert.Contains("Лише обчислення в пам'яті", afterRun);
    }

    [Fact]
    public async Task Realtor_plot_registry_is_paged()
    {
        var client = await LoginAsync("realtor@landagency.ua", "realtor123");

        string page = await client.GetStringAsync("/Realtor/Plots");

        Assert.Matches(new Regex(@"сторінка 1 з \d+"), page);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        string loginPage = await client.GetStringAsync("/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = Token(loginPage)
        }));
        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("/Account/Login", response.RequestMessage!.RequestUri!.AbsolutePath);
        return client;
    }

    private static async Task<string> PostFormAsync(HttpClient client, string pageWithToken, string url, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Token(pageWithToken);
        var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        response.EnsureSuccessStatusCode();
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    /// <summary>
    /// Застосунок із базами в тимчасовій теці, щоб тести не торкалися робочої бази розробника.
    /// </summary>
    public sealed class AppFactory : WebApplicationFactory<Program>
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "landagency-web-" + Guid.NewGuid().ToString("N"));

        public AppFactory()
        {
            Directory.CreateDirectory(_directory);
        }

        private string ConnectionString => $"Data Source={Path.Combine(_directory, "landagency.db")};Foreign Keys=True;Pooling=True";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:LandAgency", ConnectionString);
            builder.UseSetting("Lab6:SmallDatabasePath", Path.Combine(_directory, "small.db"));
            builder.UseSetting("Lab6:ResultsPath", Path.Combine(_directory, "results.json"));
        }

        public async Task<int> CountMainPlotsAsync()
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM LandPlots;";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
