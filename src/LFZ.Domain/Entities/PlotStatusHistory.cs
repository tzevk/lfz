using System.ComponentModel.DataAnnotations;
using LFZ.Domain.Enums;

namespace LFZ.Domain.Entities;

/// <summary>Immutable audit row written automatically whenever a plot's status changes.</summary>
public class PlotStatusHistory
{
    public int Id { get; set; }

    public int PlotId { get; set; }

    public Plot Plot { get; set; } = null!;

    public PlotStatus FromStatus { get; set; }

    public PlotStatus ToStatus { get; set; }

    public int? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required]
    public string ActorUserId { get; set; } = string.Empty;

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public int? OriginatingRequestId { get; set; }

    public PlotRequest? OriginatingRequest { get; set; }
}
