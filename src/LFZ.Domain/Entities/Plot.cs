using System.ComponentModel.DataAnnotations;
using LFZ.Domain.Enums;
using NetTopologySuite.Geometries;

namespace LFZ.Domain.Entities;

/// <summary>
/// A developable parcel extracted from the LFZ master-plan drawing.
/// Geometry is seeded by LFZ.Tools.PlotExtractor and is data-only maintenance:
/// re-run the extractor when the drawing changes.
/// </summary>
public class Plot
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LandUseType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Phase { get; set; }

    [MaxLength(20)]
    public string? HatchColor { get; set; }

    public decimal AreaHectares { get; set; }

    public PlotStatus Status { get; set; } = PlotStatus.Free;

    public int? CurrentTenantId { get; set; }

    public Tenant? CurrentTenant { get; set; }

    /// <summary>Polygon in plan metres (SRID 0) for server-side spatial queries.</summary>
    public Geometry? Boundary { get; set; }

    /// <summary>SVG path in plan metres, y-flipped for direct client rendering.</summary>
    public string? SvgPath { get; set; }

    public Point? Centroid { get; set; }

    /// <summary>Roads, common areas and other non-selectable regions; rendered greyed out.</summary>
    public bool IsLocked { get; set; }

    public bool MultiTenantBlockEnabled { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PlotRequest> Requests { get; set; } = new List<PlotRequest>();

    public ICollection<PlotTenantBlock> TenantBlocks { get; set; } = new List<PlotTenantBlock>();

    public ICollection<PlotStatusHistory> StatusHistory { get; set; } = new List<PlotStatusHistory>();
}
