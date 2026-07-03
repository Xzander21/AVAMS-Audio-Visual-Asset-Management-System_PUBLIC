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
/// Acquisition transactions — the Plan → Acquire → Deploy phase of the
/// ITIL asset lifecycle.
///
/// State transitions:
///   Planned   → Ordered   (via /order)
///   Ordered   → Received  (via /receive)
///   Received  → Deployed  (via /deploy, which creates an Equipment record)
///   anything  → Cancelled (via /cancel, except Deployed which is final)
///
/// Deploy is transaction-proofed via <see cref="IAcquisitionLifecycleService"/>
/// to eliminate the V1 orphan-row failure mode (the original implementation
/// used two separate SaveChangesAsync calls between the Equipment insert
/// and the Acquisition update).
///
/// AUTHORIZATION (per 2026-06-15 client + team agreement):
///   GET                                → Admin, Supervisor, AVStaff
///   Create / Update / Order / Receive
///   / Cancel                           → Admin, AVStaff
///   Deploy (Adding of Equipment)       → Supervisor ONLY (separation of duties)
///   Delete                             → Admin ONLY
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor,AVStaff,Staff")]
public class AcquisitionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAcquisitionLifecycleService _lifecycle;
    public AcquisitionsController(AppDbContext context, IAcquisitionLifecycleService lifecycle)
    {
        _context   = context;
        _lifecycle = lifecycle;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AcquisitionDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] string? category)
    {
        var q = _context.Acquisitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AcquisitionStatus>(status, true, out var st))
            q = q.Where(a => a.Status == st);
        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<AssetCategory>(category, true, out var cat))
            q = q.Where(a => a.Category == cat);
        var items = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AcquisitionDto>> GetById(int id)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        return Ok(MapToDto(a));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AcquisitionSummaryDto>> Summary()
    {
        var groups = await _context.Acquisitions
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return Ok(new AcquisitionSummaryDto
        {
            Planned   = groups.FirstOrDefault(g => g.Status == AcquisitionStatus.Planned)?.Count   ?? 0,
            Ordered   = groups.FirstOrDefault(g => g.Status == AcquisitionStatus.Ordered)?.Count   ?? 0,
            Received  = groups.FirstOrDefault(g => g.Status == AcquisitionStatus.Received)?.Count  ?? 0,
            Deployed  = groups.FirstOrDefault(g => g.Status == AcquisitionStatus.Deployed)?.Count  ?? 0,
            Cancelled = groups.FirstOrDefault(g => g.Status == AcquisitionStatus.Cancelled)?.Count ?? 0,
            Total     = groups.Sum(g => g.Count)
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<AcquisitionDto>> Create([FromBody] CreateAcquisitionDto dto)
    {
        var a = new Acquisition
        {
            ItemName            = dto.ItemName,
            Category            = dto.Category,
            Vendor              = dto.Vendor,
            PurchaseOrderNumber = dto.PurchaseOrderNumber,
            UnitCost            = dto.UnitCost,
            Quantity            = dto.Quantity,
            IntendedRoom        = dto.IntendedRoom,
            Status              = AcquisitionStatus.Planned,
            RequestedBy         = User.FindFirstValue(ClaimTypes.Name),
            Notes               = dto.Notes,
            CreatedAt           = DateTime.UtcNow
        };
        _context.Acquisitions.Add(a);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = a.Id }, MapToDto(a));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<AcquisitionDto>> Update(int id, [FromBody] UpdateAcquisitionDto dto)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        if (a.Status == AcquisitionStatus.Deployed)
            return BadRequest(new { message = "Deployed acquisitions are read-only." });

        a.ItemName            = dto.ItemName;
        a.Category            = dto.Category;
        a.Vendor              = dto.Vendor;
        a.PurchaseOrderNumber = dto.PurchaseOrderNumber;
        a.UnitCost            = dto.UnitCost;
        a.Quantity            = dto.Quantity;
        a.IntendedRoom        = dto.IntendedRoom;
        a.Notes               = dto.Notes;
        a.UpdatedAt           = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(a));
    }

    [HttpPost("{id:int}/order")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<AcquisitionDto>> Order(int id)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        if (a.Status != AcquisitionStatus.Planned)
            return BadRequest(new { message = $"Only Planned acquisitions can be ordered (current: {a.Status})." });
        a.Status = AcquisitionStatus.Ordered;
        a.OrderedAt = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(a));
    }

    [HttpPost("{id:int}/receive")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<AcquisitionDto>> Receive(int id)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        if (a.Status != AcquisitionStatus.Ordered)
            return BadRequest(new { message = $"Only Ordered acquisitions can be received (current: {a.Status})." });
        a.Status = AcquisitionStatus.Received;
        a.ReceivedAt = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(a));
    }

    /// <summary>Deploy: creates an Equipment record and links it. Status → Deployed.
    /// Transaction-proofed: Equipment insert + Acquisition update + audit logs
    /// commit atomically inside one IDbContextTransaction (V1 fix).
    /// SUPERVISOR ONLY — this is the "Adding of Equipment" approval gate per the
    /// client's separation-of-duties directive (2026-06-15).</summary>
    [HttpPost("{id:int}/deploy")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<AcquisitionDto>> Deploy(int id, [FromBody] DeployAcquisitionDto dto, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
        var r = await _lifecycle.DeployAsync(id, dto, user, ct);
        return r.Success ? Ok(MapToDto(r.Value!)) : BadRequest(new { message = r.Error });
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<AcquisitionDto>> Cancel(int id)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        if (a.Status == AcquisitionStatus.Deployed)
            return BadRequest(new { message = "Deployed acquisitions cannot be cancelled." });
        a.Status = AcquisitionStatus.Cancelled;
        a.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(a));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _context.Acquisitions.FindAsync(id);
        if (a == null) return NotFound();
        if (a.Status != AcquisitionStatus.Cancelled)
            return BadRequest(new { message = "Only Cancelled acquisitions can be permanently deleted." });
        _context.Acquisitions.Remove(a);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static AcquisitionDto MapToDto(Acquisition a) => new()
    {
        Id                  = a.Id,
        ItemName            = a.ItemName,
        Category            = a.Category,
        Vendor              = a.Vendor,
        PurchaseOrderNumber = a.PurchaseOrderNumber,
        UnitCost            = a.UnitCost,
        Quantity            = a.Quantity,
        IntendedRoom        = a.IntendedRoom,
        Status              = a.Status,
        RequestedBy         = a.RequestedBy,
        Notes               = a.Notes,
        CreatedAt           = a.CreatedAt,
        OrderedAt           = a.OrderedAt,
        ReceivedAt          = a.ReceivedAt,
        DeployedAt          = a.DeployedAt,
        UpdatedAt           = a.UpdatedAt,
        DeployedEquipmentId = a.DeployedEquipmentId
    };
}

public class AcquisitionSummaryDto
{
    public int Total     { get; set; }
    public int Planned   { get; set; }
    public int Ordered   { get; set; }
    public int Received  { get; set; }
    public int Deployed  { get; set; }
    public int Cancelled { get; set; }
}
