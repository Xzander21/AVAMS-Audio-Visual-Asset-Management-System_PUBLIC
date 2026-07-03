using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class TicketDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string EquipmentRoom { get; set; } = string.Empty;
    public TicketType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    /// <summary>Optional reference to the IT Specialist's ticket in the existing institutional service desk.</summary>
    public string? ExternalTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
