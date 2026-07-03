using System.ComponentModel.DataAnnotations;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class UpdateTicketDto
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a piece of equipment.")]
    public int EquipmentId { get; set; }

    public TicketType Type { get; set; } = TicketType.Maintenance;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [MaxLength(200)]
    public string? ReportedBy { get; set; }

    [MaxLength(200)]
    public string? AssignedTo { get; set; }

    [MaxLength(2000)]
    public string? Resolution { get; set; }

    /// <summary>Reference to the IT Specialist's ticket in the existing service desk (optional).</summary>
    [MaxLength(100)]
    public string? ExternalTicketId { get; set; }
}
