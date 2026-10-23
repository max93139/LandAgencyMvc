using System.Text;
using LandAgencyMvc.Models;
using Xunit;

namespace LandAgencyMvc.Tests;

public class PlotDocumentTests
{
    [Fact]
    public void AttachFile_CalculatesValidSha256ChecksumAndProperties()
    {
        var doc = new PlotDocument
        {
            DocType = "Витяг з ДРРП",
            DocumentNumber = "DRRP-1002"
        };

        byte[] sampleBytes = Encoding.UTF8.GetBytes("Test land title document content for SHA256 integrity check");
        doc.AttachFile("/uploads/test_doc.pdf", sampleBytes, "application/pdf");

        Assert.Equal("/uploads/test_doc.pdf", doc.FileUrl);
        Assert.Equal("application/pdf", doc.MimeType);
        Assert.Equal(sampleBytes.Length, doc.FileSizeBytes);
        Assert.NotNull(doc.FileHash);
        Assert.Equal(64, doc.FileHash.Length);
        Assert.True(doc.VerifyFileIntegrity(sampleBytes));
    }

    [Fact]
    public void VerifyFileIntegrity_ReturnsFalse_WhenContentIsTampered()
    {
        var doc = new PlotDocument();
        byte[] original = Encoding.UTF8.GetBytes("Original authentic file bytes");
        byte[] tampered = Encoding.UTF8.GetBytes("Tampered modified file bytes");

        doc.AttachFile("/uploads/authentic.pdf", original, "application/pdf");

        Assert.True(doc.VerifyFileIntegrity(original));
        Assert.False(doc.VerifyFileIntegrity(tampered));
    }

    [Fact]
    public void ApplyKepSignature_SetsSignatureMetadata()
    {
        var doc = new PlotDocument();

        doc.ApplyKepSignature("Петренко Петро Петрович", "2987654321");

        Assert.True(doc.IsSignedByKep);
        Assert.Equal("Петренко Петро Петрович", doc.SignerFullName);
        Assert.Equal("2987654321", doc.SignerTaxId);
        Assert.NotNull(doc.SignedAt);
        Assert.True((DateTime.UtcNow - doc.SignedAt.Value).TotalMinutes < 1);
    }

    [Fact]
    public void ShortFileHash_And_FileSizeKb_HandleEmptyAndPopulatedValues()
    {
        var emptyDoc = new PlotDocument { FileHash = string.Empty, FileSizeBytes = 0 };
        Assert.Equal("—", emptyDoc.ShortFileHash);
        Assert.Equal(0.0, emptyDoc.FileSizeKb);

        var shortDoc = new PlotDocument { FileHash = "abc12345", FileSizeBytes = 2048 };
        Assert.Equal("abc12345", shortDoc.ShortFileHash);
        Assert.Equal(2.0, shortDoc.FileSizeKb);

        var fullHashDoc = new PlotDocument
        {
            FileHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            FileSizeBytes = 15360
        };
        Assert.Equal("0123456789abcdef...", fullHashDoc.ShortFileHash);
        Assert.Equal(15.0, fullHashDoc.FileSizeKb);
    }

    [Fact]
    public void LandPlot_ComputedProperties_CalculateSotkyPriceUrlsAndIban()
    {
        var plot = new LandPlot
        {
            CadastralNumber = "3222481201:01:005:0042",
            AreaHectares = 0.15,
            PriceUsd = 30000m,
            SellerBankIban = string.Empty,
            Seller = new AppUser { BankDetails = "UA213223130000026007233566001" }
        };

        Assert.Equal(15.0, plot.AreaSotky);
        Assert.Equal(2000m, plot.PricePerSotkaUsd);
        Assert.Contains("3222481201%3A01%3A005%3A0042", plot.KadastrLiveUrl);
        Assert.Contains("3222481201%3A01%3A005%3A0042", plot.OpenDataBotUrl);
        Assert.Equal("UA213223130000026007233566001", plot.EffectiveSellerIban);
        Assert.Null(plot.PrimaryDocument);
        Assert.Equal(5, LandPlot.StandardPurposes.Count);

        var zeroAreaPlot = new LandPlot
        {
            AreaHectares = 0,
            PriceUsd = 10000m,
            SellerBankIban = "UA990000000000000000000000001"
        };
        Assert.Equal(0m, zeroAreaPlot.PricePerSotkaUsd);
        Assert.Equal("UA990000000000000000000000001", zeroAreaPlot.EffectiveSellerIban);
    }

    [Fact]
    public void CabinetViewModel_CanRequestPurchase_EnforcesCompletedViewingAndNoActiveDeal()
    {
        var verifiedPlot = new LandPlot { Id = 7, Status = PlotStatus.VerifiedInCatalog };
        var completedViewing = new ViewingRequest
        {
            Id = 1,
            LandPlotId = 7,
            LandPlot = verifiedPlot,
            Status = ViewingStatus.Completed
        };

        var vm = new CabinetViewModel();
        Assert.True(vm.CanRequestPurchase(completedViewing));

        var scheduledViewing = new ViewingRequest
        {
            Id = 2,
            LandPlotId = 7,
            LandPlot = verifiedPlot,
            Status = ViewingStatus.Scheduled
        };
        Assert.False(vm.CanRequestPurchase(scheduledViewing));

        vm.MyDeals.Add(new SaleDeal { Id = 10, LandPlotId = 7, Status = DealStatus.Cancelled });
        Assert.True(vm.CanRequestPurchase(completedViewing));

        vm.MyDeals.Add(new SaleDeal { Id = 11, LandPlotId = 7, Status = DealStatus.DraftPreparedByRealtor });
        Assert.False(vm.CanRequestPurchase(completedViewing));
    }
}
