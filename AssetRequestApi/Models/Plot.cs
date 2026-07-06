using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace AssetRequestApi.Models;

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

    public Geometry? Boundary { get; set; }

    public string? SvgPath { get; set; }

    public Point? Centroid { get; set; }

    public bool IsLocked { get; set; }

    public bool MultiTenantBlockEnabled { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PlotRequest> Requests { get; set; } = new List<PlotRequest>();

    public ICollection<PlotTenantBlock> TenantBlocks { get; set; } = new List<PlotTenantBlock>();

    public ICollection<PlotStatusHistory> StatusHistory { get; set; } = new List<PlotStatusHistory>();
}