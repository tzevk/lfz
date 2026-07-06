using System.Security.Claims;
using LFZ.Api.Hubs;
using LFZ.Application.DTOs;
using LFZ.Application.Mapping;
using LFZ.Application.Services;
using LFZ.Domain.Entities;
using LFZ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LFZ.Api.Controllers;

[ApiController]
[Route("api/plots")]
public class PlotsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PlotQueryService _queries;
    private readonly PlotCommandService _commands;
    private readonly IHubContext<PlotHub> _plotHub;

    public PlotsController(
        ApplicationDbContext context,
        PlotQueryService queries,
        PlotCommandService commands,
        IHubContext<PlotHub> plotHub)
    {
        _context = context;
        _queries = queries;
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
        return Ok(await _queries.GetPlotsAsync(status, landUseType, phase, search));
    }

    [HttpGet("shape/{shapeId}")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetByShapeId(string shapeId)
    {
        var plot = await _queries.GetPlotByCodeAsync(shapeId);
        if (plot is null)
        {
            return NotFound(new { message = $"Plot shape '{shapeId}' was not found." });
        }

        return Ok(plot);
    }

    [HttpGet("summary")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? status,
        [FromQuery] string? landUseType,
        [FromQuery] string? phase,
        [FromQuery] string? search)
    {
        return Ok(await _queries.GetSummaryAsync(status, landUseType, phase, search));
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

        return Ok(plot.ToDto());
    }

    [HttpPost("requests")]
    [Authorize(Policy = "CanRequestPlot")]
    public async Task<IActionResult> CreateRequest([FromBody] CreatePlotRequestDto dto)
    {
        try
        {
            var request = await _commands.SubmitRequestAsync(dto, GetUserId());
            await PublishPlotStatusChangedAsync(request.Plot);
            return CreatedAtAction(nameof(RequestQueue), new { id = request.Id }, request.ToDto());
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
        return Ok(await _queries.GetPendingRequestsAsync());
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

    private async Task<IActionResult> ExecutePlotCommandAsync(Func<Task<Plot>> command)
    {
        try
        {
            var plot = await command();
            await _context.Entry(plot).Reference(item => item.CurrentTenant).LoadAsync();
            await _context.Entry(plot).Collection(item => item.TenantBlocks).Query().Include(block => block.Tenant).LoadAsync();
            await PublishPlotStatusChangedAsync(plot);
            return Ok(plot.ToDto());
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
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id was not found.");
    }
}
