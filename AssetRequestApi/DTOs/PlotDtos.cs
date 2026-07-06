using System.ComponentModel.DataAnnotations;
using AssetRequestApi.Models;

namespace AssetRequestApi.DTOs;

public record TenantDto(int Id, string Name, string? LegalName, string? Contact, string? Industry);

public record PlotCentroidDto(double X, double Y);

public record PlotTenantBlockDto(int TenantId, string TenantName, DateTime BlockedAtUtc, string? Notes);

public record PlotDto(
    int Id,
    string Code,
    string DisplayName,
    string LandUseType,
    string? Phase,
    string? HatchColor,
    decimal AreaHectares,
    string Status,
    TenantDto? CurrentTenant,
    string? SvgPath,
    PlotCentroidDto? Centroid,
    bool IsLocked,
    bool MultiTenantBlockEnabled,
    string RowVersion,
    IReadOnlyCollection<PlotTenantBlockDto> TenantBlocks);

public record PlotSummaryDto(
    int TotalPlots,
    decimal TotalAreaHectares,
    IReadOnlyDictionary<string, int> CountByStatus,
    IReadOnlyDictionary<string, decimal> AreaByStatus,
    IReadOnlyDictionary<string, decimal> AreaByLandUseType);

public class CreatePlotRequestDto
{
    [Required]
    public int PlotId { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required]
    public PlotRequestType RequestType { get; set; }

    [MaxLength(1000)]
    public string? IntendedUse { get; set; }

    public DateOnly? RequestedStartDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdatePlotDto
{
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

    public string? SvgPath { get; set; }

    public bool IsLocked { get; set; }
}

public record PlotRequestDto(
    int Id,
    int PlotId,
    int TenantId,
    string RequestType,
    string Status,
    string? IntendedUse,
    DateOnly? RequestedStartDate,
    string RequestedByUserId,
    string? DecisionByUserId,
    DateTime? DecisionAtUtc,
    string? Notes,
    DateTime CreatedAtUtc);

public class BlockPlotDto
{
    [Required]
    public int TenantId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class DirectAllocatePlotDto
{
    [Required]
    public int TenantId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ReleasePlotDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class ReviewPlotRequestDto
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ToggleMultiTenantBlockDto
{
    public bool Enabled { get; set; }
}

public class MultiTenantBlockDto
{
    [Required]
    [MinLength(1)]
    public List<int> TenantIds { get; set; } = new();

    [MaxLength(1000)]
    public string? Notes { get; set; }
}