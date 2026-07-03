using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

public class AcquisitionDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string? Vendor { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; }
    public string? IntendedRoom { get; set; }
    public AcquisitionStatus Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OrderedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? DeployedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? DeployedEquipmentId { get; set; }
}

public class CreateAcquisitionDto
{
    public string ItemName { get; set; } = string.Empty;
    public AssetCategory Category { get; set; } = AssetCategory.Other;
    public string? Vendor { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
    public string? IntendedRoom { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAcquisitionDto
{
    public string ItemName { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string? Vendor { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
    public string? IntendedRoom { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Payload for the Deploy action — creates an Equipment record.</summary>
public class DeployAcquisitionDto
{
    public string SerialNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime DateInstalled { get; set; } = DateTime.UtcNow;
    public int ExpectedLifeInYears { get; set; } = 5;
}
