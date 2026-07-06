using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.Models;

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

    public ApplicationUser? ActorUser { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public int? OriginatingRequestId { get; set; }

    public PlotRequest? OriginatingRequest { get; set; }
}