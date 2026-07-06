using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.Models;

public class PlotTenantBlock
{
    public int PlotId { get; set; }

    public Plot Plot { get; set; } = null!;

    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string? BlockedByUserId { get; set; }

    public ApplicationUser? BlockedByUser { get; set; }

    public DateTime BlockedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}