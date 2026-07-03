using System.ComponentModel.DataAnnotations;

namespace AVEquipmentManager.Shared.DTOs;

public class CreateStaffDto
{
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
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }
}
