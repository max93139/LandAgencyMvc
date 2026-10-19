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

    [Fact]
    public void RejectPayment_TransitionsToRejected_AndAllowsResubmission()
    {
        var payment = new Payment
        {
            Id = 2,
            InvoiceNumber = "INV-2026-0001-1",
            AmountUsd = 28500m,
            Status = PaymentStatus.PendingSubmission
        };

        payment.SubmitPayment("WRONG-REF-000", "UA453052990000026201234567890");
        Assert.Equal(PaymentStatus.PendingVerification, payment.Status);

        payment.RejectPayment(verifierUserId: 3, reason: "Код квитанції не знайдено у виписці");
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Equal(3, payment.VerifiedByUserId);

        payment.SubmitPayment("VALID-REF-999", "UA453052990000026201234567890");
        Assert.Equal(PaymentStatus.PendingVerification, payment.Status);
        Assert.Equal("VALID-REF-999", payment.PaymentReference);
    }

    [Fact]
    public void RegisterOwnershipRight_RequiresFullPayment_AndBlocksUnpaidPlotPrice()
    {
        var plot = new LandPlot
        {
            Id = 10,
            CadastralNumber = "3221884000:03:001:0120",
            Status = PlotStatus.VerifiedInCatalog
        };

        var deal = new SaleDeal
        {
            Id = 10,
            LandPlotId = plot.Id,
            LandPlot = plot
        };
        deal.PrepareContractDraft("ДГ-2026-010", 30000m, 3.0);
        deal.Payments = deal.CreateDefaultPayments("UA213223130000026007233566001", isPaid: false);

        deal.Status = DealStatus.ApprovedByLawyerForNotary;

        var agencyFee = deal.AgencyPayment!;
        agencyFee.SubmitPayment("TRX-FEE-01", "UA453052990000026201234567890");
        agencyFee.VerifyPayment(verifierUserId: 1);

        Assert.True(deal.IsAgencyFeePaid);
        Assert.False(deal.IsFullyPaid);

        Assert.True(deal.SignByParties("НР-2026-0010"));
        Assert.Equal(DealStatus.SignedAtNotary, deal.Status);

        Assert.False(deal.RegisterOwnershipRight());
        Assert.Equal(DealStatus.SignedAtNotary, deal.Status);
        Assert.Equal(PlotStatus.VerifiedInCatalog, plot.Status);

        var mainPayment = deal.Payments.First(p => p != agencyFee);
        mainPayment.SubmitPayment("TRX-MAIN-02", "UA453052990000026201234567890");
        mainPayment.VerifyPayment(verifierUserId: 1);

        Assert.True(deal.IsFullyPaid);
        Assert.True(deal.RegisterOwnershipRight());
        Assert.Equal(DealStatus.CompletedAtNotary, deal.Status);
        Assert.Equal(PlotStatus.Sold, plot.Status);
    }

    [Fact]
    public void CancelDeal_SucceedsBeforeCompletion_AndFailsAfterCompletion()
    {
        var deal = new SaleDeal { Id = 15, Status = DealStatus.ApprovedByLawyerForNotary };
        Assert.True(deal.CancelDeal("Покупець відмовився від угоди"));
        Assert.Equal(DealStatus.Cancelled, deal.Status);
        Assert.False(deal.CancelDeal("Повторне скасування"));

        var completedDeal = new SaleDeal { Id = 16, Status = DealStatus.CompletedAtNotary };
        Assert.False(completedDeal.CancelDeal("Спроба скасувати завершену угоду"));
        Assert.Equal(DealStatus.CompletedAtNotary, completedDeal.Status);
    }
}
