namespace LFZ.Domain.Enums;

public enum PlotStatus
{
    Free,
    Occupied,
    Blocked,
    PendingReview,
    Unavailable
}

public enum PlotRequestType
{
    Allocate,
    Block
}

public enum PlotRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}
