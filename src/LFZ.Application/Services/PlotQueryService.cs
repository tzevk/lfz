using LFZ.Application.Abstractions;
using LFZ.Application.DTOs;
using LFZ.Application.Mapping;
using LFZ.Domain.Entities;
using LFZ.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LFZ.Application.Services;

/// <summary>Read-side queries shared by the API controllers and the Blazor Web UI.</summary>
public class PlotQueryService
{
    private readonly IApplicationDbContext _context;

    public PlotQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PlotDto>> GetPlotsAsync(
        string? status = null, string? landUseType = null, string? phase = null, string? search = null)
    {
        var query = ApplyFilters(BuildReadQuery(), status, landUseType, phase, search);
        var plots = await query.OrderBy(plot => plot.Code).ToListAsync();
        return plots.Select(plot => plot.ToDto()).ToList();
    }

    public async Task<PlotDto?> GetPlotByCodeAsync(string code)
    {
        var plot = await BuildReadQuery().FirstOrDefaultAsync(item => item.Code == code);
        return plot?.ToDto();
    }

    public async Task<PlotSummaryDto> GetSummaryAsync(
        string? status = null, string? landUseType = null, string? phase = null, string? search = null)
    {
        var plots = await ApplyFilters(_context.Plots.AsNoTracking(), status, landUseType, phase, search).ToListAsync();
        var trackedStatuses = new[]
        {
            PlotStatus.Free,
            PlotStatus.Occupied,
            PlotStatus.Blocked,
            PlotStatus.PendingReview
        };

        return new PlotSummaryDto(
            plots.Count,
            plots.Sum(plot => plot.AreaHectares),
            trackedStatuses.ToDictionary(item => item.ToString(), item => plots.Count(plot => plot.Status == item)),
            trackedStatuses.ToDictionary(item => item.ToString(), item => plots.Where(plot => plot.Status == item).Sum(plot => plot.AreaHectares)),
            plots.GroupBy(plot => plot.LandUseType).ToDictionary(group => group.Key, group => group.Sum(plot => plot.AreaHectares)));
    }

    public async Task<IReadOnlyList<PlotRequestDto>> GetPendingRequestsAsync()
    {
        var requests = await _context.PlotRequests
            .AsNoTracking()
            .Include(request => request.Plot)
            .Include(request => request.Tenant)
            .Where(request => request.Status == PlotRequestStatus.Pending)
            .OrderBy(request => request.CreatedAtUtc)
            .ToListAsync();

        return requests.Select(request => request.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync()
    {
        var tenants = await _context.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync();
        return tenants.Select(tenant => tenant.ToDto()).ToList();
    }

    private IQueryable<Plot> BuildReadQuery()
    {
        return _context.Plots
            .AsNoTracking()
            .Include(plot => plot.CurrentTenant)
            .Include(plot => plot.TenantBlocks)
                .ThenInclude(block => block.Tenant)
            .AsQueryable();
    }

    private static IQueryable<Plot> ApplyFilters(
        IQueryable<Plot> query, string? status, string? landUseType, string? phase, string? search)
    {
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<PlotStatus>(status.Replace(" ", string.Empty), true, out var parsedStatus))
        {
            query = query.Where(plot => plot.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(landUseType))
        {
            query = query.Where(plot => plot.LandUseType == landUseType);
        }

        if (!string.IsNullOrWhiteSpace(phase))
        {
            query = query.Where(plot => plot.Phase == phase);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(plot => plot.Code.Contains(term) ||
                plot.DisplayName.Contains(term) ||
                (plot.CurrentTenant != null && plot.CurrentTenant.Name.Contains(term)) ||
                plot.TenantBlocks.Any(block => block.Tenant.Name.Contains(term)));
        }

        return query;
    }
}
