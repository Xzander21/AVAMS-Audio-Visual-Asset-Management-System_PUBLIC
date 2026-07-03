using System.ComponentModel.DataAnnotations;

namespace AVEquipmentManager.Shared.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Student"; // "Admin" | "Staff" | "Student"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
