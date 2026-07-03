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
/// Disposal transactions. Each disposal moves through:
///   Pending → Approved → Disposed   (or Cancelled at any point)
/// When a Disposal reaches the Disposed state, the linked equipment is
/// automatically set to Decommissioned.
///
/// Read endpoints (GetAll, GetById, Summary) still talk to AppDbContext
/// directly. Write endpoints (Create, Approve, Dispose, Cancel) delegate
/// to <see cref="IDisposalLifecycleService"/> which wraps each state
/// change in an explicit IDbContextTransaction with RowVersion-based
/// optimistic concurrency and an immutable LifecycleLog audit row.
///
/// AUTHORIZATION (per 2026-06-15 client + team agreement):
///   GET       → Admin, Supervisor, AVStaff
///   Create    → Admin, AVStaff           (Supervisor cannot author new disposals)
///   Update    → Admin, AVStaff
///   Approve   → Supervisor ONLY          (Admin/AVStaff cannot approve — separation of duties)
///   Dispose   → Supervisor ONLY          (Admin/AVStaff cannot finalise)
///   Cancel    → Admin, AVStaff
///   Delete    → Admin ONLY               ("delete forever")
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor,AVStaff,Staff")]
public class DisposalsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDisposalLifecycleService _lifecycle;

    public DisposalsController(AppDbContext context, IDisposalLifecycleService lifecycle)
    {
        _context   = context;
        _lifecycle = lifecycle;
    }

    // GET /api/disposals?status=Pending&method=Recycled
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DisposalDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? method)
    {
        var query = _context.Disposals.Include(d => d.Equipment).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<DisposalStatus>(status, true, out var st))
            query = query.Where(d => d.Status == st);

        if (!string.IsNullOrWhiteSpace(method) &&
            Enum.TryParse<DisposalMethod>(method, true, out var mt))
            query = query.Where(d => d.Method == mt);

        var items = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    // GET /api/disposals/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DisposalDto>> GetById(int id)
    {
        var d = await _context.Disposals.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        return Ok(MapToDto(d));
    }

    // GET /api/disposals/summary  — counts per status (for dashboard)
    [HttpGet("summary")]
    public async Task<ActionResult<DisposalSummaryDto>> Summary()
    {
        var groups = await _context.Disposals
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new DisposalSummaryDto
        {
            Pending  = groups.FirstOrDefault(g => g.Status == DisposalStatus.Pending)?.Count  ?? 0,
            Approved = groups.FirstOrDefault(g => g.Status == DisposalStatus.Approved)?.Count ?? 0,
            Disposed = groups.FirstOrDefault(g => g.Status == DisposalStatus.Disposed)?.Count ?? 0,
            Cancelled= groups.FirstOrDefault(g => g.Status == DisposalStatus.Cancelled)?.Count?? 0,
            Total    = groups.Sum(g => g.Count)
        });
    }

    // POST /api/disposals  — initiate a disposal (Pending)
    // Transaction-proofed: delegates to DisposalLifecycleService which
    // wraps the insert + LifecycleLog write in one IDbContextTransaction.
    [HttpPost]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<DisposalDto>> Create([FromBody] CreateDisposalDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.CreateAsync(
            dto.EquipmentId, dto.Reason, dto.Method, dto.DisposalNotes, user, ct);
        return r.Success
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, MapToDto(r.Value!))
            : BadRequest(new { message = r.Error });
    }

    // PUT /api/disposals/{id}  — edit pending fields (reason, method, notes)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<DisposalDto>> Update(int id, [FromBody] UpdateDisposalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var d = await _context.Disposals.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();

        if (d.Status == DisposalStatus.Disposed)
            return BadRequest(new { message = "Disposed transactions are read-only." });

        d.Reason        = dto.Reason;
        d.Method        = dto.Method;
        d.DisposalNotes = dto.DisposalNotes;
        // Status updates are handled via /approve, /dispose, /cancel endpoints
        d.UpdatedAt     = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(d));
    }

    // POST /api/disposals/{id}/approve  — transaction-proofed
    // SUPERVISOR ONLY: per client agreement, only the Supervisor can authorise
    // a disposal. Admin and AVStaff cannot — strict separation of duties.
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<DisposalDto>> Approve(int id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.ApproveAsync(id, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // POST /api/disposals/{id}/dispose  — transaction-proofed multi-row write
    // SUPERVISOR ONLY: terminal approval gate.
    //
    // 2026-06-16 update: optional spare-equipment replacement. Body may carry
    // { "replacementEquipmentId": <id> } to promote a Reserved unit into Active
    // and inherit the disposed unit's room, all in the same DB transaction.
    [HttpPost("{id:int}/dispose")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<DisposalDto>> Dispose(
        int id,
        [FromBody] DisposeRequest? body,
        CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r    = await _lifecycle.DisposeAsync(id, user, body?.ReplacementEquipmentId, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // POST /api/disposals/{id}/cancel  — transaction-proofed
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<DisposalDto>> Cancel(int id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.CancelAsync(id, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    // DELETE /api/disposals/{id}  — only allowed for Cancelled records. Admin only.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _context.Disposals.FindAsync(id);
        if (d == null) return NotFound();
        if (d.Status != DisposalStatus.Cancelled)
            return BadRequest(new { message = "Only Cancelled disposal records can be deleted." });

        _context.Disposals.Remove(d);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static DisposalDto MapToDto(Disposal d) => new()
    {
        Id                         = d.Id,
        EquipmentId                = d.EquipmentId,
        EquipmentName              = d.Equipment?.Name ?? "",
        EquipmentSerial            = d.Equipment?.SerialNumber ?? "",
        EquipmentRoom              = d.Equipment?.RoomName ?? "",
        Reason                     = d.Reason,
        Method                     = d.Method,
        Status                     = d.Status,
        RequestedBy                = d.RequestedBy,
        ApprovedBy                 = d.ApprovedBy,
        DisposalNotes              = d.DisposalNotes,
        CreatedAt                  = d.CreatedAt,
        ApprovedAt                 = d.ApprovedAt,
        DisposedAt                 = d.DisposedAt,
        UpdatedAt                  = d.UpdatedAt,
        ReplacementEquipmentId     = d.ReplacementEquipmentId,
        ReplacementEquipmentName   = d.ReplacementEquipment?.Name,
        ReplacementEquipmentSerial = d.ReplacementEquipment?.SerialNumber
    };
}

/// <summary>Optional body for POST /api/disposals/{id}/dispose. May be omitted entirely.</summary>
public class DisposeRequest
{
    /// <summary>If set, the indicated Reserved equipment is promoted to Active and assigned to the disposed unit's room.</summary>
    public int? ReplacementEquipmentId { get; set; }
}

public class DisposalSummaryDto
{
    public int Total     { get; set; }
    public int Pending   { get; set; }
    public int Approved  { get; set; }
    public int Disposed  { get; set; }
    public int Cancelled { get; set; }
}
