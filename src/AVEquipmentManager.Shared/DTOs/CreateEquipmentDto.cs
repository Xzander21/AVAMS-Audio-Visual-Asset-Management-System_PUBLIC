using System.ComponentModel.DataAnnotations;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class CreateEquipmentDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

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
    /// Default is 5 years per the client institution's retirement policy for
    /// audio-visual equipment. Overridable per unit.
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int ExpectedLifeInYears { get; set; } = 5;

    public EquipmentStatus Status { get; set; } = EquipmentStatus.Active;

    // Per-unit accessory flags (Phase 1, 2026-06-24)
    public bool HasAppleTv      { get; set; } = false;
    public bool HasWallSpeaker  { get; set; } = false;
    public bool HasRemoteHolder { get; set; } = false;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
