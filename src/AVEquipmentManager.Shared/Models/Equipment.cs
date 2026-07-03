using System.ComponentModel.DataAnnotations;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.Models;

public class Equipment
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Asset classification (Projector, Microphone, Display, etc.).</summary>
    public AssetCategory Category { get; set; } = AssetCategory.Other;

    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RoomName { get; set; } = string.Empty;

    [Required]
    public DateTime DateInstalled { get; set; }

    /// <summary>
    /// Expected operational lifespan in years. Defaults to 5, which is the
    /// client institution's standard retirement basis for audio-visual
    /// equipment. The Admin may override per unit when warranted by
    /// manufacturer specification or operational evidence. AVAMS uses this
    /// value (combined with DateInstalled) to deterministically flag units
    /// at or past their expected end-of-life; it is NOT a predictive value.
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int ExpectedLifeInYears { get; set; } = 5;

    public EquipmentStatus Status { get; set; } = EquipmentStatus.Active;

    // --- Per-unit accessory flags (2026-06-24 client clarification, Phase 1) -----
    // Captured per individual AV unit so the AV team can tell at a glance which
    // rooms have Apple TV streaming, a wired wall speaker, or an installed
    // remote-control holder/bracket. Normalised lookups for projector model
    // and VLAN remain a Phase 2 enhancement.
    public bool HasAppleTv      { get; set; } = false;
    public bool HasWallSpeaker  { get; set; } = false;
    public bool HasRemoteHolder { get; set; } = false;
    // ----------------------------------------------------------------------------

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. Archived equipment is hidden from normal listings.</summary>
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAt { get; set; }

    /// <summary>EF Core optimistic-concurrency token (RowVersionInterceptor-managed).</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
