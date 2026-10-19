using System.ComponentModel.DataAnnotations.Schema;

namespace LandAgencyMvc.Models;

/// <summary>
/// Платіжна транзакція в межах угоди купівлі-продажу:
/// виставлення рахунку-фактури, внесення реквізитів платежу клієнтом та верифікація випискою банку.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int SaleDealId { get; set; }

    public SaleDeal? SaleDeal { get; set; }

    /// <summary>
    /// Номер фіскальної або внутрішньої квитанції про безготівковий розрахунок.
    /// </summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>
    /// Номер рахунку-фактури (Invoice) для оплати.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public decimal AmountUsd { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;

    public string PaymentType { get; set; } = string.Empty;

    /// <summary>
    /// IBAN рахунок платника (покупця).
    /// </summary>
    public string SenderIban { get; set; } = string.Empty;

    /// <summary>
    /// IBAN рахунок отримувача (продавця або агентства).
    /// </summary>
    public string RecipientIban { get; set; } = string.Empty;

    /// <summary>
    /// Номер банківської платіжної інструкції або код квитанції, наданий клієнтом.
    /// </summary>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>
    /// Поточний статус обробки платежу в агентстві.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.PendingSubmission;

    /// <summary>
    /// Прапорець успішного проведення та зарахування банківської транзакції.
    /// </summary>
    public bool IsPaid { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int? VerifiedByUserId { get; set; }

    public AppUser? VerifiedByUser { get; set; }

    public string VerificationNotes { get; set; } = string.Empty;

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        PaymentStatus.PendingSubmission => "Очікує оплати за рахунком",
        PaymentStatus.PendingVerification => "На перевірці виписки банку ⏳",
        PaymentStatus.Confirmed => "Зараховано та підтверджено ✓",
        PaymentStatus.Rejected => "Відхилено (помилка реквізитів) ✗",
        _ => Status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        PaymentStatus.Confirmed => "la-badge-green",
        PaymentStatus.PendingVerification => "la-badge-amber",
        PaymentStatus.Rejected => "la-badge-red",
        _ => "la-badge-indigo"
    };

    public string GenerateReceiptNumber(int dealId, int index)
    {
        ReceiptNumber = $"КВ-{DateTime.Now:yyyy}-{dealId:D4}-{index}";
        InvoiceNumber = $"INV-{DateTime.Now:yyyy}-{dealId:D4}-{index}";
        return ReceiptNumber;
    }

    /// <summary>
    /// Клієнт повідомляє про здійснену оплату за банківським рахунком IBAN.
    /// </summary>
    public void SubmitPayment(string paymentReference, string? senderIban = null)
    {
        PaymentReference = (paymentReference ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(senderIban))
        {
            SenderIban = senderIban.Trim();
        }

        Status = PaymentStatus.PendingVerification;
        SubmittedAt = DateTime.Now;
    }

    /// <summary>
    /// Рієлтор або бухгалтер агентства підтверджує зарахування коштів за банківською випискою.
    /// </summary>
    public void VerifyPayment(int verifierUserId, string? notes = null)
    {
        Status = PaymentStatus.Confirmed;
        IsPaid = true;
        VerifiedAt = DateTime.Now;
        VerifiedByUserId = verifierUserId;
        VerificationNotes = !string.IsNullOrWhiteSpace(notes)
            ? notes.Trim()
            : "Платіж підтверджено згідно з банківською випискою IBAN.";
    }

    /// <summary>
    /// Виконання або пряме підтвердження для зворотної сумісності.
    /// </summary>
    public bool ExecuteTransaction(string? payerIban = null)
    {
        if (!string.IsNullOrWhiteSpace(payerIban))
        {
            SenderIban = payerIban.Trim();
        }

        Status = PaymentStatus.Confirmed;
        IsPaid = true;
        PaymentDate = DateTime.Now;
        VerifiedAt = DateTime.Now;
        return true;
    }
}
