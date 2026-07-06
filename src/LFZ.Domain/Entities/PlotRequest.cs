using System.ComponentModel.DataAnnotations;
using LFZ.Domain.Enums;

namespace LFZ.Domain.Entities;

/// <summary>
/// A request by a requester to allocate or block a plot for a tenant.
/// User references are held as string ids issued by the identity provider;
/// the Domain layer has no dependency on ASP.NET Core Identity.
/// </summary>
public class PlotRequest
{
    public int Id { get; set; }

    public int PlotId { get; set; }

    public Plot Plot { get; set; } = null!;

    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    [Required]
    public string RequestedByUserId { get; set; } = string.Empty;

    public PlotRequestType RequestType { get; set; }

    public PlotRequestStatus Status { get; set; } = PlotRequestStatus.Pending;

    [MaxLength(1000)]
    public string? IntendedUse { get; set; }

    public DateOnly? RequestedStartDate { get; set; }

    public string? DecisionByUserId { get; set; }

    public DateTime? DecisionAtUtc { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
