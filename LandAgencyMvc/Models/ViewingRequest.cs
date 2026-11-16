using System.ComponentModel.DataAnnotations.Schema;

namespace LandAgencyMvc.Models;

public class ViewingRequest
{
    public int Id { get; set; }

    public int LandPlotId { get; set; }

    public LandPlot? LandPlot { get; set; }

    public int BuyerId { get; set; }

    public AppUser? Buyer { get; set; }

    public int? RealtorId { get; set; }

    public AppUser? Realtor { get; set; }

    public DateTime PreferredDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string TimeSlot { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public string ResultNotes { get; set; } = string.Empty;

    public bool BuyerReadyToBuy { get; set; }

    public ViewingStatus Status { get; set; } = ViewingStatus.NewRequest;

    [NotMapped]
    public string StatusDisplayName => DisplayNameOf(Status);

    public static string DisplayNameOf(ViewingStatus status) => status switch
    {
        ViewingStatus.NewRequest => "Нова заявка",
        ViewingStatus.Scheduled => "Огляд призначено ✓",
        ViewingStatus.Completed => "Огляд проведено ✓",
        ViewingStatus.PlotRejectedByBuyer => "Не підійшла (Уточнення критеріїв)",
        ViewingStatus.Cancelled => "Скасовано",
        _ => status.ToString()
    };

    [NotMapped]
    public string StatusBadgeClass => Status switch
    {
        ViewingStatus.Completed => "la-badge-green",
        ViewingStatus.Scheduled => "la-badge-green",
        ViewingStatus.Cancelled => "la-badge-red",
        ViewingStatus.PlotRejectedByBuyer => "la-badge-red",
        _ => "la-badge-amber"
    };

    [NotMapped]
    public bool CanCreateDeal => Status == ViewingStatus.Completed &&
                                 (LandPlot == null || LandPlot.Status == PlotStatus.VerifiedInCatalog);

    [NotMapped]
    public bool CanCancelByClient => Status is ViewingStatus.NewRequest or ViewingStatus.Scheduled;

    [NotMapped]
    public bool CanRescheduleByClient => CanCancelByClient;

    /// <summary>
    /// Завершені огляди (проведений, відхилений покупцем, скасований) є історією і не змінюються.
    /// </summary>
    [NotMapped]
    public bool IsFinished => Status is ViewingStatus.Completed or ViewingStatus.PlotRejectedByBuyer or ViewingStatus.Cancelled;

    /// <summary>
    /// Фізично вилучити можна лише нову, скасовану або відхилену покупцем заявку. Призначений огляд спершу
    /// скасовується, а проведений зберігається: він є підставою для угоди.
    /// </summary>
    [NotMapped]
    public bool CanBeDeleted => Status is ViewingStatus.NewRequest or ViewingStatus.Cancelled or ViewingStatus.PlotRejectedByBuyer;

    /// <summary>
    /// Допустимі переходи статусу: нова заявка підтверджується або скасовується, призначений огляд проводиться,
    /// відхиляється покупцем або скасовується. З завершеного статусу переходів немає.
    /// </summary>
    public bool CanChangeStatusTo(ViewingStatus target) => Status switch
    {
        ViewingStatus.NewRequest => target is ViewingStatus.Scheduled or ViewingStatus.Cancelled,
        ViewingStatus.Scheduled => target is ViewingStatus.Completed or ViewingStatus.PlotRejectedByBuyer or ViewingStatus.Cancelled,
        _ => false
    };

    /// <summary>
    /// Переносить огляд на нову дату та час на прохання покупця. Заявка повертається у статус
    /// «Нова заявка», бо рієлтор має підтвердити нову дату.
    /// </summary>
    /// <returns>False, якщо огляд у поточному статусі перенести не можна.</returns>
    public bool Reschedule(DateTime newDate, string newTimeSlot)
    {
        if (!CanRescheduleByClient)
        {
            return false;
        }

        var previousSlot = $"{PreferredDate:dd.MM.yyyy} {TimeSlot}";
        PreferredDate = newDate.Date;
        TimeSlot = newTimeSlot.Trim();
        Status = ViewingStatus.NewRequest;
        Comment = $"{Comment} [Дату перенесено покупцем, було {previousSlot}]".Trim();
        return true;
    }

    public void ConfirmViewing(int? realtorId)
    {
        RealtorId = realtorId;
        Status = ViewingStatus.Scheduled;
    }

    public void CancelViewing(string? reason = null)
    {
        Status = ViewingStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            ResultNotes = reason.Trim();
        }
    }

    public void CompleteViewing(bool fitsBuyer, string? notes = null)
    {
        BuyerReadyToBuy = fitsBuyer;
        Status = fitsBuyer ? ViewingStatus.Completed : ViewingStatus.PlotRejectedByBuyer;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ResultNotes = notes.Trim();
        }
    }
}
