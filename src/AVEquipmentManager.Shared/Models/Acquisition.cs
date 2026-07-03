using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.Models;

/// <summary>
/// Acquisition transaction. Represents the Plan → Acquire → Deploy phase
/// of the ITIL Asset Lifecycle. When status reaches Deployed, an Equipment
/// record is created (or linked) so the asset enters the operational inventory.
/// </summary>
public class Acquisition
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    public AssetCategory Category { get; set; } = AssetCategory.Other;

    [MaxLength(200)]
    public string? Vendor { get; set; }

    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    public decimal? UnitCost { get; set; }

    public int Quantity { get; set; } = 1;

    [MaxLength(200)]
    public string? IntendedRoom { get; set; }

    public AcquisitionStatus Status { get; set; } = AcquisitionStatus.Planned;

    [MaxLength(200)]
    public string? RequestedBy { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OrderedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? DeployedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The Equipment record created when this acquisition was deployed (nullable).</summary>
    public int? DeployedEquipmentId { get; set; }

    [ForeignKey(nameof(DeployedEquipmentId))]
    public Equipment? DeployedEquipment { get; set; }

    /// <summary>EF Core optimistic-concurrency token (RowVersionInterceptor-managed).</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
