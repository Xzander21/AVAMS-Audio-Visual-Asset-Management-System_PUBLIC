using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.Models;

public class Ticket
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public int EquipmentId { get; set; }

    [ForeignKey(nameof(EquipmentId))]
    public Equipment? Equipment { get; set; }

    public TicketType Type { get; set; } = TicketType.Maintenance;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [MaxLength(200)]
    public string? ReportedBy { get; set; }

    [MaxLength(200)]
    public string? AssignedTo { get; set; }

    [MaxLength(2000)]
    public string? Resolution { get; set; }

    /// <summary>
    /// Reference to the institution's existing service desk ticket (e.g.,
    /// the FreshService ticket ID that an IT Specialist created for this
    /// equipment defect). AVAMS does not own the service-desk workflow;
    /// this field is the bridge to the per-school IT Specialist process.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalTicketId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>EF Core optimistic-concurrency token (RowVersionInterceptor-managed).</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
