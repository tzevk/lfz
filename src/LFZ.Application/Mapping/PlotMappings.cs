using LFZ.Application.DTOs;
using LFZ.Domain.Entities;

namespace LFZ.Application.Mapping;

public static class PlotMappings
{
    public static PlotDto ToDto(this Plot plot) => new(
        plot.Id,
        plot.Code,
        plot.DisplayName,
        plot.LandUseType,
        plot.Phase,
        plot.HatchColor,
        plot.AreaHectares,
        plot.Status.ToString(),
        plot.CurrentTenant?.ToDto(),
        plot.SvgPath,
        plot.Centroid is null ? null : new PlotCentroidDto(plot.Centroid.X, plot.Centroid.Y),
        plot.IsLocked,
        plot.MultiTenantBlockEnabled,
        Convert.ToBase64String(plot.RowVersion),
        plot.TenantBlocks
            .Select(block => new PlotTenantBlockDto(
                block.TenantId,
                block.Tenant?.Name ?? string.Empty,
                block.BlockedAtUtc,
                block.Notes))
            .ToList());

    public static TenantDto ToDto(this Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.LegalName, tenant.Contact, tenant.Industry);

    public static PlotRequestDto ToDto(this PlotRequest request) => new(
        request.Id,
        request.PlotId,
        request.TenantId,
        request.RequestType.ToString(),
        request.Status.ToString(),
        request.IntendedUse,
        request.RequestedStartDate,
        request.RequestedByUserId,
        request.DecisionByUserId,
        request.DecisionAtUtc,
        request.Notes,
        request.CreatedAtUtc);
}
