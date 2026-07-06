using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.Models;

public class Tenant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(500)]
    public string? Contact { get; set; }

    [MaxLength(120)]
    public string? Industry { get; set; }

    public ICollection<Plot> CurrentPlots { get; set; } = new List<Plot>();

    public ICollection<PlotRequest> PlotRequests { get; set; } = new List<PlotRequest>();

    public ICollection<PlotTenantBlock> PlotTenantBlocks { get; set; } = new List<PlotTenantBlock>();
}