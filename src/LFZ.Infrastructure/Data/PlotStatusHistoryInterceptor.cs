using LFZ.Application.Services;
using LFZ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LFZ.Infrastructure.Data;

/// <summary>
/// Writes an immutable PlotStatusHistory row for every persisted plot status transition.
/// Actor/reason metadata comes from the scoped PlotStatusAuditContext.
/// </summary>
public class PlotStatusHistoryInterceptor : SaveChangesInterceptor
{
    private readonly PlotStatusAuditContext _auditContext;

    public PlotStatusHistoryInterceptor(PlotStatusAuditContext auditContext)
    {
        _auditContext = auditContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddStatusHistory(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddStatusHistory(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddStatusHistory(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var transitions = context.ChangeTracker
            .Entries<Plot>()
            .Where(entry => entry.State == EntityState.Modified && entry.Property(plot => plot.Status).IsModified)
            .Select(CreateHistory)
            .Where(history => history is not null)
            .Cast<PlotStatusHistory>()
            .ToList();

        if (transitions.Count > 0)
        {
            context.Set<PlotStatusHistory>().AddRange(transitions);
        }
    }

    private PlotStatusHistory? CreateHistory(EntityEntry<Plot> entry)
    {
        var fromStatus = entry.Property(plot => plot.Status).OriginalValue;
        var toStatus = entry.Property(plot => plot.Status).CurrentValue;
        if (fromStatus == toStatus)
        {
            return null;
        }

        return new PlotStatusHistory
        {
            PlotId = entry.Entity.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            TenantId = entry.Property(plot => plot.CurrentTenantId).CurrentValue,
            ActorUserId = _auditContext.ActorUserId ?? "system",
            ChangedAtUtc = DateTime.UtcNow,
            Reason = _auditContext.Reason,
            OriginatingRequestId = _auditContext.OriginatingRequestId
        };
    }
}
