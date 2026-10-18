namespace LandAgencyMvc.Models;

public class Payment
{
    public int Id { get; set; }

    public int SaleDealId { get; set; }

    public SaleDeal? SaleDeal { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal AmountUsd { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;

    public string PaymentType { get; set; } = string.Empty;

    public string SenderIban { get; set; } = string.Empty;

    public string RecipientIban { get; set; } = string.Empty;

    public bool IsPaid { get; set; }

    public string GenerateReceiptNumber(int dealId, int index)
    {
        ReceiptNumber = $"КВ-{DateTime.Now:yyyy}-{dealId:D4}-{index}";
        return ReceiptNumber;
    }

    public bool ExecuteTransaction(string? payerIban = null)
    {
        if (!string.IsNullOrWhiteSpace(payerIban))
        {
            SenderIban = payerIban.Trim();
        }

        IsPaid = true;
        PaymentDate = DateTime.Now;
        return true;
    }
}
