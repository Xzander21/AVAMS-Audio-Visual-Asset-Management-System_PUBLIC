using System.ComponentModel.DataAnnotations;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class CreateDisposalDto
{
    [Required]
    public int EquipmentId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DisposalMethod Method { get; set; } = DisposalMethod.Recycled;

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }
}
