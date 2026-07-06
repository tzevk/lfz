using System.Security.Claims;
using AssetRequestApi.Data;
using AssetRequestApi.DTOs;
using AssetRequestApi.Hubs;
using AssetRequestApi.Models;
using AssetRequestApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AssetRequestApi.Controllers;

[ApiController]
[Route("api/plots")]
public class PlotsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PlotCommandService _commands;
    private readonly IHubContext<PlotHub> _plotHub;

    public PlotsController(ApplicationDbContext context, PlotCommandService commands, IHubContext<PlotHub> plotHub)
    {
        _context = context;
        _commands = commands;
        _plotHub = plotHub;
    }

    [HttpGet]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? landUseType,
        [FromQuery] string? phase,
        [FromQuery] string? search)
    {
        var query = ApplyPlotFilters(BuildPlotReadQuery(), status, landUseType, phase, search);
        var plots = await query.OrderBy(plot => plot.Code).ToListAsync();
        return Ok(plots.Select(ToDto));
    }

    [HttpGet("shape/{shapeId}")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetByShapeId(string shapeId)
    {
        var plot = await BuildPlotReadQuery().FirstOrDefaultAsync(item => item.Code == shapeId);
        if (plot is null)
        {
            return NotFound(new { message = $"Plot shape '{shapeId}' was not found." });
        }

        return Ok(ToDto(plot));
    }

    [HttpGet("summary")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? status,
        [FromQuery] string? landUseType,
        [FromQuery] string? phase,
        [FromQuery] string? search)
    {
        var plots = await ApplyPlotFilters(_context.Plots.AsNoTracking(), status, landUseType, phase, search).ToListAsync();
        var trackedStatuses = new[]
        {
            PlotStatus.Free,
            PlotStatus.Occupied,
            PlotStatus.Blocked,
            PlotStatus.PendingReview
        };
        var summary = new PlotSummaryDto(
            plots.Count,
            plots.Sum(plot => plot.AreaHectares),
            trackedStatuses.ToDictionary(statusItem => statusItem.ToString(), statusItem => plots.Count(plot => plot.Status == statusItem)),
            trackedStatuses.ToDictionary(statusItem => statusItem.ToString(), statusItem => plots.Where(plot => plot.Status == statusItem).Sum(plot => plot.AreaHectares)),
            plots.GroupBy(plot => plot.LandUseType).ToDictionary(group => group.Key, group => group.Sum(plot => plot.AreaHectares)));

        return Ok(summary);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageSettings")]
    public async Task<IActionResult> UpdatePlot(int id, [FromBody] UpdatePlotDto dto)
    {
        var plot = await _context.Plots.FirstOrDefaultAsync(item => item.Id == id);
        if (plot is null)
        {
            return NotFound(new { message = $"Plot {id} was not found." });
        }

        plot.DisplayName = dto.DisplayName;
        plot.LandUseType = dto.LandUseType;
        plot.Phase = dto.Phase;
        plot.HatchColor = dto.HatchColor;
        plot.AreaHectares = dto.AreaHectares;
        plot.SvgPath = dto.SvgPath;
        plot.IsLocked = dto.IsLocked;

        await _context.SaveChangesAsync();
        await _context.Entry(plot).Reference(item => item.CurrentTenant).LoadAsync();
        await _context.Entry(plot).Collection(item => item.TenantBlocks).Query().Include(block => block.Tenant).LoadAsync();
        await PublishPlotStatusChangedAsync(plot);

        return Ok(ToDto(plot));
    }

    [HttpPost("requests")]
    [Authorize(Policy = "CanRequestPlot")]
    public async Task<IActionResult> CreateRequest([FromBody] CreatePlotRequestDto dto)
    {
        try
        {
            var request = await _commands.SubmitRequestAsync(dto, GetUserId());
            await PublishPlotStatusChangedAsync(request.Plot);
            return CreatedAtAction(nameof(RequestQueue), new { id = request.Id }, ToDto(request));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("requests/queue")]
    [Authorize(Policy = "CanAllocatePlot")]
    public async Task<IActionResult> RequestQueue()
    {
        var requests = await _context.PlotRequests
            .AsNoTracking()
            .Include(request => request.Plot)
            .Include(request => request.Tenant)
            .Where(request => request.Status == PlotRequestStatus.Pending)
            .OrderBy(request => request.CreatedAtUtc)
            .ToListAsync();

        return Ok(requests.Select(ToDto));
    }

    [HttpPost("requests/{id:int}/approve")]
    [Authorize(Policy = "CanAllocatePlot")]
    public async Task<IActionResult> ApproveRequest(int id, [FromBody] ReviewPlotRequestDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.ApproveRequestAsync(id, dto, GetUserId()));
    }

    [HttpPost("requests/{id:int}/reject")]
    [Authorize(Policy = "CanAllocatePlot")]
    public async Task<IActionResult> RejectRequest(int id, [FromBody] ReviewPlotRequestDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.RejectRequestAsync(id, dto, GetUserId()));
    }

    [HttpPost("{id:int}/allocate")]
    [Authorize(Policy = "CanAllocatePlot")]
    public async Task<IActionResult> Allocate(int id, [FromBody] DirectAllocatePlotDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.AllocateAsync(id, dto, GetUserId()));
    }

    [HttpPost("{id:int}/block")]
    [Authorize(Policy = "CanBlockPlot")]
    public async Task<IActionResult> Block(int id, [FromBody] BlockPlotDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.BlockAsync(id, dto, GetUserId()));
    }

    [HttpPost("{id:int}/release")]
    [Authorize(Policy = "CanManageSettings")]
    public async Task<IActionResult> Release(int id, [FromBody] ReleasePlotDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.ReleaseAsync(id, dto, GetUserId()));
    }

    [HttpPost("{id:int}/multi-tenant-block")]
    [Authorize(Policy = "CanBlockPlot")]
    public async Task<IActionResult> MultiTenantBlock(int id, [FromBody] MultiTenantBlockDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.MultiTenantBlockAsync(id, dto, GetUserId()));
    }

    [HttpPatch("{id:int}/multi-tenant-block")]
    [Authorize(Policy = "CanManageSettings")]
    public async Task<IActionResult> ToggleMultiTenantBlock(int id, [FromBody] ToggleMultiTenantBlockDto dto)
    {
        return await ExecutePlotCommandAsync(() => _commands.ToggleMultiTenantBlockAsync(id, dto, GetUserId()));
    }

    private IQueryable<Plot> BuildPlotReadQuery()
    {
        return _context.Plots
            .AsNoTracking()
            .Include(plot => plot.CurrentTenant)
            .Include(plot => plot.TenantBlocks)
                .ThenInclude(block => block.Tenant)
            .AsQueryable();
    }

    private async Task<IActionResult> ExecutePlotCommandAsync(Func<Task<Plot>> command)
    {
        try
        {
            var plot = await command();
            await _context.Entry(plot).Reference(item => item.CurrentTenant).LoadAsync();
            await _context.Entry(plot).Collection(item => item.TenantBlocks).Query().Include(block => block.Tenant).LoadAsync();
            await PublishPlotStatusChangedAsync(plot);
            return Ok(ToDto(plot));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private static IQueryable<Plot> ApplyPlotFilters(
        IQueryable<Plot> query,
        string? status,
        string? landUseType,
        string? phase,
        string? search)
    {
        if (!string.IsNullOrWhiteSpace(status) && TryParsePlotStatus(status, out var parsedStatus))
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

    private static bool TryParsePlotStatus(string status, out PlotStatus parsedStatus)
    {
        return Enum.TryParse(status.Replace(" ", string.Empty), true, out parsedStatus);
    }

    private Task PublishPlotStatusChangedAsync(Plot? plot)
    {
        if (plot is null)
        {
            return Task.CompletedTask;
        }

        return _plotHub.Clients.All.SendAsync("PlotStatusChanged", new
        {
            plot.Id,
            plot.Code,
            Status = plot.Status.ToString(),
            CurrentTenant = plot.CurrentTenant?.Name,
            plot.Phase,
            plot.LandUseType,
            plot.HatchColor,
            plot.MultiTenantBlockEnabled,
            RowVersion = Convert.ToBase64String(plot.RowVersion)
        });
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id was not found.");
    }

    private static PlotDto ToDto(Plot plot)
    {
        return new PlotDto(
            plot.Id,
            plot.Code,
            plot.DisplayName,
            plot.LandUseType,
            plot.Phase,
            plot.HatchColor,
            plot.AreaHectares,
            plot.Status.ToString(),
            plot.CurrentTenant is null ? null : ToDto(plot.CurrentTenant),
            plot.SvgPath,
            plot.Centroid is null ? null : new PlotCentroidDto(plot.Centroid.X, plot.Centroid.Y),
            plot.IsLocked,
            plot.MultiTenantBlockEnabled,
            Convert.ToBase64String(plot.RowVersion),
            plot.TenantBlocks.Select(block => new PlotTenantBlockDto(
                block.TenantId,
                block.Tenant.Name,
                block.BlockedAtUtc,
                block.Notes)).ToList());
    }

    private static TenantDto ToDto(Tenant tenant)
    {
        return new TenantDto(tenant.Id, tenant.Name, tenant.LegalName, tenant.Contact, tenant.Industry);
    }

    private static PlotRequestDto ToDto(PlotRequest request)
    {
        return new PlotRequestDto(
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
}