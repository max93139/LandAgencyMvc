namespace LandAgencyMvc.Models;

/// <summary>
/// Платіжна транзакція в межах угоди купівлі-продажу (оплата ділянки або комісія агентства).
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
    /// Прапорець успішного проведення банківської транзакції.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Формує унікальний номер платіжної квитанції у форматі КВ-РРРР-УГОДА-ІНДЕКС.
    /// </summary>
    public string GenerateReceiptNumber(int dealId, int index)
    {
        ReceiptNumber = $"КВ-{DateTime.Now:yyyy}-{dealId:D4}-{index}";
        return ReceiptNumber;
    }

    /// <summary>
    /// Фіксує виконання платежу: записує IBAN платника, статус оплати та поточну дату проведення.
    /// </summary>
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

