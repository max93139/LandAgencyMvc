using System.Security.Claims;
using LandAgencyMvc.Controllers;
using LandAgencyMvc.Models;
using LandAgencyMvc.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LandAgencyMvc.Tests;

public class ControllerWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LandAgencyContext _context;
    private readonly CadastralValidationService _cadastralService;
    private readonly AutoMatchingService _autoMatchingService;

    public ControllerWorkflowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LandAgencyContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LandAgencyContext(options);
        _context.InitializeDatabase();

        _cadastralService = new CadastralValidationService(_context);
        _autoMatchingService = new AutoMatchingService(_context);
    }

    private static void AttachUserContext(Controller controller, AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider());
    }

    [Fact]
    public async Task LawyerController_SubmitLegalConclusion_BlocksStatusChange_OnSoldPlot()
    {
        var lawyer = await _context.Users.FirstAsync(u => u.Role == UserRole.Lawyer);
        var soldPlot = await _context.LandPlots.FirstAsync(p => p.Id == 2);
        soldPlot.Status = PlotStatus.Sold;
        await _context.SaveChangesAsync();

        var controller = new LawyerController(_context, _cadastralService, _autoMatchingService);
        AttachUserContext(controller, lawyer);

        var result = await controller.SubmitLegalConclusion(
            soldPlot.Id,
            "ЮВ-2026-999",
            "Високий",
            false, false, false, false, false,
            "Спроба відхилити вже продану ділянку",
            approve: false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Queue", redirect.ActionName);
        Assert.Equal(PlotStatus.Sold, soldPlot.Status);
        Assert.Contains("вже продано", controller.TempData["LawyerError"]?.ToString());
    }

    [Fact]
    public async Task LawyerController_SubmitLegalConclusion_Reject_CancelsPendingViewings_And_RemovesFromMatchedRequests()
    {
        var lawyer = await _context.Users.FirstAsync(u => u.Role == UserRole.Lawyer);
        var verifiedPlot = await _context.LandPlots.FirstAsync(p => p.Id == 1 && p.Status == PlotStatus.VerifiedInCatalog);
        var request = await _context.SelectionRequests.FirstAsync();

        Assert.Contains(verifiedPlot.Id, request.GetMatchedPlotIds());

        var controller = new LawyerController(_context, _cadastralService, _autoMatchingService);
        AttachUserContext(controller, lawyer);

        var result = await controller.SubmitLegalConclusion(
            verifiedPlot.Id,
            "ЮВ-2026-101-R",
            "Високий (Виявлено обтяження / аномалію)",
            false, true, true, true, true,
            "Виявлено арешт ДРРП під час повторної перевірки",
            approve: false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Queue", redirect.ActionName);
        Assert.Equal(PlotStatus.RejectedByLawyer, verifiedPlot.Status);

        var updatedRequest = await _context.SelectionRequests.FindAsync(request.Id);
        Assert.NotNull(updatedRequest);
        Assert.DoesNotContain(verifiedPlot.Id, updatedRequest!.GetMatchedPlotIds());
        Assert.Equal(0, updatedRequest.MatchedPlotsCount);

        var scheduledViewing = await _context.ViewingRequests.FirstAsync(v => v.LandPlotId == verifiedPlot.Id);
        Assert.Equal(ViewingStatus.Cancelled, scheduledViewing.Status);
    }

    [Fact]
    public async Task LawyerController_ApproveDealContract_BlocksChanges_OnCompletedOrSignedDeal()
    {
        var lawyer = await _context.Users.FirstAsync(u => u.Role == UserRole.Lawyer);
        var deal = await _context.SaleDeals.FirstAsync();
        deal.Status = DealStatus.CompletedAtNotary;
        await _context.SaveChangesAsync();

        var controller = new LawyerController(_context, _cadastralService, _autoMatchingService);
        AttachUserContext(controller, lawyer);

        var result = await controller.ApproveDealContract(deal.Id, "Спроба повернути завершену угоду", "return");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Contracts", redirect.ActionName);
        Assert.Equal(DealStatus.CompletedAtNotary, deal.Status);
        Assert.Contains("не підлягає зміні правової візи", controller.TempData["LawyerError"]?.ToString());
    }

    [Fact]
    public async Task RealtorController_RejectClientPayment_BlocksRejection_OnCompletedDeal()
    {
        var realtor = await _context.Users.FirstAsync(u => u.Role == UserRole.Realtor);
        var deal = await _context.SaleDeals
            .Include(d => d.Payments)
            .FirstAsync();
        var confirmedPayment = deal.Payments.First();
        confirmedPayment.VerifyPayment(realtor.Id, "Підтверджено для тесту");
        deal.Status = DealStatus.CompletedAtNotary;
        await _context.SaveChangesAsync();

        var controller = new RealtorController(_context, _autoMatchingService);
        AttachUserContext(controller, realtor);

        var result = await controller.RejectClientPayment(confirmedPayment.Id, "Спроба скасувати оплату після ДРРП");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Deals", redirect.ActionName);
        Assert.Equal(PaymentStatus.Confirmed, confirmedPayment.Status);
        Assert.True(confirmedPayment.IsPaid);
        Assert.Contains("вже зареєстровано право власності в ДРРП", controller.TempData["RealtorError"]?.ToString());
    }

    [Fact]
    public async Task ClientController_SubmitPayment_BlocksSubmission_WhenDealCancelledOrPaymentAlreadyConfirmed()
    {
        var buyer = await _context.Users.FirstAsync(u => u.Email == "buyer@landagency.ua");
        var realtor = await _context.Users.FirstAsync(u => u.Role == UserRole.Realtor);
        var plot = await _context.LandPlots.FirstAsync(p => p.Id == 1);

        var deal = new SaleDeal
        {
            LandPlotId = plot.Id,
            BuyerId = buyer.Id,
            RealtorId = realtor.Id,
            NotaryOffice = "Тестова нотаріальна контора",
            NotaryDate = DateTime.Today.AddDays(2)
        };
        deal.PrepareContractDraft("ДГ-2026-TEST", plot.PriceUsd, 3.0);
        _context.SaleDeals.Add(deal);
        await _context.SaveChangesAsync();

        var payments = deal.CreateDefaultPayments(plot.SellerBankIban, isPaid: false, senderIban: buyer.BankDetails, agencyIban: realtor.BankDetails);
        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync();

        deal.CancelDeal("Скасовано для тесту");
        await _context.SaveChangesAsync();

        var controller = new ClientController(_context, new FakeWebHostEnvironment(), _cadastralService, _autoMatchingService);
        AttachUserContext(controller, buyer);

        var firstPayment = payments.First();
        Assert.False(firstPayment.CanSubmitByClient);

        await controller.SubmitPayment(firstPayment.Id, "TRX-CANCELLED-999", buyer.BankDetails);

        Assert.Equal(PaymentStatus.PendingSubmission, firstPayment.Status);
        Assert.Equal(string.Empty, firstPayment.PaymentReference);
        Assert.Contains("Угоду скасовано", controller.TempData["CabinetError"]?.ToString());
    }

    [Fact]
    public async Task ClientController_UpdatePlotPrice_SyncsAutoMatching_And_BlocksSoldPlots()
    {
        var seller = await _context.Users.FirstAsync(u => u.Email == "seller@landagency.ua");
        var plot1 = await _context.LandPlots.FirstAsync(p => p.Id == 1 && p.SellerId == seller.Id);
        var soldPlot = await _context.LandPlots.FirstAsync(p => p.Id == 2 && p.SellerId == seller.Id);
        soldPlot.Status = PlotStatus.Sold;
        await _context.SaveChangesAsync();

        var request = await _context.SelectionRequests.FirstAsync();
        Assert.Contains(plot1.Id, request.GetMatchedPlotIds());

        var controller = new ClientController(_context, new FakeWebHostEnvironment(), _cadastralService, _autoMatchingService);
        AttachUserContext(controller, seller);

        await controller.UpdatePlotPrice(plot1.Id, 95000m);
        Assert.Equal(95000m, plot1.PriceUsd);
        Assert.DoesNotContain(plot1.Id, request.GetMatchedPlotIds());

        decimal originalSoldPrice = soldPlot.PriceUsd;
        await controller.UpdatePlotPrice(soldPlot.Id, 50000m);
        Assert.Equal(originalSoldPrice, soldPlot.PriceUsd);
        Assert.Contains("вже проданої", controller.TempData["CabinetError"]?.ToString());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _data = new();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _data;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _data.Clear();
            foreach (var kv in values)
            {
                _data[kv.Key] = kv.Value;
            }
        }
    }

    private sealed class FakeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "LandAgencyMvc.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}
