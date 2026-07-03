using AVEquipmentManager.API.Data;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Controllers;

/// <summary>
/// Equipment endpoints — Admin only (single-role AVAMS).
/// Delete is a soft delete: it sets IsArchived = true so the item can be
/// restored later from /api/equipment/archive.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _context;

    public EquipmentController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/equipment?room=Room 1&status=Active&category=Projector
    // Returns NON-archived items only.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetAll(
        [FromQuery] string? room,
        [FromQuery] string? status,
        [FromQuery] string? category)
    {
        var query = _context.Equipment.Where(e => !e.IsArchived);

        if (!string.IsNullOrWhiteSpace(room))
            query = query.Where(e => e.RoomName.ToLower() == room.ToLower());

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<EquipmentStatus>(status, true, out var parsedStatus))
            query = query.Where(e => e.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<AssetCategory>(category, true, out var parsedCategory))
            query = query.Where(e => e.Category == parsedCategory);

        var items = await query.OrderBy(e => e.RoomName).ThenBy(e => e.Name).ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    // GET /api/equipment/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipmentDto>> GetById(int id)
    {
        var item = await _context.Equipment.FindAsync(id);
        if (item == null) return NotFound();
        return Ok(MapToDto(item));
    }

    // GET /api/equipment/rooms — distinct room names from non-archived equipment
    [HttpGet("rooms")]
    public async Task<ActionResult<IEnumerable<string>>> GetRooms()
    {
        var rooms = await _context.Equipment
            .Where(e => !e.IsArchived)
            .Select(e => e.RoomName)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();
        return Ok(rooms);
    }

    // GET /api/equipment/archive — list archived equipment (Admin only)
    [HttpGet("archive")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetArchived()
    {
        var items = await _context.Equipment
            .Where(e => e.IsArchived)
            .OrderByDescending(e => e.ArchivedAt)
            .ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    // POST /api/equipment — Admin + AVStaff per role matrix.
    [HttpPost]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<EquipmentDto>> Create([FromBody] CreateEquipmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _context.Equipment.AnyAsync(e => e.SerialNumber == dto.SerialNumber && !e.IsArchived))
            return Conflict(new { message = $"Serial number '{dto.SerialNumber}' already exists." });

        var equipment = new Equipment
        {
            Name                = dto.Name,
            Category            = dto.Category,
            SerialNumber        = dto.SerialNumber,
            RoomName            = dto.RoomName,
            DateInstalled       = dto.DateInstalled.ToUniversalTime(),
            ExpectedLifeInYears = dto.ExpectedLifeInYears,
            Status              = dto.Status,
            HasAppleTv          = dto.HasAppleTv,
            HasWallSpeaker      = dto.HasWallSpeaker,
            HasRemoteHolder     = dto.HasRemoteHolder,
            Notes               = dto.Notes,
            CreatedAt           = DateTime.UtcNow
        };

        _context.Equipment.Add(equipment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = equipment.Id }, MapToDto(equipment));
    }

    // PUT /api/equipment/{id} — Admin + AVStaff per role matrix.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<EquipmentDto>> Update(int id, [FromBody] UpdateEquipmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();

        if (await _context.Equipment.AnyAsync(e => e.SerialNumber == dto.SerialNumber && e.Id != id && !e.IsArchived))
            return Conflict(new { message = $"Serial number '{dto.SerialNumber}' already exists." });

        equipment.Name                = dto.Name;
        equipment.Category            = dto.Category;
        equipment.SerialNumber        = dto.SerialNumber;
        equipment.RoomName            = dto.RoomName;
        equipment.DateInstalled       = dto.DateInstalled.ToUniversalTime();
        equipment.ExpectedLifeInYears = dto.ExpectedLifeInYears;
        equipment.Status              = dto.Status;
        equipment.HasAppleTv          = dto.HasAppleTv;
        equipment.HasWallSpeaker      = dto.HasWallSpeaker;
        equipment.HasRemoteHolder     = dto.HasRemoteHolder;
        equipment.Notes               = dto.Notes;
        equipment.UpdatedAt           = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(equipment));
    }

    // DELETE /api/equipment/{id} — SOFT DELETE (archive). Admin + AVStaff per role matrix.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();

        if (equipment.Status != EquipmentStatus.Retired && equipment.Status != EquipmentStatus.Decommissioned)
            return BadRequest(new { message = "Equipment can only be archived if its status is Retired or Decommissioned." });

        equipment.IsArchived = true;
        equipment.ArchivedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/equipment/{id}/restore — restore an archived item. Admin + AVStaff.
    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<EquipmentDto>> Restore(int id)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();
        if (!equipment.IsArchived)
            return BadRequest(new { message = "Equipment is not archived." });

        // Refuse restore if serial number collides with a live item
        if (await _context.Equipment.AnyAsync(e => e.SerialNumber == equipment.SerialNumber && e.Id != id && !e.IsArchived))
            return Conflict(new { message = $"Cannot restore: serial number '{equipment.SerialNumber}' is in use by another active item." });

        equipment.IsArchived = false;
        equipment.ArchivedAt = null;
        equipment.UpdatedAt  = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(equipment));
    }

    // DELETE /api/equipment/{id}/purge — permanently delete from the archive.
    // ADMIN ONLY: this is the "Delete Forever" action. AVStaff cannot purge.
    [HttpDelete("{id:int}/purge")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Purge(int id)
    {
        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment == null) return NotFound();
        if (!equipment.IsArchived)
            return BadRequest(new { message = "Only archived equipment can be permanently deleted." });

        _context.Equipment.Remove(equipment);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static EquipmentDto MapToDto(Equipment e) => new()
    {
        Id                  = e.Id,
        Name                = e.Name,
        Category            = e.Category,
        SerialNumber        = e.SerialNumber,
        RoomName            = e.RoomName,
        DateInstalled       = e.DateInstalled,
        ExpectedLifeInYears = e.ExpectedLifeInYears,
        Status              = e.Status,
        HasAppleTv          = e.HasAppleTv,
        HasWallSpeaker      = e.HasWallSpeaker,
        HasRemoteHolder     = e.HasRemoteHolder,
        Notes               = e.Notes,
        CreatedAt           = e.CreatedAt,
        UpdatedAt           = e.UpdatedAt,
        IsArchived          = e.IsArchived,
        ArchivedAt          = e.ArchivedAt
    };
}
