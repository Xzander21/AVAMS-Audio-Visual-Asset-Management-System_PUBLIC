using AVEquipmentManager.API.Data;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Controllers;

/// <summary>
/// Staff (Teacher) endpoints. Admin-only.
/// Delete is a soft delete: sets IsArchived = true. Restore via /restore.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _context;

    public StaffController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/staff?search=Reyes&room=Room 1&department=Math
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? room,
        [FromQuery] string? department)
    {
        var query = _context.Staff.Where(x => !x.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(s) ||
                x.EmployeeId.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(room))
            query = query.Where(x => x.RoomNumber.ToLower() == room.ToLower());

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(x => x.Department != null && x.Department.ToLower() == department.ToLower());

        var items = await query.OrderBy(x => x.Name).ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    // GET /api/staff/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffDto>> GetById(int id)
    {
        var item = await _context.Staff.FindAsync(id);
        if (item == null) return NotFound();
        return Ok(MapToDto(item));
    }

    // GET /api/staff/{id}/tracking
    [HttpGet("{id:int}/tracking")]
    public async Task<ActionResult<StaffTrackingDto>> Track(int id)
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null || staff.IsArchived) return NotFound();

        var equipment = await _context.Equipment
            .Where(e => !e.IsArchived && e.RoomName.ToLower() == staff.RoomNumber.ToLower())
            .OrderBy(e => e.Name)
            .ToListAsync();

        return Ok(new StaffTrackingDto
        {
            Staff     = MapToDto(staff),
            Equipment = equipment.Select(MapEquipmentToDto).ToList()
        });
    }

    // GET /api/staff/departments
    [HttpGet("departments")]
    public async Task<ActionResult<IEnumerable<string>>> GetDepartments()
    {
        var items = await _context.Staff
            .Where(s => !s.IsArchived && s.Department != null && s.Department != "")
            .Select(s => s.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
        return Ok(items);
    }

    // GET /api/staff/archive (Admin only)
    [HttpGet("archive")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<StaffDto>>> GetArchived()
    {
        var items = await _context.Staff
            .Where(s => s.IsArchived)
            .OrderByDescending(s => s.ArchivedAt)
            .ToListAsync();
        return Ok(items.Select(MapToDto));
    }

    // POST /api/staff — Admin + AVStaff per role matrix.
    [HttpPost]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<StaffDto>> Create([FromBody] CreateStaffDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _context.Staff.AnyAsync(s => s.EmployeeId == dto.EmployeeId && !s.IsArchived))
            return Conflict(new { message = $"Employee ID '{dto.EmployeeId}' already exists." });

        var staff = new Staff
        {
            Name       = dto.Name,
            EmployeeId = dto.EmployeeId,
            RoomNumber = dto.RoomNumber,
            Department = dto.Department,
            Email      = dto.Email,
            Phone      = dto.Phone,
            CreatedAt  = DateTime.UtcNow
        };

        _context.Staff.Add(staff);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = staff.Id }, MapToDto(staff));
    }

    // PUT /api/staff/{id} — Admin + AVStaff per role matrix.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<StaffDto>> Update(int id, [FromBody] UpdateStaffDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var staff = await _context.Staff.FindAsync(id);
        if (staff == null) return NotFound();

        if (await _context.Staff.AnyAsync(s => s.EmployeeId == dto.EmployeeId && s.Id != id && !s.IsArchived))
            return Conflict(new { message = $"Employee ID '{dto.EmployeeId}' already exists." });

        staff.Name       = dto.Name;
        staff.EmployeeId = dto.EmployeeId;
        staff.RoomNumber = dto.RoomNumber;
        staff.Department = dto.Department;
        staff.Email      = dto.Email;
        staff.Phone      = dto.Phone;
        staff.UpdatedAt  = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(staff));
    }

    // DELETE /api/staff/{id} — SOFT DELETE (archive). Admin + AVStaff per role matrix.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null) return NotFound();

        staff.IsArchived = true;
        staff.ArchivedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/staff/{id}/restore — Admin + AVStaff per role matrix.
    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Admin,AVStaff,Staff")]
    public async Task<ActionResult<StaffDto>> Restore(int id)
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null) return NotFound();
        if (!staff.IsArchived)
            return BadRequest(new { message = "Staff record is not archived." });

        if (await _context.Staff.AnyAsync(s => s.EmployeeId == staff.EmployeeId && s.Id != id && !s.IsArchived))
            return Conflict(new { message = $"Cannot restore: Employee ID '{staff.EmployeeId}' is in use by another active record." });

        staff.IsArchived = false;
        staff.ArchivedAt = null;
        staff.UpdatedAt  = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(staff));
    }

    // DELETE /api/staff/{id}/purge — permanently delete from archive.
    // ADMIN ONLY: this is the "Delete Forever" action. AVStaff cannot purge.
    [HttpDelete("{id:int}/purge")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Purge(int id)
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null) return NotFound();
        if (!staff.IsArchived)
            return BadRequest(new { message = "Only archived staff records can be permanently deleted." });

        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static StaffDto MapToDto(Staff s) => new()
    {
        Id         = s.Id,
        Name       = s.Name,
        EmployeeId = s.EmployeeId,
        RoomNumber = s.RoomNumber,
        Department = s.Department,
        Email      = s.Email,
        Phone      = s.Phone,
        CreatedAt  = s.CreatedAt,
        UpdatedAt  = s.UpdatedAt,
        IsArchived = s.IsArchived,
        ArchivedAt = s.ArchivedAt
    };

    private static EquipmentDto MapEquipmentToDto(Equipment e) => new()
    {
        Id                  = e.Id,
        Name                = e.Name,
        SerialNumber        = e.SerialNumber,
        RoomName            = e.RoomName,
        DateInstalled       = e.DateInstalled,
        ExpectedLifeInYears = e.ExpectedLifeInYears,
        Status              = e.Status,
        Notes               = e.Notes,
        CreatedAt           = e.CreatedAt,
        UpdatedAt           = e.UpdatedAt,
        IsArchived          = e.IsArchived,
        ArchivedAt          = e.ArchivedAt
    };
}
