using LFZ.Application.Abstractions;
using LFZ.Application.DTOs;
using LFZ.Domain.Entities;
using LFZ.Domain.Enums;
using LFZ.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LFZ.Application.Services;

/// <summary>
/// Orchestrates all plot workflow commands. Invariants are enforced by LFZ.Domain.Rules.PlotRules;
/// this service adds persistence, tenant validation and audit context.
/// </summary>
public class PlotCommandService
{
    private const string AllowMultiTenantBlockKey = "Feature.AllowMultiTenantBlock";
    private readonly IApplicationDbContext _context;
    private readonly PlotStatusAuditContext _auditContext;

    public PlotCommandService(IApplicationDbContext context, PlotStatusAuditContext auditContext)
    {
        _context = context;
        _auditContext = auditContext;
    }

    public async Task<PlotRequest> SubmitRequestAsync(CreatePlotRequestDto dto, string requestedByUserId)
    {
        var plot = await GetPlotForUpdateAsync(dto.PlotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        PlotRules.EnsureCanBeRequested(plot);

        var request = new PlotRequest
        {
            PlotId = dto.PlotId,
            Plot = plot,
            TenantId = dto.TenantId,
            RequestedByUserId = requestedByUserId,
            RequestType = dto.RequestType,
            Status = PlotRequestStatus.Pending,
            IntendedUse = dto.IntendedUse,
            RequestedStartDate = dto.RequestedStartDate,
            Notes = dto.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        plot.Status = PlotStatus.PendingReview;
        plot.CurrentTenantId = dto.TenantId;

        _context.PlotRequests.Add(request);
        await SaveWithAuditAsync(requestedByUserId, dto.Notes ?? "Plot request submitted");
        return request;
    }

    public async Task<Plot> ApproveRequestAsync(int requestId, ReviewPlotRequestDto dto, string actorUserId)
    {
        var request = await GetPendingRequestForUpdateAsync(requestId);
        PlotRules.EnsureCanChange(request.Plot);

        request.Status = PlotRequestStatus.Approved;
        request.DecisionByUserId = actorUserId;
        request.DecisionAtUtc = DateTime.UtcNow;

        _context.PlotTenantBlocks.RemoveRange(request.Plot.TenantBlocks);
        request.Plot.Status = PlotStatus.Occupied;
        request.Plot.CurrentTenantId = request.TenantId;

        await SaveWithAuditAsync(actorUserId, dto.Notes ?? "Plot request approved", request.Id);
        return request.Plot;
    }

    public async Task<Plot> RejectRequestAsync(int requestId, ReviewPlotRequestDto dto, string actorUserId)
    {
        var request = await GetPendingRequestForUpdateAsync(requestId);
        PlotRules.EnsureCanChange(request.Plot);

        request.Status = PlotRequestStatus.Rejected;
        request.DecisionByUserId = actorUserId;
        request.DecisionAtUtc = DateTime.UtcNow;

        request.Plot.Status = PlotStatus.Free;
        request.Plot.CurrentTenantId = null;

        await SaveWithAuditAsync(actorUserId, dto.Notes ?? "Plot request rejected", request.Id);
        return request.Plot;
    }

    public async Task<Plot> AllocateAsync(int plotId, DirectAllocatePlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        PlotRules.EnsureCanBeAllocated(plot);

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Occupied;
        plot.CurrentTenantId = dto.TenantId;

        await SaveWithAuditAsync(actorUserId, dto.Notes ?? "Direct allocation");
        return plot;
    }

    public async Task<Plot> BlockAsync(int plotId, BlockPlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        PlotRules.EnsureCanBeBlocked(plot);

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Blocked;
        plot.CurrentTenantId = dto.TenantId;

        await SaveWithAuditAsync(actorUserId, dto.Notes);
        return plot;
    }

    public async Task<Plot> ReleaseAsync(int plotId, ReleasePlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        PlotRules.EnsureCanChange(plot);

        if (plot.Status == PlotStatus.Free && plot.CurrentTenantId is null && plot.TenantBlocks.Count == 0)
        {
            return plot;
        }

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Free;
        plot.CurrentTenantId = null;

        await SaveWithAuditAsync(actorUserId, dto.Reason ?? "Released");
        return plot;
    }

    public async Task<Plot> MultiTenantBlockAsync(int plotId, MultiTenantBlockDto dto, string actorUserId)
    {
        var tenantIds = dto.TenantIds.Distinct().ToArray();
        if (tenantIds.Length == 0)
        {
            throw new PlotRuleViolationException("At least one tenant is required.");
        }

        var plot = await GetPlotForUpdateAsync(plotId);
        PlotRules.EnsureMultiTenantBlockAllowed(plot, await IsMultiTenantBlockFeatureEnabledAsync());

        var existingTenantCount = await _context.Tenants.CountAsync(tenant => tenantIds.Contains(tenant.Id));
        if (existingTenantCount != tenantIds.Length)
        {
            throw new KeyNotFoundException("One or more tenants were not found.");
        }

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks.Where(block => !tenantIds.Contains(block.TenantId)));

        var existingBlockTenantIds = plot.TenantBlocks.Select(block => block.TenantId).ToHashSet();
        foreach (var tenantId in tenantIds.Where(tenantId => !existingBlockTenantIds.Contains(tenantId)))
        {
            plot.TenantBlocks.Add(new PlotTenantBlock
            {
                PlotId = plot.Id,
                TenantId = tenantId,
                BlockedByUserId = actorUserId,
                BlockedAtUtc = DateTime.UtcNow,
                Notes = dto.Notes
            });
        }

        plot.Status = PlotStatus.Blocked;
        plot.CurrentTenantId = null;

        await SaveWithAuditAsync(actorUserId, dto.Notes ?? "Multi-tenant block");
        return plot;
    }

    public async Task<Plot> ToggleMultiTenantBlockAsync(int plotId, ToggleMultiTenantBlockDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        plot.MultiTenantBlockEnabled = dto.Enabled;

        await SaveWithAuditAsync(actorUserId, dto.Enabled ? "Enabled multi-tenant block" : "Disabled multi-tenant block");
        return plot;
    }

    private async Task SaveWithAuditAsync(string actorUserId, string? reason, int? originatingRequestId = null)
    {
        _auditContext.Set(actorUserId, reason, originatingRequestId);
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }
    }

    private async Task<Plot> GetPlotForUpdateAsync(int plotId)
    {
        return await _context.Plots
            .Include(plot => plot.TenantBlocks)
            .FirstOrDefaultAsync(plot => plot.Id == plotId)
            ?? throw new KeyNotFoundException($"Plot {plotId} was not found.");
    }

    private async Task<PlotRequest> GetPendingRequestForUpdateAsync(int requestId)
    {
        var request = await _context.PlotRequests
            .Include(item => item.Plot)
                .ThenInclude(plot => plot.TenantBlocks)
            .FirstOrDefaultAsync(item => item.Id == requestId)
            ?? throw new KeyNotFoundException($"Plot request {requestId} was not found.");

        PlotRules.EnsureRequestIsReviewable(request);
        return request;
    }

    private async Task EnsureTenantExistsAsync(int tenantId)
    {
        if (!await _context.Tenants.AnyAsync(tenant => tenant.Id == tenantId))
        {
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        }
    }

    private async Task<bool> IsMultiTenantBlockFeatureEnabledAsync()
    {
        var setting = await _context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == AllowMultiTenantBlockKey);

        return setting is not null && bool.TryParse(setting.Value, out var enabled) && enabled;
    }
}
