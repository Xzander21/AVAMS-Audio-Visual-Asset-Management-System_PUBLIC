namespace AVEquipmentManager.Shared.DTOs;

public class StaffDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
