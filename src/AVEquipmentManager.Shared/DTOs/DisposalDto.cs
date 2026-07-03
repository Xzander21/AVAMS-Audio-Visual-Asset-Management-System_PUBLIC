using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class DisposalDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string EquipmentSerial { get; set; } = string.Empty;
    public string EquipmentRoom { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DisposalMethod Method { get; set; }
    public DisposalStatus Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? DisposalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DisposedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Equipment that was promoted from Reserved to Active to replace the disposed unit. Null when no replacement was used.</summary>
    public int?    ReplacementEquipmentId   { get; set; }
    public string? ReplacementEquipmentName { get; set; }
    public string? ReplacementEquipmentSerial { get; set; }
}
