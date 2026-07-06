using AssetRequestApi.Data;
using AssetRequestApi.DTOs;
using AssetRequestApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AssetRequestApi.Services;

public class PlotCommandService
{
    private const string AllowMultiTenantBlockKey = "Feature.AllowMultiTenantBlock";
    private readonly ApplicationDbContext _context;
    private readonly PlotStatusAuditContext _auditContext;

    public PlotCommandService(ApplicationDbContext context, PlotStatusAuditContext auditContext)
    {
        _context = context;
        _auditContext = auditContext;
    }

    public async Task<PlotRequest> SubmitRequestAsync(CreatePlotRequestDto dto, string requestedByUserId)
    {
        var plot = await GetPlotForUpdateAsync(dto.PlotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        EnsurePlotCanChange(plot);

        if (plot.Status != PlotStatus.Free)
        {
            throw new InvalidOperationException($"Only free plots can be requested. Plot {plot.Code} is {plot.Status}.");
        }

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
        _auditContext.Set(requestedByUserId, dto.Notes ?? "Plot request submitted");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return request;
    }

    public async Task<Plot> ApproveRequestAsync(int requestId, ReviewPlotRequestDto dto, string actorUserId)
    {
        var request = await GetPendingRequestForUpdateAsync(requestId);
        EnsurePlotCanChange(request.Plot);

        request.Status = PlotRequestStatus.Approved;
        request.DecisionByUserId = actorUserId;
        request.DecisionAtUtc = DateTime.UtcNow;

        _context.PlotTenantBlocks.RemoveRange(request.Plot.TenantBlocks);
        request.Plot.Status = PlotStatus.Occupied;
        request.Plot.CurrentTenantId = request.TenantId;

        _auditContext.Set(actorUserId, dto.Notes ?? "Plot request approved", request.Id);
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return request.Plot;
    }

    public async Task<Plot> RejectRequestAsync(int requestId, ReviewPlotRequestDto dto, string actorUserId)
    {
        var request = await GetPendingRequestForUpdateAsync(requestId);
        EnsurePlotCanChange(request.Plot);

        request.Status = PlotRequestStatus.Rejected;
        request.DecisionByUserId = actorUserId;
        request.DecisionAtUtc = DateTime.UtcNow;

        request.Plot.Status = PlotStatus.Free;
        request.Plot.CurrentTenantId = null;

        _auditContext.Set(actorUserId, dto.Notes ?? "Plot request rejected", request.Id);
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return request.Plot;
    }

    public async Task<Plot> AllocateAsync(int plotId, DirectAllocatePlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        EnsurePlotCanChange(plot);

        if (plot.Status != PlotStatus.Free)
        {
            throw new InvalidOperationException($"Only free plots can be allocated. Plot {plot.Code} is {plot.Status}.");
        }

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Occupied;
        plot.CurrentTenantId = dto.TenantId;

        _auditContext.Set(actorUserId, dto.Notes ?? "Direct allocation");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return plot;
    }

    public async Task<Plot> BlockAsync(int plotId, BlockPlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        await EnsureTenantExistsAsync(dto.TenantId);
        EnsurePlotCanChange(plot);

        if (plot.Status is PlotStatus.Occupied or PlotStatus.Blocked or PlotStatus.PendingReview)
        {
            throw new InvalidOperationException($"Plot {plot.Code} is already {plot.Status}.");
        }

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Blocked;
        plot.CurrentTenantId = dto.TenantId;

        _auditContext.Set(actorUserId, dto.Notes);
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return plot;
    }

    public async Task<Plot> ReleaseAsync(int plotId, ReleasePlotDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        EnsurePlotCanChange(plot);

        if (plot.Status == PlotStatus.Free && plot.CurrentTenantId is null && plot.TenantBlocks.Count == 0)
        {
            return plot;
        }

        _context.PlotTenantBlocks.RemoveRange(plot.TenantBlocks);
        plot.Status = PlotStatus.Free;
        plot.CurrentTenantId = null;

        _auditContext.Set(actorUserId, dto.Reason ?? "Released");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return plot;
    }

    public async Task<Plot> MultiTenantBlockAsync(int plotId, MultiTenantBlockDto dto, string actorUserId)
    {
        var tenantIds = dto.TenantIds.Distinct().ToArray();
        if (tenantIds.Length == 0)
        {
            throw new InvalidOperationException("At least one tenant is required.");
        }

        var plot = await GetPlotForUpdateAsync(plotId);
        EnsurePlotCanChange(plot);

        if (!plot.MultiTenantBlockEnabled || !await IsMultiTenantBlockFeatureEnabledAsync())
        {
            throw new InvalidOperationException("Multi-tenant blocking is not enabled for this plot.");
        }

        if (plot.Status == PlotStatus.Occupied)
        {
            throw new InvalidOperationException($"Plot {plot.Code} is already allocated.");
        }

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

        _auditContext.Set(actorUserId, dto.Notes ?? "Multi-tenant block");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return plot;
    }

    public async Task<Plot> ToggleMultiTenantBlockAsync(int plotId, ToggleMultiTenantBlockDto dto, string actorUserId)
    {
        var plot = await GetPlotForUpdateAsync(plotId);
        plot.MultiTenantBlockEnabled = dto.Enabled;

        _auditContext.Set(actorUserId, dto.Enabled ? "Enabled multi-tenant block" : "Disabled multi-tenant block");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            _auditContext.Clear();
        }

        return plot;
    }

    private async Task<Plot> GetPlotForUpdateAsync(int plotId)
    {
        return await _context.Plots
            .Include(plot => plot.TenantBlocks)
            .FirstOrDefaultAsync(plot => plot.Id == plotId)
            ?? throw new KeyNotFoundException($"Plot {plotId} was not found.");
    }

    private static void EnsurePlotCanChange(Plot plot)
    {
        if (plot.IsLocked)
        {
            throw new InvalidOperationException($"Plot {plot.Code} is locked.");
        }

        if (plot.Status == PlotStatus.Unavailable)
        {
            throw new InvalidOperationException($"Plot {plot.Code} is unavailable.");
        }
    }

    private async Task<PlotRequest> GetPendingRequestForUpdateAsync(int requestId)
    {
        var request = await _context.PlotRequests
            .Include(item => item.Plot)
                .ThenInclude(plot => plot.TenantBlocks)
            .FirstOrDefaultAsync(item => item.Id == requestId)
            ?? throw new KeyNotFoundException($"Plot request {requestId} was not found.");

        if (request.Status != PlotRequestStatus.Pending)
        {
            throw new InvalidOperationException($"Only pending requests can be reviewed. Request {requestId} is {request.Status}.");
        }

        if (request.Plot.Status != PlotStatus.PendingReview)
        {
            throw new InvalidOperationException($"Plot {request.Plot.Code} is not pending review.");
        }

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