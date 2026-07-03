using System.ComponentModel.DataAnnotations;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class UpdateDisposalDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DisposalMethod Method { get; set; }

    public DisposalStatus Status { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }
}
