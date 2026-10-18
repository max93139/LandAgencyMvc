namespace LandAgencyMvc.Models;

public enum UserRole
{
    Client,
    Realtor,
    Lawyer
}

public enum PlotStatus
{
    PendingLegalCheck,
    VerifiedInCatalog,
    RejectedByLawyer,
    Sold
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
    CompletedAtNotary
}
