using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AVEquipmentManager.Shared.Models;

/// <summary>
/// Append-only audit row written every time an Equipment, Disposal,
/// Acquisition, or Ticket crosses a state. Written exclusively by the
/// lifecycle services inside the SAME IDbContextTransaction as the
/// underlying state change, so the log and the live row are always
/// consistent. No controller exposes a write endpoint for this entity.
///
/// EntityType is a polymorphic discriminator ("Disposal", "Acquisition",
/// "Equipment", "Ticket"). Read paths filter by (EntityType, EntityId).
/// </summary>
[Table("LifecycleLogs")]
public class LifecycleLog
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }

    [Required]
    [MaxLength(50)]
    public string FromStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ToStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PerformedByUserId { get; set; } = "system";

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    public DateTime TransitionedAtUtc { get; set; } = DateTime.UtcNow;
}
