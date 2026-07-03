namespace AVEquipmentManager.Shared.DTOs;

/// <summary>
/// Result of a staff tracking lookup: returns the staff member and
/// every piece of equipment in their assigned room.
/// </summary>
public class StaffTrackingDto
{
    public StaffDto Staff { get; set; } = new();
    public List<EquipmentDto> Equipment { get; set; } = new();
}
