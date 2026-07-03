using System.ComponentModel.DataAnnotations;

namespace AVEquipmentManager.Shared.Models;

/// <summary>
/// Represents a staff member (teacher) assigned to a specific room.
/// Used by the Tracking system: searching a staff member surfaces the
/// equipment located in their assigned room.
/// </summary>
public class Staff
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Department { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. Archived staff is hidden from normal listings.</summary>
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAt { get; set; }
}
