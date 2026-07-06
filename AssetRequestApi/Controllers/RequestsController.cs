using System.Security.Claims;
using AssetRequestApi.Data;
using AssetRequestApi.DTOs;
using AssetRequestApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetRequestApi.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RequestsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET /api/requests  -> Viewer, Requester, Allocator, Admin
    [HttpGet]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetAll()
    {
        var requests = await _context.Requests
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    // GET /api/requests/{id}  -> Viewer, Requester, Allocator, Admin
    [HttpGet("{id:int}")]
    [Authorize(Policy = "CanViewDashboard")]
    public async Task<IActionResult> GetById(int id)
    {
        var request = await _context.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (request is null)
        {
            return NotFound(new { message = $"Request {id} not found." });
        }

        return Ok(request);
    }

    // POST /api/requests  -> Requester, Allocator, Admin
    [HttpPost]
    [Authorize(Policy = "CanRequestPlot")]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var request = new RequestItem
        {
            Title = dto.Title,
            Description = dto.Description,
            Status = "Pending",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    // PUT /api/requests/{id}/allocate  -> Allocator, Admin
    [HttpPut("{id:int}/allocate")]
    [Authorize(Policy = "CanAllocatePlot")]
    public async Task<IActionResult> Allocate(int id, [FromBody] AllocateRequestDto dto)
    {
        var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null)
        {
            return NotFound(new { message = $"Request {id} not found." });
        }

        request.AllocatedToUserId = dto.AllocatedToUserId;
        request.Status = "Allocated";
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    // DELETE /api/requests/{id}  -> Admin only
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "CanManageSettings")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null)
        {
            return NotFound(new { message = $"Request {id} not found." });
        }

        _context.Requests.Remove(request);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
