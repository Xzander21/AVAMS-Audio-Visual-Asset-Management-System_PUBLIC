using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.Models;

/// <summary>
/// A Disposal transaction records the planned and actual removal of an equipment
/// item from service. It is initiated by the Admin (Pending), authorized
/// (Approved), and resolved when the equipment is physically removed (Disposed)
/// or the decision is reversed (Cancelled).
/// </summary>
public class Disposal
{
    public int Id { get; set; }

    [Required]
    public int EquipmentId { get; set; }

    [ForeignKey(nameof(EquipmentId))]
    public Equipment? Equipment { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DisposalMethod Method { get; set; } = DisposalMethod.Recycled;

    public DisposalStatus Status { get; set; } = DisposalStatus.Pending;

    [MaxLength(200)]
    public string? RequestedBy { get; set; }

    [MaxLength(200)]
    public string? ApprovedBy { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public DateTime? DisposedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional FK to a Reserved-status Equipment unit that takes the place of
    /// the disposed unit. When the Supervisor marks this Disposal as Disposed
    /// with a replacement selected, DisposalLifecycleService.DisposeAsync
    /// atomically (inside one IDbContextTransaction):
    ///   • Decommissions the original Equipment (Status = Decommissioned).
    ///   • Promotes the spare to Active and copies the disposed unit's RoomName.
    ///   • Writes paired LifecycleLog audit rows for both equipment transitions.
    /// </summary>
    public int? ReplacementEquipmentId { get; set; }

    [ForeignKey(nameof(ReplacementEquipmentId))]
    public Equipment? ReplacementEquipment { get; set; }

    /// <summary>
    /// EF Core optimistic-concurrency token. Mutated by RowVersionInterceptor
    /// on every SaveChanges; mismatch on UPDATE throws DbUpdateConcurrencyException,
    /// which DisposalLifecycleService catches and converts to a clean "refresh
    /// and try again" Result.Fail.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
