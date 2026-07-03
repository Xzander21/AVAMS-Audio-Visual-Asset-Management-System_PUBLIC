using System.Security.Claims;
using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Controllers;

/// <summary>
/// Maintenance tickets. Authorization model per client agreement
/// (2026-06-15):
///   GET  (all/by-id/summary) → Admin, Supervisor, AVStaff, ITSpecialist
///                              (ITSpecialist sees only their own tickets)
///   POST (submit ticket)     → Admin, Supervisor, AVStaff, ITSpecialist
///                              (ITSpecialist is the primary submitter)
///   PUT  (full update)       → Admin, Supervisor, AVStaff
///   POST /{id}/acknowledge   → Admin, Supervisor, AVStaff   (tx-proof)
///   POST /{id}/resolve       → Admin, Supervisor, AVStaff   (tx-proof)
///   POST /{id}/close         → Admin, Supervisor            (tx-proof)
///   DELETE                   → Admin only
///
/// State-changing endpoints delegate to ITicketLifecycleService so the
/// Ticket + Equipment.Status + LifecycleLog writes commit atomically.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITicketLifecycleService _lifecycle;

    public TicketsController(AppDbContext context, ITicketLifecycleService lifecycle)
    {
        _context   = context;
        _lifecycle = lifecycle;
    }

    /// <summary>
    /// Normalises both the legacy "Student" role and the new "ITSpecialist"
    /// role to a single boolean so the existing scoping logic continues to
    /// work for users with either role string in their JWT.
    /// </summary>
    private bool IsITSpecialist() =>
        User.IsInRole(Roles.ITSpecialist) || User.IsInRole("Student");

    // GET /api/tickets?status=Open&priority=High&equipmentId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? type,
        [FromQuery] int? equipmentId)
    {
        var query = _context.Tickets.Include(t => t.Equipment).AsQueryable();

        // ITSpecialists can only see tickets they themselves submitted.
        if (IsITSpecialist())
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            query = query.Where(t => t.ReportedBy == username);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<TicketStatus>(status, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(priority) &&
            Enum.TryParse<TicketPriority>(priority, true, out var parsedPriority))
            query = query.Where(t => t.Priority == parsedPriority);

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<TicketType>(type, true, out var parsedType))
            query = query.Where(t => t.Type == parsedType);

        if (equipmentId.HasValue)
            query = query.Where(t => t.EquipmentId == equipmentId.Value);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(items.Select(MapToDto));
    }

    // GET /api/tickets/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetById(int id)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Equipment)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();
        return Ok(MapToDto(ticket));
    }

    // GET /api/tickets/summary
    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        // ITSpecialists see stats scoped to their own tickets only.
        var baseQuery = _context.Tickets.AsQueryable();
        if (IsITSpecialist())
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            baseQuery = baseQuery.Where(t => t.ReportedBy == username);
        }

        var summary = new
        {
            Total      = await baseQuery.CountAsync(),
            Open       = await baseQuery.CountAsync(t => t.Status == TicketStatus.Open),
            InProgress = await baseQuery.CountAsync(t => t.Status == TicketStatus.InProgress),
            Resolved   = await baseQuery.CountAsync(t => t.Status == TicketStatus.Resolved),
            Closed     = await baseQuery.CountAsync(t => t.Status == TicketStatus.Closed),
            Critical   = await baseQuery.CountAsync(t => t.Priority == TicketPriority.Critical
                                                      && t.Status != TicketStatus.Closed
                                                      && t.Status != TicketStatus.Resolved),
        };
        return Ok(summary);
    }

    // POST /api/tickets  — transaction-proofed; delegates to TicketLifecycleService.
    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create([FromBody] CreateTicketDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var role = IsITSpecialist() ? Roles.ITSpecialist : (User.IsInRole(Roles.Admin) ? Roles.Admin : null);

        var r = await _lifecycle.SubmitAsync(dto, user, role, ct);
        if (!r.Success) return BadRequest(new { message = r.Error });

        return CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, MapToDto(r.Value!));
    }

    // POST /api/tickets/{id}/acknowledge  — Open → InProgress (tx-proof).
    [HttpPost("{id:int}/acknowledge")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Supervisor + "," + Roles.AVStaff)]
    public async Task<ActionResult<TicketDto>> Acknowledge(int id, [FromBody] AcknowledgeTicketDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.AssignedTo))
            return BadRequest(new { message = "AssignedTo is required." });

        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.AcknowledgeAsync(id, dto.AssignedTo, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // POST /api/tickets/{id}/resolve  — Open/InProgress → Resolved (tx-proof).
    [HttpPost("{id:int}/resolve")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Supervisor + "," + Roles.AVStaff)]
    public async Task<ActionResult<TicketDto>> Resolve(int id, [FromBody] ResolveTicketDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Resolution))
            return BadRequest(new { message = "Resolution note is required." });

        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.ResolveAsync(id, dto.Resolution, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // POST /api/tickets/{id}/close  — Resolved → Closed (tx-proof).
    [HttpPost("{id:int}/close")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Supervisor)]
    public async Task<ActionResult<TicketDto>> Close(int id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.CloseAsync(id, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // PUT /api/tickets/{id}  — full update (Admin / Supervisor / AVStaff only).
    // Note: state transitions should go through /acknowledge, /resolve, /close
    // for the transaction-proof path. This endpoint remains for backwards
    // compatibility with the existing Tickets.razor UI.
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Supervisor + "," + Roles.AVStaff + ",Staff")]
    public async Task<ActionResult<TicketDto>> Update(int id, [FromBody] UpdateTicketDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ticket = await _context.Tickets
            .Include(t => t.Equipment)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        var equipmentExists = await _context.Equipment.AnyAsync(e => e.Id == dto.EquipmentId);
        if (!equipmentExists)
            return BadRequest(new { message = $"Equipment with ID {dto.EquipmentId} not found." });

        var wasResolved = ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed;
        var isNowResolved = dto.Status == TicketStatus.Resolved || dto.Status == TicketStatus.Closed;

        ticket.Title            = dto.Title;
        ticket.Description      = dto.Description;
        ticket.EquipmentId      = dto.EquipmentId;
        ticket.Type             = dto.Type;
        ticket.Priority         = dto.Priority;
        ticket.Status           = dto.Status;
        ticket.ReportedBy       = dto.ReportedBy;
        ticket.AssignedTo       = dto.AssignedTo;
        ticket.Resolution       = dto.Resolution;
        ticket.ExternalTicketId = dto.ExternalTicketId;
        ticket.UpdatedAt        = DateTime.UtcNow;

        if (!wasResolved && isNowResolved)
            ticket.ResolvedAt = DateTime.UtcNow;
        else if (wasResolved && !isNowResolved)
            ticket.ResolvedAt = null;

        await _context.SaveChangesAsync();

        // Reload navigation property if equipment changed
        if (ticket.Equipment?.Id != dto.EquipmentId)
            await _context.Entry(ticket).Reference(t => t.Equipment).LoadAsync();

        return Ok(MapToDto(ticket));
    }

    // DELETE /api/tickets/{id}  — Admin only.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static TicketDto MapToDto(Ticket t) => new()
    {
        Id            = t.Id,
        Title         = t.Title,
        Description   = t.Description,
        EquipmentId   = t.EquipmentId,
        EquipmentName = t.Equipment?.Name ?? string.Empty,
        EquipmentRoom = t.Equipment?.RoomName ?? string.Empty,
        Type          = t.Type,
        Priority      = t.Priority,
        Status        = t.Status,
        ReportedBy       = t.ReportedBy,
        AssignedTo       = t.AssignedTo,
        Resolution       = t.Resolution,
        ExternalTicketId = t.ExternalTicketId,
        CreatedAt        = t.CreatedAt,
        UpdatedAt     = t.UpdatedAt,
        ResolvedAt    = t.ResolvedAt
    };
}

// ── Small transition DTOs (used only by acknowledge/resolve endpoints) ──
public class AcknowledgeTicketDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string AssignedTo { get; set; } = string.Empty;
}

public class ResolveTicketDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string Resolution { get; set; } = string.Empty;
}
