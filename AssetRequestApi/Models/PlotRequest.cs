using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.Models;

public class PlotRequest
{
    public int Id { get; set; }

    public int PlotId { get; set; }

    public Plot Plot { get; set; } = null!;

    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    [Required]
    public string RequestedByUserId { get; set; } = string.Empty;

    public ApplicationUser RequestedByUser { get; set; } = null!;

    public PlotRequestType RequestType { get; set; }

    public PlotRequestStatus Status { get; set; } = PlotRequestStatus.Pending;

    [MaxLength(1000)]
    public string? IntendedUse { get; set; }

    public DateOnly? RequestedStartDate { get; set; }

    public string? DecisionByUserId { get; set; }

    public ApplicationUser? DecisionByUser { get; set; }

    public DateTime? DecisionAtUtc { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}