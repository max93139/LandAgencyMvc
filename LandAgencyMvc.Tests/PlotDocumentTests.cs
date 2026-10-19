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
}
