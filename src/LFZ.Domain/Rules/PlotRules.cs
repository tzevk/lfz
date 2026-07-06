using LFZ.Domain.Entities;
using LFZ.Domain.Enums;

namespace LFZ.Domain.Rules;

/// <summary>
/// Pure domain rules for the plot status workflow. All state-machine invariants live here
/// so they are enforced identically by every caller (API, Web, background jobs) and are
/// unit-testable without a database.
///
/// Status workflow:
///   Free ──request──▶ PendingReview ──approve──▶ Occupied
///                        │ reject                    │ release (admin)
///                        ▼                           ▼
///                       Free ◀────────────────────  Free
///   Free ──block──▶ Blocked ──release──▶ Free
///   Unavailable / IsLocked: terminal, no transitions allowed.
/// </summary>
public static class PlotRules
{
    /// <summary>Transitions the workflow permits, independent of who performs them.</summary>
    private static readonly Dictionary<PlotStatus, PlotStatus[]> AllowedTransitions = new()
    {
        [PlotStatus.Free] = new[] { PlotStatus.PendingReview, PlotStatus.Occupied, PlotStatus.Blocked },
        [PlotStatus.PendingReview] = new[] { PlotStatus.Occupied, PlotStatus.Free },
        [PlotStatus.Blocked] = new[] { PlotStatus.Free, PlotStatus.Blocked },
        [PlotStatus.Occupied] = new[] { PlotStatus.Free },
        [PlotStatus.Unavailable] = Array.Empty<PlotStatus>()
    };

    public static bool CanTransition(PlotStatus from, PlotStatus to) =>
        from == to || (AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to));

    /// <summary>Locked and Unavailable plots never accept workflow changes.</summary>
    public static void EnsureCanChange(Plot plot)
    {
        if (plot.IsLocked)
        {
            throw new PlotRuleViolationException($"Plot {plot.Code} is locked.");
        }

        if (plot.Status == PlotStatus.Unavailable)
        {
            throw new PlotRuleViolationException($"Plot {plot.Code} is unavailable.");
        }
    }

    public static void EnsureCanBeRequested(Plot plot)
    {
        EnsureCanChange(plot);
        if (plot.Status != PlotStatus.Free)
        {
            throw new PlotRuleViolationException(
                $"Only free plots can be requested. Plot {plot.Code} is {plot.Status}.");
        }
    }

    public static void EnsureCanBeAllocated(Plot plot)
    {
        EnsureCanChange(plot);
        if (plot.Status != PlotStatus.Free)
        {
            throw new PlotRuleViolationException(
                $"Only free plots can be allocated. Plot {plot.Code} is {plot.Status}.");
        }
    }

    public static void EnsureCanBeBlocked(Plot plot)
    {
        EnsureCanChange(plot);
        if (plot.Status is PlotStatus.Occupied or PlotStatus.Blocked or PlotStatus.PendingReview)
        {
            throw new PlotRuleViolationException($"Plot {plot.Code} is already {plot.Status}.");
        }
    }

    public static void EnsureRequestIsReviewable(PlotRequest request)
    {
        if (request.Status != PlotRequestStatus.Pending)
        {
            throw new PlotRuleViolationException(
                $"Only pending requests can be reviewed. Request {request.Id} is {request.Status}.");
        }

        if (request.Plot.Status != PlotStatus.PendingReview)
        {
            throw new PlotRuleViolationException($"Plot {request.Plot.Code} is not pending review.");
        }
    }

    public static void EnsureMultiTenantBlockAllowed(Plot plot, bool featureEnabledGlobally)
    {
        EnsureCanChange(plot);

        if (!plot.MultiTenantBlockEnabled || !featureEnabledGlobally)
        {
            throw new PlotRuleViolationException("Multi-tenant blocking is not enabled for this plot.");
        }

        if (plot.Status == PlotStatus.Occupied)
        {
            throw new PlotRuleViolationException($"Plot {plot.Code} is already allocated.");
        }
    }
}

/// <summary>Raised when a workflow invariant would be violated. Maps to HTTP 409 in the API.</summary>
public class PlotRuleViolationException : InvalidOperationException
{
    public PlotRuleViolationException(string message) : base(message)
    {
    }
}
