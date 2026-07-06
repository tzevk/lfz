namespace AssetRequestApi.Services;

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