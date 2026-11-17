namespace LandAgencyMvc.Models;

/// <summary>
/// Ролі користувачів за діаграмою прецедентів: два клієнтські актори (Покупець, Продавець)
/// та два службові (Рієлтор, Юрист). Числові значення зберігаються в базі даних і не мають змінюватися.
/// </summary>
public enum UserRole
{
    Buyer = 0,
    Realtor = 1,
    Lawyer = 2,
    Seller = 3
}

public enum PlotStatus
{
    PendingLegalCheck = 0,
    VerifiedInCatalog = 1,
    RejectedByLawyer = 2,
    Sold = 3,
    Draft = 4
}

public enum ViewingStatus
{
    NewRequest,
    Scheduled,
    Completed,
    PlotRejectedByBuyer,
    Cancelled
}

public enum DealStatus
{
    DraftPreparedByRealtor,
    ReturnedForRevision,
    ApprovedByLawyerForNotary,
    SignedAtNotary,
    CompletedAtNotary,
    Cancelled
}

public enum PaymentStatus
{
    PendingSubmission,
    PendingVerification,
    Confirmed,
    Rejected
}
