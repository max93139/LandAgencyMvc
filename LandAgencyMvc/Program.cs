using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using LandAgencyMvc.Data;
using LandAgencyMvc.Filters;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

builder.Services.AddControllersWithViews(options => options.Filters.Add<DatabaseExceptionFilter>());
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});
builder.Services.AddDbContext<LandAgencyContext>(options => options
    .UseSqlite("Data Source=landagency.db;Foreign Keys=True")
    .AddInterceptors(new SqliteDurabilityInterceptor()));

builder.Services.AddScoped<ICadastralValidationService, CadastralValidationService>();
builder.Services.AddScoped<IAutoMatchingService, AutoMatchingService>();
builder.Services.AddScoped<ILandAgencyQueries, LandAgencyQueries>();
builder.Services.AddScoped<IPlotMediaStorage, PlotMediaStorage>();
builder.Services.AddScoped<IPlotRemovalService, PlotRemovalService>();
builder.Services.AddSingleton<IParallelAnalysisService, ParallelAnalysisService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "LandAgency.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

bool seeded;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LandAgencyContext>();
    seeded = db.InitializeDatabase();
}

if (seeded)
{
    // Збіги заявок у початкових даних узгоджуються з поточними правилами підбору: продані й неопубліковані ділянки прибираються.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LandAgencyContext>();
    var matching = scope.ServiceProvider.GetRequiredService<IAutoMatchingService>();
    foreach (var plotId in db.LandPlots.Select(p => p.Id).ToList())
    {
        await matching.MatchNewOrUpdatedPlotAsync(plotId);
    }
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = new List<CultureInfo> { CultureInfo.InvariantCulture },
    SupportedUICultures = new List<CultureInfo> { CultureInfo.InvariantCulture }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();