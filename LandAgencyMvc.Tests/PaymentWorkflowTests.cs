using LandAgencyMvc.Models;
using Xunit;

namespace LandAgencyMvc.Tests;

public class PaymentWorkflowTests
{
    [Fact]
    public void CreateDefaultPayments_GeneratesCorrectInvoicesAndAmounts()
    {
        var deal = new SaleDeal
        {
            Id = 42,
            DraftContractNumber = "ДГ-2026-042",
            FinalPriceUsd = 50000m,
            AgencyCommissionUsd = 1500m
        };

        var payments = deal.CreateDefaultPayments(
            sellerIban: "UA112222220000011111111111111",
            isPaid: false,
            senderIban: "UA998888880000099999999999999",
            agencyIban: "UA003333330000033333333333333");

        Assert.Equal(2, payments.Count);

        var mainPart = payments[0];
        Assert.Equal("Оплата вартості земельної ділянки за договором (IBAN)", mainPart.PaymentType);
        Assert.Equal(50000m, mainPart.AmountUsd);
        Assert.StartsWith("INV-", mainPart.InvoiceNumber);
        Assert.Equal(PaymentStatus.PendingSubmission, mainPart.Status);
        Assert.False(mainPart.IsPaid);

        var agencyFee = payments[1];
        Assert.Equal("Комісійна винагорода агентства (3%)", agencyFee.PaymentType);
        Assert.Equal(1500m, agencyFee.AmountUsd);
        Assert.StartsWith("INV-", agencyFee.InvoiceNumber);
        Assert.Equal(PaymentStatus.PendingSubmission, agencyFee.Status);
        Assert.False(agencyFee.IsPaid);
    }

    [Fact]
    public void SubmitPayment_TransitionsToPendingVerification_WithReference()
    {
        var payment = new Payment
        {
            Id = 1,
            InvoiceNumber = "INV-2026042-01",
            AmountUsd = 5000m,
            Status = PaymentStatus.PendingSubmission
        };

        payment.SubmitPayment("TRX-BANK-998811", "UA998888880000099999999999999");

        Assert.Equal(PaymentStatus.PendingVerification, payment.Status);
        Assert.Equal("TRX-BANK-998811", payment.PaymentReference);
        Assert.Equal("UA998888880000099999999999999", payment.SenderIban);
        Assert.NotNull(payment.SubmittedAt);
        Assert.False(payment.IsPaid);
    }

    [Fact]
    public void VerifyPayment_ConfirmsTransaction_AndUpdatesPaidState()
    {
        var payment = new Payment
        {
            Id = 1,
            InvoiceNumber = "INV-2026042-01",
            AmountUsd = 5000m,
            Status = PaymentStatus.PendingVerification,
            PaymentReference = "TRX-BANK-998811"
        };

        payment.VerifyPayment(verifierUserId: 5, notes: "Виписка підтверджена");

        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.True(payment.IsPaid);
        Assert.Equal(5, payment.VerifiedByUserId);
        Assert.NotNull(payment.VerifiedAt);
    }

    [Fact]
    public void SaleDeal_HasPendingPayments_DetectsUnconfirmedSubmissions()
    {
        var deal = new SaleDeal();
        deal.Payments.Add(new Payment { Status = PaymentStatus.Confirmed, IsPaid = true });
        var pending = new Payment { Status = PaymentStatus.PendingVerification, PaymentReference = "TRX-1" };
        deal.Payments.Add(pending);

        Assert.True(deal.HasPendingPayments);

        pending.VerifyPayment(verifierUserId: 1);
        Assert.False(deal.HasPendingPayments);
    }
}
