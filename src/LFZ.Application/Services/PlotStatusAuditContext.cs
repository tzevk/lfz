namespace LFZ.Application.Services;

/// <summary>
/// Scoped ambient context describing who is performing the current unit of work.
/// Read by the status-history interceptor when a plot status transition is persisted.
/// </summary>
public class PlotStatusAuditContext
{
    public string? ActorUserId { get; private set; }

    public string? Reason { get; private set; }

    public int? OriginatingRequestId { get; private set; }

    public void Set(string actorUserId, string? reason = null, int? originatingRequestId = null)
    {
        ActorUserId = actorUserId;
        Reason = reason;
        OriginatingRequestId = originatingRequestId;
    }

    public void Clear()
    {
        ActorUserId = null;
        Reason = null;
        OriginatingRequestId = null;
    }
}
