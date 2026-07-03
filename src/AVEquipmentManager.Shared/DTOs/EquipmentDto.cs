using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class EquipmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime DateInstalled { get; set; }
    public int ExpectedLifeInYears { get; set; }
    public EquipmentStatus Status { get; set; }

    // Per-unit accessory flags (Phase 1, 2026-06-24)
    public bool HasAppleTv      { get; set; }
    public bool HasWallSpeaker  { get; set; }
    public bool HasRemoteHolder { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
